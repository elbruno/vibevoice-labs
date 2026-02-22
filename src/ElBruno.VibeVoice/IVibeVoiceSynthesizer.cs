namespace ElBruno.VibeVoice;

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
    /// Returns the names of available voice presets.
    /// </summary>
    string[] GetAvailableVoices();
}
