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

    [Theory]
    [InlineData(VibeVoicePreset.Carter, "Carter", "en-Carter_man", "en", "man")]
    [InlineData(VibeVoicePreset.Emma, "Emma", "en-Emma_woman", "en", "woman")]
    [InlineData(VibeVoicePreset.Grace, "Grace", "en-Grace_woman", "en", "woman")]
    public void ToVoiceInfo_ReturnsCorrectMetadata(VibeVoicePreset preset, string name, string internalName, string lang, string gender)
    {
        var info = preset.ToVoiceInfo();
        Assert.Equal(name, info.Name);
        Assert.Equal(internalName, info.InternalName);
        Assert.Equal(lang, info.Language);
        Assert.Equal(gender, info.Gender);
    }

    [Fact]
    public void AllPresets_HaveVoiceInfo()
    {
        foreach (var preset in Enum.GetValues<VibeVoicePreset>())
        {
            var info = preset.ToVoiceInfo();
            Assert.NotNull(info);
            Assert.False(string.IsNullOrEmpty(info.Name));
            Assert.False(string.IsNullOrEmpty(info.InternalName));
            Assert.NotEqual("unknown", info.Language);
            Assert.NotEqual("unknown", info.Gender);
        }
    }
}
