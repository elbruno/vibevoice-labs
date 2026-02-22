using ElBruno.VibeVoiceTTS;
using ElBruno.VibeVoiceTTS.Utils;

namespace ElBruno.VibeVoiceTTS.Tests;

public class AudioWriterTests
{
    [Fact]
    public void SaveWav_CreatesValidFile()
    {
        var tempFile = Path.GetTempFileName() + ".wav";
        try
        {
            float[] samples = new float[24000]; // 1 second of silence
            AudioWriter.SaveWav(tempFile, samples, 24000);

            Assert.True(File.Exists(tempFile));
            var fileBytes = File.ReadAllBytes(tempFile);

            // Check WAV header: RIFF....WAVE
            Assert.True(fileBytes.Length > 44);
            Assert.Equal((byte)'R', fileBytes[0]);
            Assert.Equal((byte)'I', fileBytes[1]);
            Assert.Equal((byte)'F', fileBytes[2]);
            Assert.Equal((byte)'F', fileBytes[3]);
            Assert.Equal((byte)'W', fileBytes[8]);
            Assert.Equal((byte)'A', fileBytes[9]);
            Assert.Equal((byte)'V', fileBytes[10]);
            Assert.Equal((byte)'E', fileBytes[11]);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact]
    public void SaveWav_CorrectDataSize()
    {
        var tempFile = Path.GetTempFileName() + ".wav";
        try
        {
            int numSamples = 1000;
            float[] samples = new float[numSamples];
            AudioWriter.SaveWav(tempFile, samples, 24000);

            var fileBytes = File.ReadAllBytes(tempFile);
            // 44 bytes header + numSamples * 2 (16-bit PCM)
            Assert.Equal(44 + numSamples * 2, fileBytes.Length);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }
}
