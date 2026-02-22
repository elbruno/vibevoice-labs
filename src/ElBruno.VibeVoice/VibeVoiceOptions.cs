namespace ElBruno.VibeVoice;

/// <summary>
/// Configuration options for the VibeVoice synthesizer.
/// </summary>
public sealed class VibeVoiceOptions
{
    /// <summary>
    /// Local directory containing ONNX model files. If null, uses the shared default cache location.
    /// </summary>
    public string? ModelPath { get; set; }

    /// <summary>
    /// HuggingFace repository to download models from.
    /// </summary>
    public string HuggingFaceRepo { get; set; } = "elbruno/VibeVoice-Realtime-0.5B-ONNX";

    /// <summary>
    /// Number of DDPM diffusion steps (default: 20, matching model config).
    /// </summary>
    public int DiffusionSteps { get; set; } = 20;

    /// <summary>
    /// Classifier-free guidance scale (default: 1.5).
    /// </summary>
    public float CfgScale { get; set; } = 1.5f;

    /// <summary>
    /// Audio sample rate in Hz (default: 24000).
    /// </summary>
    public int SampleRate { get; set; } = 24000;

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
}
