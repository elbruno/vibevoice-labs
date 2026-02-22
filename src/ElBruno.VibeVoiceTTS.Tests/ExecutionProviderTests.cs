using ElBruno.VibeVoiceTTS;
using ElBruno.VibeVoiceTTS.Pipeline;
using Microsoft.ML.OnnxRuntime;

namespace ElBruno.VibeVoiceTTS.Tests;

public class ExecutionProviderTests
{
    [Fact]
    public void Default_ExecutionProvider_IsCpu()
    {
        var opts = new VibeVoiceOptions();
        Assert.Equal(ExecutionProvider.Cpu, opts.ExecutionProvider);
    }

    [Fact]
    public void Default_GpuDeviceId_IsZero()
    {
        var opts = new VibeVoiceOptions();
        Assert.Equal(0, opts.GpuDeviceId);
    }

    [Fact]
    public void GpuDeviceId_AcceptsZero()
    {
        var opts = new VibeVoiceOptions { GpuDeviceId = 0 };
        Assert.Equal(0, opts.GpuDeviceId);
    }

    [Fact]
    public void GpuDeviceId_AcceptsPositive()
    {
        var opts = new VibeVoiceOptions { GpuDeviceId = 3 };
        Assert.Equal(3, opts.GpuDeviceId);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(-100)]
    public void GpuDeviceId_RejectsNegative(int value)
    {
        var opts = new VibeVoiceOptions();
        Assert.Throws<ArgumentOutOfRangeException>(() => opts.GpuDeviceId = value);
    }

    [Fact]
    public void ExecutionProvider_CanSetDirectML()
    {
        var opts = new VibeVoiceOptions { ExecutionProvider = ExecutionProvider.DirectML };
        Assert.Equal(ExecutionProvider.DirectML, opts.ExecutionProvider);
    }

    [Fact]
    public void ExecutionProvider_CanSetCuda()
    {
        var opts = new VibeVoiceOptions { ExecutionProvider = ExecutionProvider.Cuda };
        Assert.Equal(ExecutionProvider.Cuda, opts.ExecutionProvider);
    }

    [Fact]
    public void ConfigureExecutionProvider_Cpu_DoesNotThrow()
    {
        using var sessionOptions = new SessionOptions();
        // Should be a no-op for CPU
        OnnxInferencePipeline.ConfigureExecutionProvider(sessionOptions, ExecutionProvider.Cpu, 0);
    }

    [Fact]
    public void ConfigureExecutionProvider_DirectML_FallsBackGracefully()
    {
        // On a machine without Microsoft.ML.OnnxRuntime.DirectML, this should not throw
        using var sessionOptions = new SessionOptions();
        var ex = Record.Exception(() =>
            OnnxInferencePipeline.ConfigureExecutionProvider(sessionOptions, ExecutionProvider.DirectML, 0));
        // Either succeeds (GPU available) or silently falls back (no throw)
        Assert.Null(ex);
    }

    [Fact]
    public void ConfigureExecutionProvider_Cuda_FallsBackGracefully()
    {
        // On a machine without Microsoft.ML.OnnxRuntime.Gpu, this should not throw
        using var sessionOptions = new SessionOptions();
        var ex = Record.Exception(() =>
            OnnxInferencePipeline.ConfigureExecutionProvider(sessionOptions, ExecutionProvider.Cuda, 0));
        // Either succeeds (CUDA available) or silently falls back (no throw)
        Assert.Null(ex);
    }
}
