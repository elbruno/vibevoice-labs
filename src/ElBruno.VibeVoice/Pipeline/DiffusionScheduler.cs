namespace ElBruno.VibeVoice.Pipeline;

/// <summary>
/// DPM-Solver++ multistep scheduler (midpoint, order 2, v_prediction).
/// Matches VibeVoice's DPMSolverMultistepScheduler configuration exactly.
/// </summary>
internal sealed class DiffusionScheduler
{
    private readonly int _numTrainTimesteps;
    private readonly int[] _timesteps;
    private readonly float[] _alphasCumprod;
    private readonly float[] _alphaT;   // sqrt(alpha_cumprod)
    private readonly float[] _sigmaT;   // sqrt(1 - alpha_cumprod)
    private readonly float[] _lambdaT;  // log(alpha / sigma)
    private readonly List<float[]> _modelOutputs;
    private readonly List<int> _timestepList;
    private int _stepIndex;

    public const int DefaultTrainTimesteps = 1000;

    public DiffusionScheduler(
        int numInferenceSteps = 20,
        int numTrainTimesteps = DefaultTrainTimesteps)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(numInferenceSteps, 1);
        _numTrainTimesteps = numTrainTimesteps;

        // Cosine beta schedule (matching diffusers betas_for_alpha_bar)
        _alphasCumprod = ComputeCosineAlphasCumprod(numTrainTimesteps);

        // Precompute alpha, sigma, lambda for all timesteps
        _alphaT = new float[numTrainTimesteps];
        _sigmaT = new float[numTrainTimesteps];
        _lambdaT = new float[numTrainTimesteps];
        for (int i = 0; i < numTrainTimesteps; i++)
        {
            _alphaT[i] = MathF.Sqrt(_alphasCumprod[i]);
            _sigmaT[i] = MathF.Sqrt(1.0f - _alphasCumprod[i]);
            _lambdaT[i] = MathF.Log(_alphaT[i] / Math.Max(_sigmaT[i], 1e-10f));
        }

        // Timesteps: linspace(0, T-1, N+1).round()[1:].reverse()
        _timesteps = ComputeTimesteps(numInferenceSteps, numTrainTimesteps);

        // Multi-step state
        _modelOutputs = new List<float[]>();
        _timestepList = new List<int>();
        _stepIndex = 0;
    }

    private static float[] ComputeCosineAlphasCumprod(int numTrainTimesteps, float maxBeta = 0.999f)
    {
        // Matches diffusers betas_for_alpha_bar
        var alphasCumprod = new float[numTrainTimesteps];
        float AlphaBar(float t) => MathF.Pow(MathF.Cos((t + 0.008f) / 1.008f * MathF.PI / 2.0f), 2);

        float cumprod = 1.0f;
        for (int i = 0; i < numTrainTimesteps; i++)
        {
            float t1 = (float)i / numTrainTimesteps;
            float t2 = (float)(i + 1) / numTrainTimesteps;
            float beta = Math.Min(1.0f - AlphaBar(t2) / AlphaBar(t1), maxBeta);
            cumprod *= (1.0f - beta);
            alphasCumprod[i] = cumprod;
        }
        return alphasCumprod;
    }

    private static int[] ComputeTimesteps(int numInferenceSteps, int numTrainTimesteps)
    {
        // linspace(0, T-1, N+1).round()[1:].flip()
        int n = numInferenceSteps + 1;
        var raw = new float[n];
        for (int i = 0; i < n; i++)
            raw[i] = (float)i * (numTrainTimesteps - 1) / (n - 1);

        var timesteps = new int[numInferenceSteps];
        for (int i = 0; i < numInferenceSteps; i++)
            timesteps[i] = (int)MathF.Round(raw[numInferenceSteps - i]);

        return timesteps;
    }

    public int[] GetTimesteps() => (int[])_timesteps.Clone();

    /// <summary>
    /// Performs one denoising step using DPM-Solver++ (midpoint, order 2).
    /// modelOutput is v-prediction from the prediction_head model.
    /// </summary>
    public float[] Step(float[] modelOutput, int timestep, float[] sample)
    {
        // 1. Convert v-prediction to x0 prediction
        float alphaT = _alphaT[timestep];
        float sigmaT = _sigmaT[timestep];
        float[] x0Pred = new float[sample.Length];
        for (int i = 0; i < sample.Length; i++)
            x0Pred[i] = alphaT * sample[i] - sigmaT * modelOutput[i];

        // 2. Store for multi-step
        _modelOutputs.Add((float[])x0Pred.Clone());
        _timestepList.Add(timestep);

        // 3. Get the "previous" timestep (next in denoising sequence)
        int prevTimestep = _stepIndex < _timesteps.Length - 1
            ? _timesteps[_stepIndex + 1]
            : 0;

        // 4. Determine solver order for this step
        // Need at least 2 stored outputs for second order; use first order for final step
        bool lowerOrderFinal = _stepIndex == _timesteps.Length - 1;
        int order = (_modelOutputs.Count < 2 || lowerOrderFinal) ? 1 : 2;

        float[] result;
        if (order == 1)
        {
            result = DpmSolverFirstOrderUpdate(x0Pred, timestep, prevTimestep, sample);
        }
        else
        {
            result = DpmSolverSecondOrderUpdate(
                _modelOutputs[^2], _modelOutputs[^1],
                _timestepList[^2], timestep, prevTimestep, sample);
        }

        _stepIndex++;
        return result;
    }

    private float[] DpmSolverFirstOrderUpdate(float[] x0Pred, int timestep, int prevTimestep, float[] sample)
    {
        float lambdaT = prevTimestep >= 0 && prevTimestep < _numTrainTimesteps
            ? _lambdaT[prevTimestep] : _lambdaT[0];
        float lambdaS = _lambdaT[timestep];
        float alphaT = prevTimestep >= 0 && prevTimestep < _numTrainTimesteps
            ? _alphaT[prevTimestep] : 1.0f;
        float sigmaT = prevTimestep >= 0 && prevTimestep < _numTrainTimesteps
            ? _sigmaT[prevTimestep] : 0.0f;
        float sigmaS = _sigmaT[timestep];

        float h = lambdaT - lambdaS;
        float coeff1 = sigmaS > 1e-10f ? sigmaT / sigmaS : 0.0f;
        float coeff2 = alphaT * (MathF.Exp(-h) - 1.0f);

        float[] result = new float[sample.Length];
        for (int i = 0; i < sample.Length; i++)
            result[i] = coeff1 * sample[i] - coeff2 * x0Pred[i];

        return result;
    }

    private float[] DpmSolverSecondOrderUpdate(
        float[] x0Prev, float[] x0Cur,
        int timestepPrev, int timestepCur, int prevTimestep,
        float[] sample)
    {
        float lambdaT = prevTimestep >= 0 && prevTimestep < _numTrainTimesteps
            ? _lambdaT[prevTimestep] : _lambdaT[0];
        float lambdaS0 = _lambdaT[timestepCur];
        float lambdaS1 = _lambdaT[timestepPrev];
        float alphaT = prevTimestep >= 0 && prevTimestep < _numTrainTimesteps
            ? _alphaT[prevTimestep] : 1.0f;
        float sigmaT = prevTimestep >= 0 && prevTimestep < _numTrainTimesteps
            ? _sigmaT[prevTimestep] : 0.0f;
        float sigmaS0 = _sigmaT[timestepCur];

        float h = lambdaT - lambdaS0;
        float h0 = lambdaS0 - lambdaS1;
        float r0 = h0 / h;

        float coeff1 = sigmaS0 > 1e-10f ? sigmaT / sigmaS0 : 0.0f;
        float coeff2 = alphaT * (MathF.Exp(-h) - 1.0f);

        float[] result = new float[sample.Length];
        for (int i = 0; i < sample.Length; i++)
        {
            float d0 = x0Cur[i];
            float d1 = (1.0f / r0) * (x0Cur[i] - x0Prev[i]);
            result[i] = coeff1 * sample[i] - coeff2 * d0 - 0.5f * coeff2 * d1;
        }

        return result;
    }

    public float GetAlphaCumprod(int timestep)
    {
        int t = Math.Clamp(timestep, 0, _numTrainTimesteps - 1);
        return _alphasCumprod[t];
    }
}
