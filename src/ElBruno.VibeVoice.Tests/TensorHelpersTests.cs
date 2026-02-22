using ElBruno.VibeVoice.Utils;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace ElBruno.VibeVoice.Tests;

public class TensorHelpersTests
{
    [Fact]
    public void CreateTensor_1D_ReturnsCorrectShape()
    {
        float[] data = [1.0f, 2.0f, 3.0f];
        var tensor = TensorHelpers.CreateTensor(data, [3]);
        Assert.Equal(3, tensor.Length);
        Assert.Equal(1.0f, tensor[0]);
        Assert.Equal(3.0f, tensor[2]);
    }

    [Fact]
    public void CreateTensor_2D_ReturnsCorrectShape()
    {
        float[] data = [1.0f, 2.0f, 3.0f, 4.0f, 5.0f, 6.0f];
        var tensor = TensorHelpers.CreateTensor(data, [2, 3]);
        Assert.Equal(6, tensor.Length);
        Assert.Equal(2, tensor.Dimensions[0]);
        Assert.Equal(3, tensor.Dimensions[1]);
    }

    [Fact]
    public void Randn_ProducesCorrectLength()
    {
        float[] result = TensorHelpers.Randn([64], 42);
        Assert.Equal(64, result.Length);
    }

    [Fact]
    public void Randn_IsNotAllZero()
    {
        float[] result = TensorHelpers.Randn([100], 42);
        Assert.Contains(result, v => v != 0.0f);
    }

    [Fact]
    public void Randn_ApproximatelyStandardNormal()
    {
        float[] result = TensorHelpers.Randn([10000], 123);
        double mean = result.Average();
        double variance = result.Select(x => (x - mean) * (x - mean)).Average();

        // Mean should be near 0, variance near 1
        Assert.InRange(mean, -0.1, 0.1);
        Assert.InRange(variance, 0.8, 1.2);
    }

    [Fact]
    public void Randn_DeterministicWithSameSeed()
    {
        float[] a = TensorHelpers.Randn([64], 42);
        float[] b = TensorHelpers.Randn([64], 42);
        Assert.Equal(a, b);
    }

    [Fact]
    public void Randn_DifferentWithDifferentSeeds()
    {
        float[] a = TensorHelpers.Randn([64], 42);
        float[] b = TensorHelpers.Randn([64], 43);
        Assert.NotEqual(a, b);
    }
}
