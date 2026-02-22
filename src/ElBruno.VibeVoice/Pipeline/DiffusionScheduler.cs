namespace ElBruno.VibeVoice.Pipeline;

/// <summary>
/// DDPM noise scheduler for the diffusion denoising loop.
/// </summary>
internal sealed class DiffusionScheduler
{
    private readonly int _numTrainTimesteps;
    private readonly float[] _alphasCumprod;
    private readonly int[] _timesteps;
    private readonly bool _useVPrediction;

    public const int DefaultTrainTimesteps = 1000;

    /// <param name="numInferenceSteps">Inference steps (default: 20, matching model config).</param>
    /// <param name="numTrainTimesteps">Training timesteps (default: 1000).</param>
    /// <param name="betaSchedule">Beta schedule: "cosine" or "linear".</param>
    /// <param name="predictionType">Prediction type: "v_prediction" or "epsilon".</param>
    public DiffusionScheduler(
        int numInferenceSteps = 20,
        int numTrainTimesteps = DefaultTrainTimesteps,
        string betaSchedule = "cosine",
        string predictionType = "v_prediction")
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(numInferenceSteps, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(numTrainTimesteps, 1);

        _numTrainTimesteps = numTrainTimesteps;
        _useVPrediction = predictionType == "v_prediction";

        _alphasCumprod = betaSchedule == "cosine"
            ? ComputeCosineAlphasCumprod(numTrainTimesteps)
            : ComputeLinearAlphasCumprod(numTrainTimesteps);

        _timesteps = ComputeTimesteps(numInferenceSteps, numTrainTimesteps);
    }

    private static float[] ComputeCosineAlphasCumprod(int numTrainTimesteps, float s = 0.008f)
    {
        var alphasCumprod = new float[numTrainTimesteps];
        for (int i = 0; i < numTrainTimesteps; i++)
        {
            float t = (float)i / numTrainTimesteps;
            float cos = MathF.Cos((t + s) / (1 + s) * MathF.PI / 2);
            float cos0 = MathF.Cos(s / (1 + s) * MathF.PI / 2);
            alphasCumprod[i] = Math.Clamp(cos * cos / (cos0 * cos0), 0.0001f, 0.9999f);
        }
        return alphasCumprod;
    }

    private static float[] ComputeLinearAlphasCumprod(int numTrainTimesteps,
        float betaStart = 0.00085f, float betaEnd = 0.012f)
    {
        var alphasCumprod = new float[numTrainTimesteps];
        float cumprod = 1.0f;
        for (int i = 0; i < numTrainTimesteps; i++)
        {
            float beta = betaStart + (betaEnd - betaStart) * i / (numTrainTimesteps - 1);
            cumprod *= (1.0f - beta);
            alphasCumprod[i] = cumprod;
        }
        return alphasCumprod;
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

        // Predict x0 from model output using the correct prediction type
        float[] x0Pred = new float[sample.Length];
        if (_useVPrediction)
        {
            // v-prediction: x0 = sqrt(alpha_t) * sample - sqrt(1-alpha_t) * v
            for (int i = 0; i < sample.Length; i++)
            {
                x0Pred[i] = sqrtAlphaCumprod * sample[i] - sqrtOneMinusAlphaCumprod * modelOutput[i];
                x0Pred[i] = Math.Clamp(x0Pred[i], -1.0f, 1.0f);
            }
        }
        else
        {
            // epsilon prediction: x0 = (sample - sqrt(1-alpha_t) * eps) / sqrt(alpha_t)
            for (int i = 0; i < sample.Length; i++)
            {
                x0Pred[i] = (sample[i] - sqrtOneMinusAlphaCumprod * modelOutput[i]) / sqrtAlphaCumprod;
                x0Pred[i] = Math.Clamp(x0Pred[i], -1.0f, 1.0f);
            }
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
