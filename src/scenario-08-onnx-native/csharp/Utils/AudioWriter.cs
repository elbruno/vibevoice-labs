// =============================================================================
// AudioWriter — WAV File Writer (24kHz, 16-bit PCM)
// =============================================================================
// Writes float audio samples to a standard WAV file with proper RIFF headers.
// No external dependencies — uses raw binary writing.
// =============================================================================

namespace VoiceLabs.OnnxNative.Utils;

/// <summary>
/// Writes audio samples to WAV files (RIFF format, 16-bit PCM).
/// </summary>
public static class AudioWriter
{
    /// <summary>
    /// Saves float audio samples as a 16-bit PCM WAV file.
    /// </summary>
    /// <param name="path">Output file path.</param>
    /// <param name="samples">Audio samples normalized to [-1.0, 1.0].</param>
    /// <param name="sampleRate">Sample rate in Hz (default: 24000 for VibeVoice).</param>
    /// <param name="channels">Number of audio channels (default: 1 = mono).</param>
    /// <exception cref="ArgumentException">Thrown when samples array is empty.</exception>
    public static void SaveWav(string path, float[] samples, int sampleRate = 24000, int channels = 1)
    {
        ArgumentNullException.ThrowIfNull(samples);
        ArgumentNullException.ThrowIfNull(path);

        if (samples.Length == 0)
            throw new ArgumentException("Audio samples array is empty.", nameof(samples));

        const int bitsPerSample = 16;
        int byteRate = sampleRate * channels * bitsPerSample / 8;
        int blockAlign = channels * bitsPerSample / 8;
        int dataSize = samples.Length * blockAlign;

        // Ensure output directory exists
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        using var stream = new FileStream(path, FileMode.Create, FileAccess.Write);
        using var writer = new BinaryWriter(stream);

        // RIFF header
        writer.Write("RIFF"u8);
        writer.Write(36 + dataSize);    // File size - 8 bytes
        writer.Write("WAVE"u8);

        // fmt subchunk
        writer.Write("fmt "u8);
        writer.Write(16);               // Subchunk1 size (PCM = 16)
        writer.Write((short)1);         // Audio format (1 = PCM)
        writer.Write((short)channels);  // Number of channels
        writer.Write(sampleRate);       // Sample rate
        writer.Write(byteRate);         // Byte rate
        writer.Write((short)blockAlign);// Block align
        writer.Write((short)bitsPerSample); // Bits per sample

        // data subchunk
        writer.Write("data"u8);
        writer.Write(dataSize);         // Data size

        // Convert float [-1, 1] to 16-bit PCM and write
        for (int i = 0; i < samples.Length; i++)
        {
            float clamped = Math.Clamp(samples[i], -1.0f, 1.0f);
            short pcmSample = (short)(clamped * short.MaxValue);
            writer.Write(pcmSample);
        }
    }
}
