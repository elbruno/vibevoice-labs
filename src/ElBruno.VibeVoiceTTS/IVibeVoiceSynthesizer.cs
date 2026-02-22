namespace ElBruno.VibeVoiceTTS;

/// <summary>
/// Interface for VibeVoice text-to-speech synthesis.
/// </summary>
public interface IVibeVoiceSynthesizer : IDisposable
{
    /// <summary>
    /// Gets whether all required ONNX model files are present at the configured model path.
    /// </summary>
    bool IsModelAvailable { get; }

    /// <summary>
    /// Gets the effective model path (configured or default cache location).
    /// </summary>
    string ModelPath { get; }

    /// <summary>
    /// Ensures model files are available, downloading from HuggingFace if needed.
    /// </summary>
    Task EnsureModelAvailableAsync(
        IProgress<DownloadProgress>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates audio samples from text using the specified voice preset.
    /// </summary>
    /// <returns>Float array of audio samples at 24kHz, normalized to [-1, 1].</returns>
    Task<float[]> GenerateAudioAsync(
        string text,
        VibeVoicePreset voice,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates audio samples from text using a voice name string.
    /// </summary>
    Task<float[]> GenerateAudioAsync(
        string text,
        string voiceName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves audio samples to a WAV file (24kHz, 16-bit PCM).
    /// </summary>
    void SaveWav(string path, float[] audioSamples);

    /// <summary>
    /// Returns the friendly names of voice presets currently downloaded on disk (e.g. "Carter", "Emma").
    /// These names can be used directly with GenerateAudioAsync.
    /// Use GetSupportedVoices() to see all voices that can be downloaded on demand.
    /// </summary>
    string[] GetAvailableVoices();

    /// <summary>
    /// Returns detailed information about voice presets currently downloaded on disk.
    /// </summary>
    VoiceInfo[] GetAvailableVoiceDetails();

    /// <summary>
    /// Returns the friendly names of all supported voice presets, including those not yet downloaded.
    /// Voices not on disk will be auto-downloaded when first used with GenerateAudioAsync.
    /// </summary>
    string[] GetSupportedVoices();

    /// <summary>
    /// Returns detailed information about all supported voice presets, including those not yet downloaded.
    /// </summary>
    VoiceInfo[] GetSupportedVoiceDetails();

    /// <summary>
    /// Downloads a specific voice preset if not already available.
    /// Accepts both short names ("Davis") and internal names ("en-Davis_man").
    /// </summary>
    Task EnsureVoiceAvailableAsync(
        string voiceName,
        IProgress<DownloadProgress>? progress = null,
        CancellationToken cancellationToken = default);
}
