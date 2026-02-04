namespace AutoDrop.Services.AI.Local;

/// <summary>
/// Status of the local AI model download and availability.
/// </summary>
public sealed class LocalModelStatus
{
    /// <summary>
    /// Whether all required models are downloaded and ready.
    /// </summary>
    public bool IsReady { get; init; }

    /// <summary>
    /// Whether a download is currently in progress.
    /// </summary>
    public bool IsDownloading { get; init; }

    /// <summary>
    /// Download progress (0.0 - 1.0).
    /// </summary>
    public double DownloadProgress { get; init; }

    /// <summary>
    /// Current download status message.
    /// </summary>
    public string StatusMessage { get; init; } = string.Empty;

    /// <summary>
    /// Total size of models in bytes (when downloaded).
    /// </summary>
    public long TotalModelSize { get; init; }

    /// <summary>
    /// Error message if download failed.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Whether an error occurred.
    /// </summary>
    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

    /// <summary>
    /// Creates a ready status.
    /// </summary>
    public static LocalModelStatus Ready(long modelSize) => new()
    {
        IsReady = true,
        IsDownloading = false,
        DownloadProgress = 1.0,
        StatusMessage = "Models ready",
        TotalModelSize = modelSize
    };

    /// <summary>
    /// Creates a not ready status (models need download).
    /// </summary>
    public static LocalModelStatus NotReady() => new()
    {
        IsReady = false,
        IsDownloading = false,
        DownloadProgress = 0,
        StatusMessage = "Models not downloaded"
    };

    /// <summary>
    /// Creates a downloading status.
    /// </summary>
    public static LocalModelStatus Downloading(double progress, string message) => new()
    {
        IsReady = false,
        IsDownloading = true,
        DownloadProgress = progress,
        StatusMessage = message
    };

    /// <summary>
    /// Creates an error status.
    /// </summary>
    public static LocalModelStatus Error(string error) => new()
    {
        IsReady = false,
        IsDownloading = false,
        DownloadProgress = 0,
        StatusMessage = "Download failed",
        ErrorMessage = error
    };
}
