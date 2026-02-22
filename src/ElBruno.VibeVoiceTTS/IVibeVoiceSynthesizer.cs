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
    /// Returns the friendly names of available voice presets (e.g. "Carter", "Emma").
    /// These names can be used directly with the string overload of GenerateAudioAsync.
    /// </summary>
    string[] GetAvailableVoices();

    /// <summary>
    /// Returns detailed information about all available voice presets,
    /// including display name, internal name, language, and gender.
    /// </summary>
    VoiceInfo[] GetAvailableVoiceDetails();
}
