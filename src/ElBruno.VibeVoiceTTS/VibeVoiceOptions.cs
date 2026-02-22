namespace ElBruno.VibeVoiceTTS;

/// <summary>
/// Configuration options for the VibeVoice synthesizer.
/// </summary>
public sealed class VibeVoiceOptions
{
    /// <summary>
    /// Local directory containing ONNX model files. If null, uses the shared default cache location.
    /// </summary>
    public string? ModelPath { get; set; }

    private string _huggingFaceRepo = "elbruno/VibeVoice-Realtime-0.5B-ONNX";

    /// <summary>
    /// HuggingFace repository to download models from. Must be in "owner/repo" format.
    /// </summary>
    public string HuggingFaceRepo
    {
        get => _huggingFaceRepo;
        set
        {
            ValidateHuggingFaceRepo(value);
            _huggingFaceRepo = value;
        }
    }

    private int _diffusionSteps = 20;

    /// <summary>
    /// Number of DDPM diffusion steps (default: 20). Must be greater than 0.
    /// </summary>
    public int DiffusionSteps
    {
        get => _diffusionSteps;
        set
        {
            ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(value, 0, nameof(DiffusionSteps));
            _diffusionSteps = value;
        }
    }

    private float _cfgScale = 1.5f;

    /// <summary>
    /// Classifier-free guidance scale (default: 1.5). Must be greater than 0.
    /// </summary>
    public float CfgScale
    {
        get => _cfgScale;
        set
        {
            if (value <= 0f || float.IsNaN(value) || float.IsInfinity(value))
                throw new ArgumentOutOfRangeException(nameof(CfgScale), value, "CfgScale must be a positive finite number.");
            _cfgScale = value;
        }
    }

    private int _sampleRate = 24000;

    /// <summary>
    /// Audio sample rate in Hz (default: 24000). Must be greater than 0.
    /// </summary>
    public int SampleRate
    {
        get => _sampleRate;
        set
        {
            ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(value, 0, nameof(SampleRate));
            _sampleRate = value;
        }
    }

    /// <summary>
    /// Random seed for reproducible diffusion noise (default: 42).
    /// </summary>
    public int Seed { get; set; } = 42;

    /// <summary>
    /// Returns the effective model path, falling back to the OS-specific shared cache.
    /// </summary>
    public string GetEffectiveModelPath()
    {
        if (!string.IsNullOrWhiteSpace(ModelPath))
            return ModelPath;

        return GetDefaultModelPath();
    }

    /// <summary>
    /// Returns the OS-specific default cache directory for VibeVoice models.
    /// Windows: %LOCALAPPDATA%\ElBruno\VibeVoice\models
    /// Linux/macOS: ~/.local/share/elbruno/vibevoice/models
    /// </summary>
    public static string GetDefaultModelPath()
    {
        if (OperatingSystem.IsWindows())
        {
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return Path.Combine(localAppData, "ElBruno", "VibeVoice", "models");
        }
        else
        {
            var xdgData = Environment.GetEnvironmentVariable("XDG_DATA_HOME")
                          ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "share");
            return Path.Combine(xdgData, "elbruno", "vibevoice", "models");
        }
    }

    /// <summary>
    /// Validates that a HuggingFace repository string is in safe "owner/repo" format.
    /// </summary>
    internal static void ValidateHuggingFaceRepo(string repo)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repo, nameof(repo));
        if (!System.Text.RegularExpressions.Regex.IsMatch(repo, @"^[a-zA-Z0-9_.-]+/[a-zA-Z0-9_.-]+$"))
            throw new ArgumentException(
                $"Invalid HuggingFace repo format: '{repo}'. Expected 'owner/repo-name' (alphanumeric, dots, hyphens, underscores only).",
                nameof(repo));
    }
}
