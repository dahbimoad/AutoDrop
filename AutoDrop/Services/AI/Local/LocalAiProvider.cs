using System.Text;
using System.Text.Json;
using AutoDrop.Models;
using Microsoft.Extensions.Logging;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace AutoDrop.Services.AI.Local;

/// <summary>
/// Local AI provider using embedded ONNX models for 100% offline file classification.
/// Uses sentence-transformers (all-MiniLM-L6-v2) for semantic similarity matching.
/// </summary>
public sealed class LocalAiProvider : AiProviderBase
{
    private readonly OnnxModelManager _modelManager;
    private readonly LocalAiOptions _options;
    private bool _disposed;

    // Special tokens for BERT-based tokenization
    private const int ClsTokenId = 101;  // [CLS]
    private const int SepTokenId = 102;  // [SEP]
    private const int PadTokenId = 0;    // [PAD]
    private const int UnkTokenId = 100;  // [UNK]
    private const int MaxSequenceLength = 128;

    private static readonly AiProviderInfo _providerInfo = new()
    {
        Provider = AiProvider.Local,
        DisplayName = "Local AI (Offline)",
        Description = "100% offline AI classification - complete privacy, no internet required",
        IsLocal = true,
        RequiresApiKey = false,
        SupportsTextPrompts = false, // Embedding model - can't generate text responses
        IconGlyph = "\uE8B7", // Chip/processor icon
        Models =
        [
            new()
            {
                Id = "all-MiniLM-L6-v2",
                DisplayName = "MiniLM v2 (Recommended)",
                SupportsVision = true,
                SupportsPdf = false,
                MaxTokens = 512,
                Description = "Fast semantic classification (~90MB)"
            }
        ]
    };

    public LocalAiProvider(
        OnnxModelManager modelManager,
        LocalAiOptions options,
        ILogger<LocalAiProvider> logger) : base(logger)
    {
        _modelManager = modelManager ?? throw new ArgumentNullException(nameof(modelManager));
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public override AiProvider ProviderType => AiProvider.Local;
    public override AiProviderInfo ProviderInfo => _providerInfo;
    public override bool SupportsVision => true;
    public override bool SupportsPdf => false;

    /// <summary>
    /// Gets the model manager for status checking and downloads.
    /// </summary>
    public OnnxModelManager ModelManager => _modelManager;

    protected override void ConfigureHttpClient()
    {
        // No HTTP client needed for local inference
    }

    public override async Task<bool> ValidateAsync(CancellationToken ct = default)
    {
        try
        {
            if (!_modelManager.AreModelsDownloaded())
            {
                Logger.LogWarning("Local AI models not downloaded");
                return false;
            }

            // Try to load the session to validate it works
            await _modelManager.GetEmbeddingSessionAsync(ct);
            await _modelManager.GetVocabularyAsync(ct);
            return true;
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Local AI validation failed");
            return false;
        }
    }

    public override async Task<AiAnalysisResult> AnalyzeImageAsync(
        string imagePath,
        IReadOnlyList<CustomFolder>? customFolders = null,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(imagePath);

        if (!File.Exists(imagePath))
            return AiAnalysisResult.Failed("File not found.");

        if (!_modelManager.AreModelsDownloaded())
            return AiAnalysisResult.Failed("Local AI models not downloaded. Please download models first.");

        try
        {
            Logger.LogDebug("[Local AI] Analyzing image: {Path}", Path.GetFileName(imagePath));

            // Build context from filename and image metadata
            var filename = Path.GetFileNameWithoutExtension(imagePath);
            var extension = Path.GetExtension(imagePath);
            var contextText = await BuildImageContextAsync(imagePath, filename, ct);

            // Get embedding for the context
            var embedding = await GetEmbeddingAsync(contextText, ct);

            // Find best matching category (image categories only)
            var (category, confidence, categoryDef) = await FindBestCategoryAsync(
                embedding, isImage: true, ct);

            // Try to match to custom folders
            var (matchedFolderId, matchedFolderPath, matchedFolderName) =
                MatchToCustomFolder(category, customFolders);

            // Generate suggested name
            var suggestedName = GenerateSuggestedName(filename, category);

            Logger.LogInformation(
                "[Local AI] Image classified as {Category} ({Confidence:P0})",
                category, confidence);

            return new AiAnalysisResult
            {
                Success = true,
                Category = category,
                Confidence = confidence,
                ContentType = AiContentType.Image,
                Description = $"Local AI: {categoryDef?.Description ?? category}",
                SuggestedName = suggestedName,
                MatchedFolderId = matchedFolderId,
                MatchedFolderPath = matchedFolderPath,
                MatchedFolderName = matchedFolderName,
                SuggestedNewFolderPath = matchedFolderId == null ? $"Pictures/{category}" : null
            };
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "[Local AI] Image analysis failed");
            return AiAnalysisResult.Failed($"Local analysis failed: {ex.Message}");
        }
    }

    public override async Task<AiAnalysisResult> AnalyzeDocumentAsync(
        string documentPath,
        IReadOnlyList<CustomFolder>? customFolders = null,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(documentPath);

        if (!File.Exists(documentPath))
            return AiAnalysisResult.Failed("File not found.");

        if (!_modelManager.AreModelsDownloaded())
            return AiAnalysisResult.Failed("Local AI models not downloaded. Please download models first.");

        try
        {
            Logger.LogDebug("[Local AI] Analyzing document: {Path}", Path.GetFileName(documentPath));

            var filename = Path.GetFileNameWithoutExtension(documentPath);
            var extension = Path.GetExtension(documentPath).ToLowerInvariant();

            // Read document content (limited)
            var content = await ReadDocumentContentAsync(documentPath, ct);

            // Build context combining filename and content
            var contextText = $"Filename: {filename}{extension}\n\nContent preview:\n{content}";

            // Get embedding for the context
            var embedding = await GetEmbeddingAsync(contextText, ct);

            // Find best matching category (document categories)
            var (category, confidence, categoryDef) = await FindBestCategoryAsync(
                embedding, isImage: false, ct);

            // Try to match to custom folders
            var (matchedFolderId, matchedFolderPath, matchedFolderName) =
                MatchToCustomFolder(category, customFolders);

            // Generate suggested name
            var suggestedName = GenerateSuggestedName(filename, category);

            Logger.LogInformation(
                "[Local AI] Document classified as {Category} ({Confidence:P0})",
                category, confidence);

            return new AiAnalysisResult
            {
                Success = true,
                Category = category,
                Confidence = confidence,
                ContentType = AiContentType.Document,
                Description = $"Local AI: {categoryDef?.Description ?? category}",
                SuggestedName = suggestedName,
                MatchedFolderId = matchedFolderId,
                MatchedFolderPath = matchedFolderPath,
                MatchedFolderName = matchedFolderName,
                SuggestedNewFolderPath = matchedFolderId == null ? $"Documents/{category}" : null
            };
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "[Local AI] Document analysis failed");
            return AiAnalysisResult.Failed($"Local analysis failed: {ex.Message}");
        }
    }

    public override Task<string> SendTextPromptAsync(string prompt, CancellationToken ct = default)
    {
        // Local embedding models don't support chat - return informative message
        return Task.FromResult(
            "Local AI uses semantic classification and doesn't support text prompts. " +
            "Use a cloud provider (OpenAI, Claude, Gemini) for chat functionality.");
    }

    /// <summary>
    /// Generates text embedding using the ONNX model.
    /// </summary>
    private async Task<float[]> GetEmbeddingAsync(string text, CancellationToken ct)
    {
        var session = await _modelManager.GetEmbeddingSessionAsync(ct);
        var (_, vocabLookup) = await _modelManager.GetVocabularyAsync(ct);

        // Tokenize the text
        var (inputIds, attentionMask, tokenTypeIds) = Tokenize(text, vocabLookup);

        // Create input tensors
        var inputIdsTensor = new DenseTensor<long>(inputIds, [1, inputIds.Length]);
        var attentionMaskTensor = new DenseTensor<long>(attentionMask, [1, attentionMask.Length]);
        var tokenTypeIdsTensor = new DenseTensor<long>(tokenTypeIds, [1, tokenTypeIds.Length]);

        var inputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor("input_ids", inputIdsTensor),
            NamedOnnxValue.CreateFromTensor("attention_mask", attentionMaskTensor),
            NamedOnnxValue.CreateFromTensor("token_type_ids", tokenTypeIdsTensor)
        };

        // Run inference
        using var results = session.Run(inputs);

        // Get sentence embedding (mean pooling of last hidden state)
        var lastHiddenState = results.First(r => r.Name == "last_hidden_state").AsTensor<float>();
        return MeanPooling(lastHiddenState, attentionMask);
    }

    /// <summary>
    /// Simple WordPiece tokenization for BERT models.
    /// </summary>
    private static (long[] InputIds, long[] AttentionMask, long[] TokenTypeIds) Tokenize(
        string text,
        Dictionary<string, int> vocabLookup)
    {
        var tokens = new List<int> { ClsTokenId };

        // Simple tokenization: lowercase, split on spaces and punctuation
        char[] separators = [' ', '\t', '\n', '\r', '.', ',', '!', '?', ';', ':', '-', '_', '(', ')', '[', ']', '{', '}', '"', '\''];
        var words = text.ToLowerInvariant().Split(separators, StringSplitOptions.RemoveEmptyEntries);

        foreach (var word in words)
        {
            if (tokens.Count >= MaxSequenceLength - 1) break;

            // Try to find the word in vocabulary
            if (vocabLookup.TryGetValue(word, out var tokenId))
            {
                tokens.Add(tokenId);
            }
            else
            {
                // WordPiece: try to break into subwords
                var subTokens = TokenizeWordPiece(word, vocabLookup);
                foreach (var subToken in subTokens)
                {
                    if (tokens.Count >= MaxSequenceLength - 1) break;
                    tokens.Add(subToken);
                }
            }
        }

        tokens.Add(SepTokenId);

        // Pad to MaxSequenceLength
        var inputIds = new long[MaxSequenceLength];
        var attentionMask = new long[MaxSequenceLength];
        var tokenTypeIds = new long[MaxSequenceLength];

        for (var i = 0; i < MaxSequenceLength; i++)
        {
            if (i < tokens.Count)
            {
                inputIds[i] = tokens[i];
                attentionMask[i] = 1;
            }
            else
            {
                inputIds[i] = PadTokenId;
                attentionMask[i] = 0;
            }
            tokenTypeIds[i] = 0;
        }

        return (inputIds, attentionMask, tokenTypeIds);
    }

    /// <summary>
    /// Basic WordPiece tokenization for unknown words.
    /// </summary>
    private static List<int> TokenizeWordPiece(string word, Dictionary<string, int> vocabLookup)
    {
        var tokens = new List<int>();
        var start = 0;

        while (start < word.Length)
        {
            var end = word.Length;
            var foundToken = false;

            while (start < end)
            {
                var substr = start > 0 ? $"##{word[start..end]}" : word[start..end];

                if (vocabLookup.TryGetValue(substr, out var tokenId))
                {
                    tokens.Add(tokenId);
                    foundToken = true;
                    start = end;
                    break;
                }
                end--;
            }

            if (!foundToken)
            {
                tokens.Add(UnkTokenId);
                start++;
            }
        }

        return tokens;
    }

    /// <summary>
    /// Mean pooling over the sequence dimension, masked by attention.
    /// </summary>
    private static float[] MeanPooling(Tensor<float> lastHiddenState, long[] attentionMask)
    {
        var seqLen = lastHiddenState.Dimensions[1];
        var hiddenSize = lastHiddenState.Dimensions[2];

        var pooled = new float[hiddenSize];
        var count = 0;

        for (var i = 0; i < seqLen; i++)
        {
            if (attentionMask[i] == 0) continue;
            count++;

            for (var j = 0; j < hiddenSize; j++)
            {
                pooled[j] += lastHiddenState[0, i, j];
            }
        }

        if (count > 0)
        {
            for (var j = 0; j < hiddenSize; j++)
            {
                pooled[j] /= count;
            }
        }

        // L2 normalize
        var norm = (float)Math.Sqrt(pooled.Sum(x => x * x));
        if (norm > 0)
        {
            for (var j = 0; j < pooled.Length; j++)
            {
                pooled[j] /= norm;
            }
        }

        return pooled;
    }

    /// <summary>
    /// Finds the best matching category using cosine similarity.
    /// </summary>
    private async Task<(string Category, double Confidence, CategoryDefinition? Definition)> FindBestCategoryAsync(
        float[] embedding,
        bool isImage,
        CancellationToken ct)
    {
        var categoryEmbeddings = await _modelManager.GetCategoryEmbeddingsAsync(
            GetEmbeddingAsync, ct);

        var bestScore = float.MinValue;
        var bestIndex = 0;

        for (var i = 0; i < _options.Categories.Count; i++)
        {
            var category = _options.Categories[i];

            // Filter by image/document category if needed
            if (isImage && !category.IsImageCategory) continue;
            if (!isImage && category.IsImageCategory) continue;

            var score = CosineSimilarity(embedding, categoryEmbeddings[i]);

            if (score > bestScore)
            {
                bestScore = score;
                bestIndex = i;
            }
        }

        var bestCategory = _options.Categories[bestIndex];

        // Convert cosine similarity [-1, 1] to confidence [0, 1]
        var confidence = Math.Clamp((bestScore + 1) / 2.0, 0, 1);

        return (bestCategory.Name, confidence, bestCategory);
    }

    /// <summary>
    /// Calculates cosine similarity between two vectors.
    /// </summary>
    private static float CosineSimilarity(float[] a, float[] b)
    {
        if (a.Length != b.Length) return 0;

        float dot = 0, normA = 0, normB = 0;

        for (var i = 0; i < a.Length; i++)
        {
            dot += a[i] * b[i];
            normA += a[i] * a[i];
            normB += b[i] * b[i];
        }

        var denominator = (float)(Math.Sqrt(normA) * Math.Sqrt(normB));
        return denominator > 0 ? dot / denominator : 0;
    }

    /// <summary>
    /// Builds context text from image metadata.
    /// </summary>
    private async Task<string> BuildImageContextAsync(string imagePath, string filename, CancellationToken ct)
    {
        var sb = new StringBuilder();
        sb.Append($"Image filename: {filename}");

        try
        {
            using var image = await Image.LoadAsync<Rgba32>(imagePath, ct);
            var width = image.Width;
            var height = image.Height;
            var aspectRatio = (float)width / height;

            sb.Append($" | Dimensions: {width}x{height}");

            // Aspect ratio hints
            if (Math.Abs(aspectRatio - 1.0f) < 0.1f)
                sb.Append(" | Square aspect ratio, possibly profile picture or icon");
            else if (aspectRatio > 1.5f)
                sb.Append(" | Wide/landscape orientation");
            else if (aspectRatio < 0.7f)
                sb.Append(" | Tall/portrait orientation, possibly screenshot or document scan");

            // Size hints
            if (width >= 1920 || height >= 1080)
                sb.Append(" | High resolution, possibly photo or screenshot");
            else if (width < 500 && height < 500)
                sb.Append(" | Small size, possibly icon or thumbnail");
        }
        catch (Exception ex)
        {
            Logger.LogDebug(ex, "Could not read image metadata for {Path}", imagePath);
        }

        return sb.ToString();
    }

    /// <summary>
    /// Reads document content with size limits.
    /// </summary>
    private static async Task<string> ReadDocumentContentAsync(string path, CancellationToken ct)
    {
        const int maxChars = 2000;

        try
        {
            var content = await File.ReadAllTextAsync(path, ct);
            return content.Length > maxChars
                ? content[..maxChars] + "..."
                : content;
        }
        catch
        {
            return string.Empty;
        }
    }

    /// <summary>
    /// Matches a category to user's custom folders.
    /// </summary>
    private static (string? Id, string? Path, string? Name) MatchToCustomFolder(
        string category,
        IReadOnlyList<CustomFolder>? customFolders)
    {
        if (customFolders == null || customFolders.Count == 0)
            return (null, null, null);

        // Try exact name match first
        var matched = customFolders.FirstOrDefault(f =>
            f.Name.Equals(category, StringComparison.OrdinalIgnoreCase));

        // Try contains match
        matched ??= customFolders.FirstOrDefault(f =>
            f.Name.Contains(category, StringComparison.OrdinalIgnoreCase) ||
            f.Path.Contains(category, StringComparison.OrdinalIgnoreCase));

        // Try partial word match
        matched ??= customFolders.FirstOrDefault(f =>
            category.Split(' ').Any(word =>
                f.Name.Contains(word, StringComparison.OrdinalIgnoreCase)));

        return matched != null
            ? (matched.Id.ToString(), matched.Path, matched.Name)
            : (null, null, null);
    }

    /// <summary>
    /// Generates a suggested filename based on original and category.
    /// </summary>
    private static string GenerateSuggestedName(string originalName, string category)
    {
        // Clean up the original name
        var cleaned = originalName
            .Replace("_", " ")
            .Replace("-", " ")
            .Trim();

        // If name is very generic (like IMG_1234), suggest category-based name
        if (IsGenericName(cleaned))
        {
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            return $"{category}_{timestamp}";
        }

        return cleaned;
    }

    /// <summary>
    /// Checks if a filename is generic (like IMG_1234, DSC00001, etc.)
    /// </summary>
    private static bool IsGenericName(string name)
    {
        var upperName = name.ToUpperInvariant();
        string[] genericPrefixes = ["IMG", "DSC", "DCIM", "PHOTO", "IMAGE", "SCREENSHOT", "SCREEN"];

        return genericPrefixes.Any(prefix => upperName.StartsWith(prefix)) ||
               System.Text.RegularExpressions.Regex.IsMatch(name, @"^\d+$");
    }

    protected override void Dispose(bool disposing)
    {
        if (_disposed) return;

        if (disposing)
        {
            // ModelManager is managed by DI container
        }

        _disposed = true;
        base.Dispose(disposing);
    }
}
