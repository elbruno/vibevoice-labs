using ElBruno.VibeVoice;

namespace ElBruno.VibeVoice.Tests;

public class VibeVoiceOptionsTests
{
    [Fact]
    public void Defaults_AreCorrect()
    {
        var opts = new VibeVoiceOptions();
        Assert.Null(opts.ModelPath);
        Assert.Equal("elbruno/VibeVoice-Realtime-0.5B-ONNX", opts.HuggingFaceRepo);
        Assert.Equal(20, opts.DiffusionSteps);
        Assert.Equal(3.0f, opts.CfgScale);
        Assert.Equal(24000, opts.SampleRate);
        Assert.Equal(42, opts.Seed);
    }

    [Fact]
    public void GetEffectiveModelPath_ReturnsCustomPath_WhenSet()
    {
        var opts = new VibeVoiceOptions { ModelPath = @"C:\custom\models" };
        Assert.Equal(@"C:\custom\models", opts.GetEffectiveModelPath());
    }

    [Fact]
    public void GetEffectiveModelPath_ReturnsDefaultCache_WhenNull()
    {
        var opts = new VibeVoiceOptions();
        string path = opts.GetEffectiveModelPath();
        Assert.False(string.IsNullOrWhiteSpace(path));
        Assert.Contains("VibeVoice", path, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GetDefaultModelPath_ReturnsNonEmpty()
    {
        string path = VibeVoiceOptions.GetDefaultModelPath();
        Assert.False(string.IsNullOrWhiteSpace(path));
    }
}
