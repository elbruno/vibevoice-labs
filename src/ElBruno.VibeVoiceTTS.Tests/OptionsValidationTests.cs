using ElBruno.VibeVoiceTTS;

namespace ElBruno.VibeVoiceTTS.Tests;

public class OptionsValidationTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public void DiffusionSteps_RejectsInvalidValues(int value)
    {
        var opts = new VibeVoiceOptions();
        Assert.Throws<ArgumentOutOfRangeException>(() => opts.DiffusionSteps = value);
    }

    [Fact]
    public void DiffusionSteps_AcceptsValidValues()
    {
        var opts = new VibeVoiceOptions { DiffusionSteps = 1 };
        Assert.Equal(1, opts.DiffusionSteps);
        opts.DiffusionSteps = 50;
        Assert.Equal(50, opts.DiffusionSteps);
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(-1f)]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    public void CfgScale_RejectsInvalidValues(float value)
    {
        var opts = new VibeVoiceOptions();
        Assert.Throws<ArgumentOutOfRangeException>(() => opts.CfgScale = value);
    }

    [Fact]
    public void CfgScale_AcceptsValidValues()
    {
        var opts = new VibeVoiceOptions { CfgScale = 0.1f };
        Assert.Equal(0.1f, opts.CfgScale);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void SampleRate_RejectsInvalidValues(int value)
    {
        var opts = new VibeVoiceOptions();
        Assert.Throws<ArgumentOutOfRangeException>(() => opts.SampleRate = value);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("no-slash")]
    [InlineData("has spaces/repo")]
    [InlineData("owner/repo/extra")]
    [InlineData("../traversal/attack")]
    [InlineData("https://evil.com/repo")]
    public void HuggingFaceRepo_RejectsInvalidFormats(string value)
    {
        var opts = new VibeVoiceOptions();
        Assert.ThrowsAny<ArgumentException>(() => opts.HuggingFaceRepo = value);
    }

    [Theory]
    [InlineData("elbruno/VibeVoice-Realtime-0.5B-ONNX")]
    [InlineData("org/repo")]
    [InlineData("user_name/repo.name")]
    public void HuggingFaceRepo_AcceptsValidFormats(string value)
    {
        var opts = new VibeVoiceOptions { HuggingFaceRepo = value };
        Assert.Equal(value, opts.HuggingFaceRepo);
    }
}
