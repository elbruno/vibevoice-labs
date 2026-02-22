using ElBruno.VibeVoiceTTS;

namespace ElBruno.VibeVoiceTTS.Tests;

public class VibeVoicePresetTests
{
    [Theory]
    [InlineData(VibeVoicePreset.Carter, "Carter")]
    [InlineData(VibeVoicePreset.Davis, "Davis")]
    [InlineData(VibeVoicePreset.Emma, "Emma")]
    [InlineData(VibeVoicePreset.Frank, "Frank")]
    [InlineData(VibeVoicePreset.Grace, "Grace")]
    [InlineData(VibeVoicePreset.Mike, "Mike")]
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
