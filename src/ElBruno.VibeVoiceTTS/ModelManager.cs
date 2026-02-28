using ElBruno.HuggingFace;

namespace ElBruno.VibeVoiceTTS;

/// <summary>
/// Manages model file downloading from HuggingFace, validation, and caching.
/// </summary>
internal sealed class ModelManager
{
    /// <summary>
    /// Cross-platform invalid file name characters to prevent security issues.
    /// </summary>
    internal static readonly char[] InvalidFileNameChars = ['<', '>', ':', '"', '|', '?', '*', '\\', '/', '\0'];
    // Files required for inference (autoregressive pipeline with KV-cache)
    private static readonly string[] RequiredFiles =
    [
        "lm_with_kv.onnx",
        "lm_with_kv.onnx.data",
        "tts_lm_prefill.onnx",
        "tts_lm_prefill.onnx.data",
        "tts_lm_step.onnx",
        "tts_lm_step.onnx.data",
        "prediction_head.onnx",
        "prediction_head.onnx.data",
        "acoustic_decoder.onnx",
        "acoustic_decoder.onnx.data",
        "acoustic_connector.onnx",
        "acoustic_connector.onnx.data",
        "eos_classifier.onnx",
        "eos_classifier.onnx.data",
        "type_embeddings.npy",
        "tokenizer.json",
        "model_config.json"
    ];

    // Additional files to download (optional but recommended)
    private static readonly string[] OptionalFiles =
    [
        "config.json"
    ];

    // Default voice presets with KV-cache data (downloaded by EnsureModelAvailableAsync)
    private static readonly string[] DefaultVoiceNames = ["en-Carter_man", "en-Emma_woman"];

    // All known voice presets (can be downloaded on demand)
    internal static readonly string[] AllVoiceNames =
    [
        "en-Carter_man", "en-Davis_man", "en-Emma_woman",
        "en-Frank_man", "en-Grace_woman", "en-Mike_man"
    ];

    /// <summary>
    /// Checks whether all required model files exist in the specified directory.
    /// </summary>
    public static bool IsModelAvailable(string modelPath)
    {
        if (!Directory.Exists(modelPath))
            return false;

        return RequiredFiles.All(f => File.Exists(Path.Combine(modelPath, f)));
    }

    /// <summary>
    /// Downloads all model files from HuggingFace if not already present.
    /// </summary>
    public static async Task EnsureModelAvailableAsync(
        string modelPath,
        string huggingFaceRepo,
        IProgress<DownloadProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        using var downloader = new HuggingFaceDownloader();

        // Build list of optional files including default voice presets
        var optionalFilesList = new List<string>();
        optionalFilesList.AddRange(OptionalFiles);

        // Add voice preset KV-cache files for default voices
        foreach (var voice in DefaultVoiceNames)
            optionalFilesList.AddRange(GetVoiceFiles(voice));

        // Map local DownloadProgress to package's DownloadProgress
        IProgress<ElBruno.HuggingFace.DownloadProgress>? packageProgress = null;
        if (progress != null)
        {
            packageProgress = new Progress<ElBruno.HuggingFace.DownloadProgress>(p =>
            {
                progress.Report(new DownloadProgress
                {
                    Stage = (DownloadStage)p.Stage,
                    PercentComplete = p.PercentComplete,
                    BytesDownloaded = p.BytesDownloaded,
                    TotalBytes = p.TotalBytes,
                    CurrentFile = p.CurrentFile,
                    Message = p.Message
                });
            });
        }

        await downloader.DownloadFilesAsync(new DownloadRequest
        {
            RepoId = huggingFaceRepo,
            LocalDirectory = modelPath,
            RequiredFiles = RequiredFiles,
            OptionalFiles = [.. optionalFilesList],
            Progress = packageProgress
        }, cancellationToken);
    }

    /// <summary>
    /// Checks whether a specific voice preset is downloaded.
    /// </summary>
    internal static bool IsVoiceAvailable(string modelPath, string voiceInternalName)
    {
        var voiceDir = Path.Combine(modelPath, "voices", voiceInternalName);
        return Directory.Exists(voiceDir) && File.Exists(Path.Combine(voiceDir, "metadata.json"));
    }

    /// <summary>
    /// Downloads a single voice preset from HuggingFace if not already present.
    /// </summary>
    internal static async Task EnsureVoiceAvailableAsync(
        string modelPath,
        string huggingFaceRepo,
        string voiceInternalName,
        IProgress<DownloadProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ValidateVoicePresetName(voiceInternalName);

        using var downloader = new HuggingFaceDownloader();

        var voiceFiles = GetVoiceFiles(voiceInternalName);

        // Map local DownloadProgress to package's DownloadProgress
        IProgress<ElBruno.HuggingFace.DownloadProgress>? packageProgress = null;
        if (progress != null)
        {
            packageProgress = new Progress<ElBruno.HuggingFace.DownloadProgress>(p =>
            {
                progress.Report(new DownloadProgress
                {
                    Stage = (DownloadStage)p.Stage,
                    PercentComplete = p.PercentComplete,
                    BytesDownloaded = p.BytesDownloaded,
                    TotalBytes = p.TotalBytes,
                    CurrentFile = p.CurrentFile,
                    Message = p.Message
                });
            });
        }

        await downloader.DownloadFilesAsync(new DownloadRequest
        {
            RepoId = huggingFaceRepo,
            LocalDirectory = modelPath,
            RequiredFiles = [.. voiceFiles],
            OptionalFiles = [],
            Progress = packageProgress
        }, cancellationToken);
    }

    /// <summary>
    /// Returns the list of HuggingFace file paths for a voice preset.
    /// </summary>
    internal static List<string> GetVoiceFiles(string voiceInternalName)
    {
        var files = new List<string>();
        files.Add($"voices/{voiceInternalName}/metadata.json");
        // TTS-LM KV-cache (20 layers)
        for (int i = 0; i < 20; i++)
        {
            files.Add($"voices/{voiceInternalName}/tts_kv_key_{i}.npy");
            files.Add($"voices/{voiceInternalName}/tts_kv_value_{i}.npy");
        }
        // LM KV-cache (4 layers)
        for (int i = 0; i < 4; i++)
        {
            files.Add($"voices/{voiceInternalName}/lm_kv_key_{i}.npy");
            files.Add($"voices/{voiceInternalName}/lm_kv_value_{i}.npy");
        }
        // Negative path
        files.Add($"voices/{voiceInternalName}/negative/tts_lm_hidden.npy");
        for (int i = 0; i < 20; i++)
        {
            files.Add($"voices/{voiceInternalName}/negative/tts_kv_key_{i}.npy");
            files.Add($"voices/{voiceInternalName}/negative/tts_kv_value_{i}.npy");
        }
        // Hidden states
        files.Add($"voices/{voiceInternalName}/tts_lm_hidden.npy");
        files.Add($"voices/{voiceInternalName}/lm_hidden.npy");
        return files;
    }

    /// <summary>
    /// Validates that a voice preset name does not contain invalid file name characters.
    /// </summary>
    /// <exception cref="ArgumentException">Thrown when the name contains invalid characters.</exception>
    internal static void ValidateVoicePresetName(string voiceName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(voiceName, nameof(voiceName));

        foreach (var invalidChar in InvalidFileNameChars)
        {
            if (voiceName.Contains(invalidChar))
                throw new ArgumentException(
                    $"Voice preset name contains invalid character '{invalidChar}': '{voiceName}'",
                    nameof(voiceName));
        }
    }
}
