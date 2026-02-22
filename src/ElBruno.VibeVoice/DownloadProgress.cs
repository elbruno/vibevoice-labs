namespace ElBruno.VibeVoice;

/// <summary>
/// Represents the current stage of model download.
/// </summary>
public enum DownloadStage
{
    Checking,
    Downloading,
    Validating,
    Complete,
    Failed
}

/// <summary>
/// Reports progress during model download from HuggingFace.
/// </summary>
public sealed class DownloadProgress
{
    /// <summary>Current stage of the download process.</summary>
    public DownloadStage Stage { get; init; }

    /// <summary>Overall completion percentage (0–100).</summary>
    public double PercentComplete { get; init; }

    /// <summary>Total bytes downloaded so far across all files.</summary>
    public long BytesDownloaded { get; init; }

    /// <summary>Total bytes expected across all files (0 if unknown).</summary>
    public long TotalBytes { get; init; }

    /// <summary>Name of the file currently being downloaded, or null.</summary>
    public string? CurrentFile { get; init; }

    /// <summary>Optional message describing the current operation.</summary>
    public string? Message { get; init; }
}
