namespace AutoDrop.Core.Constants;

/// <summary>
/// Application-wide constants.
/// </summary>
public static class AppConstants
{
    /// <summary>
    /// Application name.
    /// </summary>
    public const string AppName = "AutoDrop";

    /// <summary>
    /// Application data folder name.
    /// </summary>
    public const string AppDataFolderName = "AutoDrop";

    /// <summary>
    /// Rules configuration file name.
    /// </summary>
    public const string RulesFileName = "rules.json";

    /// <summary>
    /// Application settings file name.
    /// </summary>
    public const string SettingsFileName = "settings.json";

    /// <summary>
    /// Default drop zone width.
    /// </summary>
    public const double DefaultDropZoneWidth = 150;

    /// <summary>
    /// Default drop zone height.
    /// </summary>
    public const double DefaultDropZoneHeight = 150;

    /// <summary>
    /// Toast notification display duration in milliseconds.
    /// </summary>
    public const int ToastDurationMs = 5000;

    /// <summary>
    /// Maximum number of destination suggestions to show.
    /// </summary>
    public const int MaxSuggestions = 4;
}
