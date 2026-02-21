// =============================================================================
// DiffusionScheduler — DDPM / DPM-Solver Noise Scheduler
// =============================================================================
// Implements the noise schedule for diffusion-based audio generation.
// Uses a linear beta schedule and supports configurable inference steps.
//
// Based on the DDPM (Denoising Diffusion Probabilistic Models) formulation
// with a DPM-Solver-inspired fast scheduling for reduced step counts.
// =============================================================================

namespace VoiceLabs.OnnxNative.Pipeline;

/// <summary>
/// Noise scheduler for the diffusion denoising loop.
/// Computes beta schedule, alpha cumulative products, and performs
/// single denoising steps.
/// </summary>
public sealed class DiffusionScheduler
{
    private readonly int _numInferenceSteps;
    private readonly int _numTrainTimesteps;
    private readonly float[] _betas;
    private readonly float[] _alphas;
    private readonly float[] _alphasCumprod;
    private readonly int[] _timesteps;

    /// <summary>Start of the linear beta schedule.</summary>
    public const float BetaStart = 0.00085f;

    /// <summary>End of the linear beta schedule.</summary>
    public const float BetaEnd = 0.012f;

    /// <summary>Number of training timesteps the model was trained with.</summary>
    public const int DefaultTrainTimesteps = 1000;

    /// <summary>
    /// Creates a new diffusion scheduler with the specified number of inference steps.
    /// </summary>
    /// <param name="numInferenceSteps">
    /// Number of denoising steps to run during inference (default: 5).
    /// More steps = higher quality but slower.
    /// </param>
    /// <param name="numTrainTimesteps">
    /// Number of timesteps the model was trained with (default: 1000).
    /// </param>
    public DiffusionScheduler(int numInferenceSteps = 5, int numTrainTimesteps = DefaultTrainTimesteps)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(numInferenceSteps, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(numTrainTimesteps, 1);

        _numInferenceSteps = numInferenceSteps;
        _numTrainTimesteps = numTrainTimesteps;

        // Compute linear beta schedule: beta_t linearly interpolated from BetaStart to BetaEnd
        _betas = new float[numTrainTimesteps];
        for (int i = 0; i < numTrainTimesteps; i++)
        {
            _betas[i] = BetaStart + (BetaEnd - BetaStart) * i / (numTrainTimesteps - 1);
        }

        // alpha_t = 1 - beta_t
        _alphas = new float[numTrainTimesteps];
        for (int i = 0; i < numTrainTimesteps; i++)
        {
            _alphas[i] = 1.0f - _betas[i];
        }

        // alpha_cumprod_t = product(alpha_0 ... alpha_t)
        _alphasCumprod = new float[numTrainTimesteps];
        _alphasCumprod[0] = _alphas[0];
        for (int i = 1; i < numTrainTimesteps; i++)
        {
            _alphasCumprod[i] = _alphasCumprod[i - 1] * _alphas[i];
        }

        // Compute evenly-spaced timesteps for inference (reversed: high → low)
        _timesteps = ComputeTimesteps(numInferenceSteps, numTrainTimesteps);
    }

    /// <summary>
    /// Returns the timestep sequence for the denoising loop (descending order).
    /// </summary>
    public int[] GetTimesteps() => (int[])_timesteps.Clone();

    /// <summary>
    /// Performs a single denoising step: given the model's noise prediction,
    /// the current timestep, and the current noisy sample, returns the
    /// partially-denoised sample.
    /// </summary>
    /// <param name="modelOutput">Predicted noise from the diffusion model.</param>
    /// <param name="timestep">Current timestep value.</param>
    /// <param name="sample">Current noisy sample (latent representation).</param>
    /// <returns>Denoised sample after removing the predicted noise.</returns>
    /// <exception cref="ArgumentException">Thrown when array lengths don't match.</exception>
    public float[] Step(float[] modelOutput, int timestep, float[] sample)
    {
        ArgumentNullException.ThrowIfNull(modelOutput);
        ArgumentNullException.ThrowIfNull(sample);

        if (modelOutput.Length != sample.Length)
            throw new ArgumentException(
                $"Model output length ({modelOutput.Length}) must match sample length ({sample.Length}).");

        int t = Math.Clamp(timestep, 0, _numTrainTimesteps - 1);

        float alphaCumprodT = _alphasCumprod[t];
        float alphaCumprodTPrev = t > 0 ? _alphasCumprod[t - 1] : 1.0f;
        float betaT = _betas[t];

        // DDPM formulation:
        // x_0_pred = (sample - sqrt(1 - alpha_cumprod_t) * noise_pred) / sqrt(alpha_cumprod_t)
        float sqrtAlphaCumprod = MathF.Sqrt(alphaCumprodT);
        float sqrtOneMinusAlphaCumprod = MathF.Sqrt(1.0f - alphaCumprodT);

        // Predicted clean sample (x_0)
        float[] x0Pred = new float[sample.Length];
        for (int i = 0; i < sample.Length; i++)
        {
            x0Pred[i] = (sample[i] - sqrtOneMinusAlphaCumprod * modelOutput[i]) / sqrtAlphaCumprod;
            x0Pred[i] = Math.Clamp(x0Pred[i], -1.0f, 1.0f); // Clip for stability
        }

        // Compute the denoised sample direction
        // x_{t-1} = sqrt(alpha_cumprod_{t-1}) * x_0_pred + sqrt(1 - alpha_cumprod_{t-1}) * noise_direction
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

    /// <summary>
    /// Returns the alpha cumulative product for a given timestep.
    /// Useful for external noise scaling calculations.
    /// </summary>
    public float GetAlphaCumprod(int timestep)
    {
        int t = Math.Clamp(timestep, 0, _numTrainTimesteps - 1);
        return _alphasCumprod[t];
    }

    // =========================================================================
    // Internal Helpers
    // =========================================================================

    /// <summary>
    /// Computes evenly-spaced timesteps for inference, in descending order.
    /// For example, with 5 steps and 1000 training steps: [999, 799, 599, 399, 199]
    /// </summary>
    private static int[] ComputeTimesteps(int numInferenceSteps, int numTrainTimesteps)
    {
        var timesteps = new int[numInferenceSteps];
        float stepSize = (float)numTrainTimesteps / numInferenceSteps;

        for (int i = 0; i < numInferenceSteps; i++)
        {
            // Reverse order: start from the noisiest timestep
            timesteps[i] = (int)Math.Round(numTrainTimesteps - 1 - i * stepSize);
            timesteps[i] = Math.Max(timesteps[i], 0);
        }

        return timesteps;
    }
}
