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
    public void GetAvailableVoices_ReturnsInternalVoiceNames()
    {
        using var tts = new VibeVoiceSynthesizer();
        string[] voices = tts.GetAvailableVoices();
        Assert.NotEmpty(voices);
        Assert.Contains("en-Carter_man", voices);
        Assert.Contains("en-Emma_woman", voices);
    }

    [Theory]
    [InlineData("Carter", "en-Carter_man")]
    [InlineData("carter", "en-Carter_man")]
    [InlineData("Emma", "en-Emma_woman")]
    [InlineData("Davis", "en-Davis_man")]
    [InlineData("Frank", "en-Frank_man")]
    [InlineData("Grace", "en-Grace_woman")]
    [InlineData("Mike", "en-Mike_man")]
    public void ResolveVoiceName_MapsShortNamesToInternalNames(string input, string expected)
    {
        Assert.Equal(expected, VibeVoiceSynthesizer.ResolveVoiceName(input));
    }

    [Theory]
    [InlineData("en-Carter_man")]
    [InlineData("en-Emma_woman")]
    [InlineData("custom-voice")]
    public void ResolveVoiceName_PassesThroughInternalNames(string input)
    {
        Assert.Equal(input, VibeVoiceSynthesizer.ResolveVoiceName(input));
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
