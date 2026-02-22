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
    public void GetAvailableVoices_ReturnsOnlyDownloadedVoices()
    {
        using var tts = new VibeVoiceSynthesizer();
        string[] voices = tts.GetAvailableVoices();
        // Only Carter and Emma are downloaded by default
        Assert.DoesNotContain("en-Carter_man", voices); // Should be friendly names
        // Voices returned should be a subset of supported voices
        string[] supported = tts.GetSupportedVoices();
        foreach (var v in voices)
            Assert.Contains(v, supported);
    }

    [Fact]
    public void GetSupportedVoices_ReturnsAllSixPresets()
    {
        using var tts = new VibeVoiceSynthesizer();
        string[] voices = tts.GetSupportedVoices();
        Assert.Equal(6, voices.Length);
        Assert.Contains("Carter", voices);
        Assert.Contains("Davis", voices);
        Assert.Contains("Emma", voices);
        Assert.Contains("Frank", voices);
        Assert.Contains("Grace", voices);
        Assert.Contains("Mike", voices);
    }

    [Fact]
    public void GetSupportedVoiceDetails_ReturnsAllSixPresets()
    {
        using var tts = new VibeVoiceSynthesizer();
        VoiceInfo[] details = tts.GetSupportedVoiceDetails();
        Assert.Equal(6, details.Length);
    }

    [Fact]
    public void GetSupportedVoiceDetails_ContainsCorrectMetadata()
    {
        using var tts = new VibeVoiceSynthesizer();
        VoiceInfo[] details = tts.GetSupportedVoiceDetails();

        var carter = details.First(v => v.Name == "Carter");
        Assert.Equal("en-Carter_man", carter.InternalName);
        Assert.Equal("en", carter.Language);
        Assert.Equal("man", carter.Gender);

        var emma = details.First(v => v.Name == "Emma");
        Assert.Equal("en-Emma_woman", emma.InternalName);
        Assert.Equal("en", emma.Language);
        Assert.Equal("woman", emma.Gender);
    }

    [Fact]
    public void GetAvailableVoiceDetails_NamesMatchGetAvailableVoices()
    {
        using var tts = new VibeVoiceSynthesizer();
        string[] voices = tts.GetAvailableVoices();
        VoiceInfo[] details = tts.GetAvailableVoiceDetails();

        Assert.Equal(voices.Length, details.Length);
        for (int i = 0; i < voices.Length; i++)
            Assert.Equal(voices[i], details[i].Name);
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
    public void ResolveVoiceName_ResolvesInternalNamesToSame(string input)
    {
        // Internal names map back to the same internal name via preset lookup
        Assert.Equal(input, VibeVoiceSynthesizer.ResolveVoiceName(input));
    }

    [Theory]
    [InlineData("custom-voice")]
    [InlineData("fr-Marie_woman")]
    public void ResolveVoiceName_PassesThroughUnknownNames(string input)
    {
        Assert.Equal(input, VibeVoiceSynthesizer.ResolveVoiceName(input));
    }

    [Theory]
    [InlineData("en-Carter_man", "Carter")]
    [InlineData("en-Emma_woman", "Emma")]
    [InlineData("en-Mike_man", "Mike")]
    public void TryParseVoice_ParsesInternalNames(string internalName, string expectedEnumName)
    {
        Assert.True(VibeVoicePresetExtensions.TryParseVoice(internalName, out var preset));
        Assert.Equal(expectedEnumName, preset.ToString());
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
