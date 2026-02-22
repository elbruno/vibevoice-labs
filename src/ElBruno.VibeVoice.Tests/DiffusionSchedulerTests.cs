using ElBruno.VibeVoice.Pipeline;

namespace ElBruno.VibeVoice.Tests;

public class DiffusionSchedulerTests
{
    [Fact]
    public void GetTimesteps_Returns20Steps_ByDefault()
    {
        var scheduler = new DiffusionScheduler(20);
        int[] timesteps = scheduler.GetTimesteps();
        Assert.Equal(20, timesteps.Length);
    }

    [Fact]
    public void GetTimesteps_StartsFromHighAndDecreases()
    {
        var scheduler = new DiffusionScheduler(20);
        int[] timesteps = scheduler.GetTimesteps();
        Assert.Equal(999, timesteps[0]);
        Assert.True(timesteps[0] > timesteps[^1]);
        // Each timestep should be decreasing
        for (int i = 1; i < timesteps.Length; i++)
            Assert.True(timesteps[i] < timesteps[i - 1], $"Timestep {i} ({timesteps[i]}) should be < timestep {i - 1} ({timesteps[i - 1]})");
    }

    [Fact]
    public void GetTimesteps_MatchesExpectedValues()
    {
        var scheduler = new DiffusionScheduler(20);
        int[] timesteps = scheduler.GetTimesteps();
        // Expected from actual VibeVoice model
        int[] expected = [999, 949, 899, 849, 799, 749, 699, 649, 599, 549,
                          500, 450, 400, 350, 300, 250, 200, 150, 100, 50];
        Assert.Equal(expected, timesteps);
    }

    [Fact]
    public void Step_ProducesSameLengthOutput()
    {
        var scheduler = new DiffusionScheduler(5);
        int[] timesteps = scheduler.GetTimesteps();
        float[] sample = new float[64];
        float[] modelOutput = new float[64];

        // Fill with some values
        var rng = new Random(42);
        for (int i = 0; i < 64; i++)
        {
            sample[i] = (float)(rng.NextDouble() * 2 - 1);
            modelOutput[i] = (float)(rng.NextDouble() * 2 - 1);
        }

        float[] result = scheduler.Step(modelOutput, timesteps[0], sample);
        Assert.Equal(64, result.Length);
    }

    [Fact]
    public void Step_ProducesFiniteValues()
    {
        var scheduler = new DiffusionScheduler(20);
        int[] timesteps = scheduler.GetTimesteps();
        float[] sample = new float[64];
        float[] modelOutput = new float[64];

        var rng = new Random(42);
        for (int i = 0; i < 64; i++)
        {
            sample[i] = (float)(rng.NextDouble() * 2 - 1);
            modelOutput[i] = (float)(rng.NextDouble() * 0.5);
        }

        foreach (var t in timesteps)
        {
            modelOutput = new float[64];
            for (int i = 0; i < 64; i++)
                modelOutput[i] = (float)(rng.NextDouble() * 0.5);

            sample = scheduler.Step(modelOutput, t, sample);
            Assert.All(sample, v => Assert.True(float.IsFinite(v), $"Non-finite value: {v}"));
        }
    }

    [Fact]
    public void GetAlphaCumprod_ReturnsValidRange()
    {
        var scheduler = new DiffusionScheduler(20);

        // t=0 should have high alpha (low noise)
        float alpha0 = scheduler.GetAlphaCumprod(0);
        Assert.InRange(alpha0, 0.99f, 1.0f);

        // t=999 should have low alpha (high noise)
        float alpha999 = scheduler.GetAlphaCumprod(999);
        Assert.InRange(alpha999, 0.0f, 0.001f);
    }

    [Fact]
    public void Constructor_ThrowsOnInvalidSteps()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new DiffusionScheduler(0));
    }
}
