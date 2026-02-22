using ElBruno.VibeVoiceTTS;

namespace ElBruno.VibeVoiceTTS.Tests;

public class VibeVoiceSynthesizerTests
{
    [Fact]
    public void Constructor_WithDefaultOptions_DoesNotThrow()
    {
        using var tts = new VibeVoiceSynthesizer();
        Assert.NotNull(tts);
    }

    [Fact]
    public void Constructor_WithCustomOptions_DoesNotThrow()
    {
        var opts = new VibeVoiceOptions { DiffusionSteps = 10, CfgScale = 2.0f };
        using var tts = new VibeVoiceSynthesizer(opts);
        Assert.NotNull(tts);
    }

    [Fact]
    public void Constructor_NullOptions_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new VibeVoiceSynthesizer(null!));
    }

    [Fact]
    public void ModelPath_ReturnsNonEmpty()
    {
        using var tts = new VibeVoiceSynthesizer();
        Assert.False(string.IsNullOrWhiteSpace(tts.ModelPath));
    }

    [Fact]
    public void GetAvailableVoices_ReturnsVoiceList()
    {
        using var tts = new VibeVoiceSynthesizer();
        string[] voices = tts.GetAvailableVoices();
        Assert.NotEmpty(voices);
        Assert.Contains("Carter", voices);
    }

    [Fact]
    public void Dispose_CanBeCalledMultipleTimes()
    {
        var tts = new VibeVoiceSynthesizer();
        tts.Dispose();
        tts.Dispose(); // Should not throw
    }

    [Fact]
    public async Task GenerateAudioAsync_AfterDispose_Throws()
    {
        var tts = new VibeVoiceSynthesizer();
        tts.Dispose();
        await Assert.ThrowsAsync<ObjectDisposedException>(
            () => tts.GenerateAudioAsync("test", "Carter"));
    }

    [Fact]
    public async Task GenerateAudioAsync_NullText_Throws()
    {
        using var tts = new VibeVoiceSynthesizer(new VibeVoiceOptions
        {
            ModelPath = VibeVoiceOptions.GetDefaultModelPath()
        });

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => tts.GenerateAudioAsync(null!, "Carter"));
    }

    [Fact]
    public async Task GenerateAudioAsync_EmptyText_Throws()
    {
        using var tts = new VibeVoiceSynthesizer(new VibeVoiceOptions
        {
            ModelPath = VibeVoiceOptions.GetDefaultModelPath()
        });

        await Assert.ThrowsAsync<ArgumentException>(
            () => tts.GenerateAudioAsync("", "Carter"));
    }
}
