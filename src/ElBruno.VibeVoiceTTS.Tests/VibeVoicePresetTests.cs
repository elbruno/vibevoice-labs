using ElBruno.VibeVoiceTTS;

namespace ElBruno.VibeVoiceTTS.Tests;

public class VibeVoicePresetTests
{
    [Theory]
    [InlineData(VibeVoicePreset.Carter, "en-Carter_man")]
    [InlineData(VibeVoicePreset.Davis, "en-Davis_man")]
    [InlineData(VibeVoicePreset.Emma, "en-Emma_woman")]
    [InlineData(VibeVoicePreset.Frank, "en-Frank_man")]
    [InlineData(VibeVoicePreset.Grace, "en-Grace_woman")]
    [InlineData(VibeVoicePreset.Mike, "en-Mike_man")]
    public void ToVoiceName_ReturnsExpectedName(VibeVoicePreset preset, string expected)
    {
        Assert.Equal(expected, preset.ToVoiceName());
    }

    [Fact]
    public void AllPresets_AreDefined()
    {
        var presets = Enum.GetValues<VibeVoicePreset>();
        Assert.True(presets.Length >= 6, "Should have at least 6 voice presets");
    }
}
