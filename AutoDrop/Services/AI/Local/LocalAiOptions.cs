namespace AutoDrop.Services.AI.Local;

/// <summary>
/// Configuration options for local ONNX-based AI classification.
/// </summary>
public sealed class LocalAiOptions
{
    /// <summary>
    /// Path where ONNX models are cached.
    /// Default: %AppData%\AutoDrop\Models
    /// </summary>
    public string ModelsPath { get; set; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "AutoDrop", "Models");

    /// <summary>
    /// Whether to use GPU acceleration (DirectML) if available.
    /// </summary>
    public bool UseGpuIfAvailable { get; set; } = true;

    /// <summary>
    /// Maximum threads for CPU inference.
    /// 0 = auto-detect based on CPU cores.
    /// </summary>
    public int MaxThreads { get; set; }

    /// <summary>
    /// URL to download the text embedding model.
    /// Using all-MiniLM-L6-v2 ONNX from Hugging Face.
    /// </summary>
    public string TextModelUrl { get; set; } = 
        "https://huggingface.co/sentence-transformers/all-MiniLM-L6-v2/resolve/main/onnx/model.onnx";

    /// <summary>
    /// URL to download the tokenizer vocabulary file.
    /// </summary>
    public string VocabUrl { get; set; } = 
        "https://huggingface.co/sentence-transformers/all-MiniLM-L6-v2/resolve/main/vocab.txt";

    /// <summary>
    /// Filename for the text classification model.
    /// </summary>
    public string TextModelFileName { get; set; } = "all-MiniLM-L6-v2.onnx";

    /// <summary>
    /// Filename for the vocabulary file.
    /// </summary>
    public string VocabFileName { get; set; } = "vocab.txt";

    /// <summary>
    /// File classification categories with descriptions for embedding matching.
    /// </summary>
    public IReadOnlyList<CategoryDefinition> Categories { get; set; } =
    [
        // Images
        new("Screenshots", "screenshot screen capture desktop window application UI interface", true),
        new("Photos", "photo photograph picture portrait selfie person people family friends", true),
        new("Landscapes", "landscape nature scenery mountain ocean beach sunset sunrise sky outdoor", true),
        new("Artwork", "art artwork drawing painting illustration digital art creative design", true),
        new("Memes", "meme funny humor comic joke internet viral social media", true),
        new("Receipts", "receipt invoice bill payment transaction purchase store shop", true),
        new("Documents", "document scan scanned paper form certificate official", true),
        new("Diagrams", "diagram chart graph flowchart architecture technical schematic", true),
        new("Products", "product item merchandise shopping ecommerce listing catalog", true),
        
        // Documents
        new("Invoices", "invoice billing payment amount due total charge financial business"),
        new("Contracts", "contract agreement legal terms conditions party signature binding"),
        new("Resumes", "resume cv curriculum vitae experience education skills job career"),
        new("Reports", "report analysis summary findings data statistics quarterly annual"),
        new("Letters", "letter correspondence communication formal message memo"),
        new("Notes", "notes memo reminder todo list personal draft"),
        new("Code", "code programming source script function class method variable software"),
        new("Configuration", "configuration config settings json xml yaml ini properties"),
        new("Financial", "financial finance tax budget accounting money investment bank"),
        new("Medical", "medical health patient prescription diagnosis treatment doctor"),
        new("Legal", "legal law court case attorney lawyer litigation"),
        new("Technical", "technical documentation manual guide tutorial reference API"),
        new("Spreadsheets", "spreadsheet excel data table calculation formula budget"),
        new("Presentations", "presentation slides powerpoint keynote pitch deck meeting")
    ];
}

/// <summary>
/// Defines a file category with its description for semantic matching.
/// </summary>
public sealed record CategoryDefinition(
    string Name, 
    string Description, 
    bool IsImageCategory = false);
