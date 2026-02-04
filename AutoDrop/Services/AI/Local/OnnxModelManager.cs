using System.Net.Http;
using Microsoft.Extensions.Logging;
using Microsoft.ML.OnnxRuntime;

namespace AutoDrop.Services.AI.Local;

/// <summary>
/// Manages ONNX model loading, downloading, and lifecycle.
/// Thread-safe with lazy initialization and progress reporting.
/// </summary>
public sealed class OnnxModelManager : IDisposable
{
    private readonly LocalAiOptions _options;
    private readonly ILogger<OnnxModelManager> _logger;
    private readonly SemaphoreSlim _loadLock = new(1, 1);
    private readonly HttpClient _httpClient;

    private InferenceSession? _embeddingSession;
    private string[]? _vocabulary;
    private Dictionary<string, int>? _vocabLookup;
    private float[][]? _categoryEmbeddings;
    private bool _disposed;

    /// <summary>
    /// Event raised when download progress changes.
    /// </summary>
    public event EventHandler<LocalModelStatus>? StatusChanged;

    public OnnxModelManager(
        LocalAiOptions options,
        ILogger<OnnxModelManager> logger)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _httpClient = new HttpClient { Timeout = TimeSpan.FromMinutes(15) };

        EnsureModelDirectoryExists();
    }

    /// <summary>
    /// Gets the current model status without blocking.
    /// </summary>
    public LocalModelStatus GetStatus()
    {
        if (_embeddingSession != null && _vocabulary != null)
        {
            return LocalModelStatus.Ready(GetCachedModelsSize());
        }

        return AreModelsDownloaded()
            ? LocalModelStatus.Ready(GetCachedModelsSize())
            : LocalModelStatus.NotReady();
    }

    /// <summary>
    /// Checks if all required models are downloaded.
    /// </summary>
    public bool AreModelsDownloaded()
    {
        var modelPath = Path.Combine(_options.ModelsPath, _options.TextModelFileName);
        var vocabPath = Path.Combine(_options.ModelsPath, _options.VocabFileName);
        return File.Exists(modelPath) && File.Exists(vocabPath);
    }

    /// <summary>
    /// Downloads required models with progress reporting.
    /// </summary>
    public async Task DownloadModelsAsync(
        IProgress<LocalModelStatus>? progress = null,
        CancellationToken ct = default)
    {
        await _loadLock.WaitAsync(ct);
        try
        {
            if (AreModelsDownloaded())
            {
                var status = LocalModelStatus.Ready(GetCachedModelsSize());
                progress?.Report(status);
                StatusChanged?.Invoke(this, status);
                return;
            }

            _logger.LogInformation("Starting model download to {Path}", _options.ModelsPath);

            // Download vocabulary first (smaller, ~230KB)
            var vocabPath = Path.Combine(_options.ModelsPath, _options.VocabFileName);
            if (!File.Exists(vocabPath))
            {
                var vocabStatus = LocalModelStatus.Downloading(0.1, "Downloading vocabulary...");
                progress?.Report(vocabStatus);
                StatusChanged?.Invoke(this, vocabStatus);

                await DownloadFileAsync(_options.VocabUrl, vocabPath, ct);
                _logger.LogInformation("Vocabulary downloaded: {Path}", vocabPath);
            }

            // Download ONNX model (~90MB)
            var modelPath = Path.Combine(_options.ModelsPath, _options.TextModelFileName);
            if (!File.Exists(modelPath))
            {
                await DownloadFileWithProgressAsync(
                    _options.TextModelUrl,
                    modelPath,
                    (downloaded, total) =>
                    {
                        var progressPct = total > 0 ? (double)downloaded / total : 0;
                        var overallProgress = 0.1 + (progressPct * 0.9); // 10% for vocab, 90% for model
                        var status = LocalModelStatus.Downloading(
                            overallProgress,
                            $"Downloading model... {downloaded / (1024 * 1024):F1}MB / {total / (1024 * 1024):F1}MB");
                        progress?.Report(status);
                        StatusChanged?.Invoke(this, status);
                    },
                    ct);
                _logger.LogInformation("Model downloaded: {Path}", modelPath);
            }

            var finalStatus = LocalModelStatus.Ready(GetCachedModelsSize());
            progress?.Report(finalStatus);
            StatusChanged?.Invoke(this, finalStatus);

            _logger.LogInformation("All models downloaded successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Model download failed");
            var errorStatus = LocalModelStatus.Error(ex.Message);
            progress?.Report(errorStatus);
            StatusChanged?.Invoke(this, errorStatus);
            throw;
        }
        finally
        {
            _loadLock.Release();
        }
    }

    /// <summary>
    /// Gets or loads the embedding inference session.
    /// </summary>
    public async Task<InferenceSession> GetEmbeddingSessionAsync(CancellationToken ct = default)
    {
        if (_embeddingSession != null) return _embeddingSession;

        await _loadLock.WaitAsync(ct);
        try
        {
            if (_embeddingSession != null) return _embeddingSession;

            var modelPath = Path.Combine(_options.ModelsPath, _options.TextModelFileName);

            if (!File.Exists(modelPath))
            {
                throw new InvalidOperationException(
                    "Model not downloaded. Call DownloadModelsAsync first.");
            }

            _embeddingSession = CreateSession(modelPath);
            _logger.LogInformation("Embedding model loaded from {Path}", modelPath);
            return _embeddingSession;
        }
        finally
        {
            _loadLock.Release();
        }
    }

    /// <summary>
    /// Gets the vocabulary for tokenization.
    /// </summary>
    public async Task<(string[] Vocabulary, Dictionary<string, int> Lookup)> GetVocabularyAsync(
        CancellationToken ct = default)
    {
        if (_vocabulary != null && _vocabLookup != null)
            return (_vocabulary, _vocabLookup);

        await _loadLock.WaitAsync(ct);
        try
        {
            if (_vocabulary != null && _vocabLookup != null)
                return (_vocabulary, _vocabLookup);

            var vocabPath = Path.Combine(_options.ModelsPath, _options.VocabFileName);

            if (!File.Exists(vocabPath))
            {
                throw new InvalidOperationException(
                    "Vocabulary not downloaded. Call DownloadModelsAsync first.");
            }

            _vocabulary = await File.ReadAllLinesAsync(vocabPath, ct);
            _vocabLookup = new Dictionary<string, int>(_vocabulary.Length);

            for (var i = 0; i < _vocabulary.Length; i++)
            {
                _vocabLookup[_vocabulary[i]] = i;
            }

            _logger.LogInformation("Vocabulary loaded: {Count} tokens", _vocabulary.Length);
            return (_vocabulary, _vocabLookup);
        }
        finally
        {
            _loadLock.Release();
        }
    }

    /// <summary>
    /// Gets or computes category embeddings for similarity matching.
    /// </summary>
    public async Task<float[][]> GetCategoryEmbeddingsAsync(
        Func<string, CancellationToken, Task<float[]>> embedFunc,
        CancellationToken ct = default)
    {
        if (_categoryEmbeddings != null) return _categoryEmbeddings;

        await _loadLock.WaitAsync(ct);
        try
        {
            if (_categoryEmbeddings != null) return _categoryEmbeddings;

            _logger.LogInformation("Computing category embeddings for {Count} categories",
                _options.Categories.Count);

            _categoryEmbeddings = new float[_options.Categories.Count][];

            for (var i = 0; i < _options.Categories.Count; i++)
            {
                var category = _options.Categories[i];
                var text = $"{category.Name}: {category.Description}";
                _categoryEmbeddings[i] = await embedFunc(text, ct);
            }

            _logger.LogInformation("Category embeddings computed");
            return _categoryEmbeddings;
        }
        finally
        {
            _loadLock.Release();
        }
    }

    /// <summary>
    /// Gets the total size of cached models in bytes.
    /// </summary>
    public long GetCachedModelsSize()
    {
        if (!Directory.Exists(_options.ModelsPath)) return 0;

        return Directory.GetFiles(_options.ModelsPath)
            .Where(f => f.EndsWith(".onnx") || f.EndsWith(".txt"))
            .Sum(f => new FileInfo(f).Length);
    }

    /// <summary>
    /// Deletes all cached models.
    /// </summary>
    public void ClearCache()
    {
        if (!Directory.Exists(_options.ModelsPath)) return;

        _embeddingSession?.Dispose();
        _embeddingSession = null;
        _vocabulary = null;
        _vocabLookup = null;
        _categoryEmbeddings = null;

        foreach (var file in Directory.GetFiles(_options.ModelsPath))
        {
            try
            {
                File.Delete(file);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to delete cached file: {Path}", file);
            }
        }

        _logger.LogInformation("Model cache cleared");
    }

    private InferenceSession CreateSession(string modelPath)
    {
        var sessionOptions = new SessionOptions();

        // Configure threading
        if (_options.MaxThreads > 0)
        {
            sessionOptions.IntraOpNumThreads = _options.MaxThreads;
            sessionOptions.InterOpNumThreads = Math.Max(1, _options.MaxThreads / 2);
        }
        else
        {
            // Auto-configure based on CPU
            var cpuCount = Environment.ProcessorCount;
            sessionOptions.IntraOpNumThreads = Math.Max(1, cpuCount / 2);
            sessionOptions.InterOpNumThreads = Math.Max(1, cpuCount / 4);
        }

        // Enable optimizations
        sessionOptions.GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL;

        // Try GPU acceleration (DirectML) if enabled
        if (_options.UseGpuIfAvailable)
        {
            try
            {
                sessionOptions.AppendExecutionProvider_DML(0);
                _logger.LogInformation("Using DirectML GPU acceleration");
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "DirectML not available, using CPU");
            }
        }

        return new InferenceSession(modelPath, sessionOptions);
    }

    private void EnsureModelDirectoryExists()
    {
        if (!Directory.Exists(_options.ModelsPath))
        {
            Directory.CreateDirectory(_options.ModelsPath);
            _logger.LogDebug("Created models directory: {Path}", _options.ModelsPath);
        }
    }

    private async Task DownloadFileAsync(string url, string destination, CancellationToken ct)
    {
        using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        await using var fileStream = File.Create(destination);
        await stream.CopyToAsync(fileStream, ct);
    }

    private async Task DownloadFileWithProgressAsync(
        string url,
        string destination,
        Action<long, long> progressCallback,
        CancellationToken ct)
    {
        using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();

        var totalBytes = response.Content.Headers.ContentLength ?? -1;
        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        await using var fileStream = File.Create(destination);

        var buffer = new byte[81920]; // 80KB buffer
        long downloadedBytes = 0;
        int bytesRead;
        var lastReport = DateTime.UtcNow;

        while ((bytesRead = await stream.ReadAsync(buffer, ct)) > 0)
        {
            await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), ct);
            downloadedBytes += bytesRead;

            // Report progress every 100ms
            if (DateTime.UtcNow - lastReport > TimeSpan.FromMilliseconds(100))
            {
                progressCallback(downloadedBytes, totalBytes);
                lastReport = DateTime.UtcNow;
            }
        }

        // Final progress report
        progressCallback(downloadedBytes, totalBytes);
    }

    public void Dispose()
    {
        if (_disposed) return;

        _embeddingSession?.Dispose();
        _httpClient.Dispose();
        _loadLock.Dispose();

        _disposed = true;
    }
}
