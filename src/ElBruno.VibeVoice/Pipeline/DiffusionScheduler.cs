namespace ElBruno.VibeVoice.Pipeline;

/// <summary>
/// DDPM noise scheduler for the diffusion denoising loop.
/// </summary>
internal sealed class DiffusionScheduler
{
    private readonly int _numTrainTimesteps;
    private readonly float[] _betas;
    private readonly float[] _alphas;
    private readonly float[] _alphasCumprod;
    private readonly int[] _timesteps;

    public const float BetaStart = 0.00085f;
    public const float BetaEnd = 0.012f;
    public const int DefaultTrainTimesteps = 1000;

    public DiffusionScheduler(int numInferenceSteps = 5, int numTrainTimesteps = DefaultTrainTimesteps)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(numInferenceSteps, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(numTrainTimesteps, 1);

        _numTrainTimesteps = numTrainTimesteps;

        _betas = new float[numTrainTimesteps];
        for (int i = 0; i < numTrainTimesteps; i++)
            _betas[i] = BetaStart + (BetaEnd - BetaStart) * i / (numTrainTimesteps - 1);

        _alphas = new float[numTrainTimesteps];
        for (int i = 0; i < numTrainTimesteps; i++)
            _alphas[i] = 1.0f - _betas[i];

        _alphasCumprod = new float[numTrainTimesteps];
        _alphasCumprod[0] = _alphas[0];
        for (int i = 1; i < numTrainTimesteps; i++)
            _alphasCumprod[i] = _alphasCumprod[i - 1] * _alphas[i];

        _timesteps = ComputeTimesteps(numInferenceSteps, numTrainTimesteps);
    }

    public int[] GetTimesteps() => (int[])_timesteps.Clone();

    public float[] Step(float[] modelOutput, int timestep, float[] sample)
    {
        ArgumentNullException.ThrowIfNull(modelOutput);
        ArgumentNullException.ThrowIfNull(sample);
        if (modelOutput.Length != sample.Length)
            throw new ArgumentException($"Model output length ({modelOutput.Length}) must match sample length ({sample.Length}).");

        int t = Math.Clamp(timestep, 0, _numTrainTimesteps - 1);
        float alphaCumprodT = _alphasCumprod[t];
        float alphaCumprodTPrev = t > 0 ? _alphasCumprod[t - 1] : 1.0f;

        float sqrtAlphaCumprod = MathF.Sqrt(alphaCumprodT);
        float sqrtOneMinusAlphaCumprod = MathF.Sqrt(1.0f - alphaCumprodT);

        float[] x0Pred = new float[sample.Length];
        for (int i = 0; i < sample.Length; i++)
        {
            x0Pred[i] = (sample[i] - sqrtOneMinusAlphaCumprod * modelOutput[i]) / sqrtAlphaCumprod;
            x0Pred[i] = Math.Clamp(x0Pred[i], -1.0f, 1.0f);
        }

        float sqrtAlphaCumprodPrev = MathF.Sqrt(alphaCumprodTPrev);
        float sqrtOneMinusAlphaCumprodPrev = MathF.Sqrt(1.0f - alphaCumprodTPrev);

        float[] result = new float[sample.Length];
        for (int i = 0; i < sample.Length; i++)
        {
            result[i] = sqrtAlphaCumprodPrev * x0Pred[i]
                       + sqrtOneMinusAlphaCumprodPrev * modelOutput[i];
        }

        return result;
    }

    public float GetAlphaCumprod(int timestep)
    {
        int t = Math.Clamp(timestep, 0, _numTrainTimesteps - 1);
        return _alphasCumprod[t];
    }

    private static int[] ComputeTimesteps(int numInferenceSteps, int numTrainTimesteps)
    {
        var timesteps = new int[numInferenceSteps];
        float stepSize = (float)numTrainTimesteps / numInferenceSteps;
        for (int i = 0; i < numInferenceSteps; i++)
        {
            timesteps[i] = (int)Math.Round(numTrainTimesteps - 1 - i * stepSize);
            timesteps[i] = Math.Max(timesteps[i], 0);
        }
        return timesteps;
    }
}
