namespace ElBruno.VibeVoiceTTS.Utils;

/// <summary>
/// Writes audio samples to WAV files (RIFF format, 16-bit PCM).
/// </summary>
public static class AudioWriter
{
    /// <summary>
    /// Saves float audio samples as a 16-bit PCM WAV file.
    /// </summary>
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

        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        using var stream = new FileStream(path, FileMode.Create, FileAccess.Write);
        using var writer = new BinaryWriter(stream);

        writer.Write("RIFF"u8);
        writer.Write(36 + dataSize);
        writer.Write("WAVE"u8);

        writer.Write("fmt "u8);
        writer.Write(16);
        writer.Write((short)1);
        writer.Write((short)channels);
        writer.Write(sampleRate);
        writer.Write(byteRate);
        writer.Write((short)blockAlign);
        writer.Write((short)bitsPerSample);

        writer.Write("data"u8);
        writer.Write(dataSize);

        for (int i = 0; i < samples.Length; i++)
        {
            float clamped = Math.Clamp(samples[i], -1.0f, 1.0f);
            short pcmSample = (short)Math.Round(clamped * short.MaxValue);
            writer.Write(pcmSample);
        }
    }
}
