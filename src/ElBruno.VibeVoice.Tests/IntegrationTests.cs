using ElBruno.VibeVoice;

namespace ElBruno.VibeVoice.Tests;

/// <summary>
/// Integration tests that require ONNX model files to be present.
/// These tests are skipped if models are not downloaded.
/// </summary>
public class IntegrationTests
{
    private static bool ModelsAvailable =>
        ModelManager.IsModelAvailable(VibeVoiceOptions.GetDefaultModelPath());

    [SkippableFact]
    public async Task GenerateAudio_ProducesNonSilentOutput()
    {
        Skip.IfNot(ModelsAvailable, "ONNX models not available locally");

        using var tts = new VibeVoiceSynthesizer(new VibeVoiceOptions
        {
            DiffusionSteps = 5, // Fewer steps for faster test
            CfgScale = 3.0f,
        });

        float[] audio = await tts.GenerateAudioAsync("Hello world", VibeVoicePreset.Carter);

        Assert.NotNull(audio);
        Assert.True(audio.Length > 0, "Audio should have samples");

        // Check audio is not silent: RMS > threshold
        double rms = Math.Sqrt(audio.Select(x => (double)x * x).Average());
        Assert.True(rms > 0.001, $"Audio RMS ({rms:F6}) should be > 0.001 (not silent)");
    }

    [SkippableFact]
    public async Task GenerateAudio_DifferentVoices_ProduceSamples()
    {
        Skip.IfNot(ModelsAvailable, "ONNX models not available locally");

        using var tts = new VibeVoiceSynthesizer(new VibeVoiceOptions { DiffusionSteps = 5 });

        float[] audio1 = await tts.GenerateAudioAsync("Test", "Carter");
        float[] audio2 = await tts.GenerateAudioAsync("Test", "Emma");

        Assert.True(audio1.Length > 0);
        Assert.True(audio2.Length > 0);
    }

    [SkippableFact]
    public async Task SaveWav_CreatesPlayableFile()
    {
        Skip.IfNot(ModelsAvailable, "ONNX models not available locally");

        using var tts = new VibeVoiceSynthesizer(new VibeVoiceOptions { DiffusionSteps = 5 });
        float[] audio = await tts.GenerateAudioAsync("Test", "Carter");

        var tempFile = Path.GetTempFileName() + ".wav";
        try
        {
            tts.SaveWav(tempFile, audio);
            Assert.True(File.Exists(tempFile));

            var fileInfo = new FileInfo(tempFile);
            Assert.True(fileInfo.Length > 44, "WAV file should have header + data");
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [SkippableFact]
    public async Task EnsureModelAvailable_ReportsProgress()
    {
        Skip.IfNot(ModelsAvailable, "ONNX models not available locally");

        using var tts = new VibeVoiceSynthesizer();
        bool gotReport = false;
        var progress = new Progress<DownloadProgress>(p => gotReport = true);

        await tts.EnsureModelAvailableAsync(progress);
        // Give the Progress<T> callback time to fire on the thread pool
        await Task.Delay(200);

        Assert.True(gotReport, "Should receive at least one progress report");
    }
}
