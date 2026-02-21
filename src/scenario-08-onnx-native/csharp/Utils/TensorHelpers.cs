// =============================================================================
// TensorHelpers — Tensor Utility Functions for ONNX Runtime
// =============================================================================
// Provides helper methods for creating tensors, generating noise, and
// performing element-wise operations on float arrays used throughout
// the inference pipeline.
// =============================================================================

using System.Numerics;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace VoiceLabs.OnnxNative.Utils;

/// <summary>
/// Utility methods for tensor creation and numerical operations.
/// </summary>
public static class TensorHelpers
{
    // =========================================================================
    // Tensor Creation
    // =========================================================================

    /// <summary>
    /// Creates an ONNX Runtime <see cref="DenseTensor{T}"/> from a flat data array and shape.
    /// </summary>
    /// <typeparam name="T">Element type (float, long, int, etc.).</typeparam>
    /// <param name="data">Flat array of tensor data.</param>
    /// <param name="dimensions">Shape of the tensor (e.g., [1, 1024, 50]).</param>
    /// <returns>A new <see cref="DenseTensor{T}"/>.</returns>
    public static DenseTensor<T> CreateTensor<T>(T[] data, int[] dimensions)
    {
        var tensor = new DenseTensor<T>(dimensions);
        data.AsSpan().CopyTo(tensor.Buffer.Span);
        return tensor;
    }

    /// <summary>
    /// Creates a <see cref="DenseTensor{T}"/> filled with zeros.
    /// </summary>
    public static DenseTensor<T> Zeros<T>(int[] dimensions) where T : struct
    {
        return new DenseTensor<T>(dimensions);
    }

    /// <summary>
    /// Creates a <see cref="DenseTensor{T}"/> filled with ones.
    /// </summary>
    public static DenseTensor<float> Ones(int[] dimensions)
    {
        var tensor = new DenseTensor<float>(dimensions);
        tensor.Fill(1.0f);
        return tensor;
    }

    // =========================================================================
    // Random Noise Generation
    // =========================================================================

    /// <summary>
    /// Generates a float array of random samples from a standard normal distribution
    /// (mean=0, std=1) using the Box-Muller transform.
    /// </summary>
    /// <param name="shape">Shape of the desired tensor (product = number of samples).</param>
    /// <param name="seed">Random seed for reproducibility.</param>
    /// <returns>Float array with Gaussian noise.</returns>
    public static float[] Randn(int[] shape, int seed)
    {
        int totalSize = 1;
        foreach (int dim in shape) totalSize *= dim;

        var rng = new Random(seed);
        var result = new float[totalSize];

        // Box-Muller transform: generates pairs of independent normal samples
        for (int i = 0; i < totalSize - 1; i += 2)
        {
            double u1 = 1.0 - rng.NextDouble(); // Avoid log(0)
            double u2 = rng.NextDouble();
            double magnitude = Math.Sqrt(-2.0 * Math.Log(u1));

            result[i] = (float)(magnitude * Math.Cos(2.0 * Math.PI * u2));
            result[i + 1] = (float)(magnitude * Math.Sin(2.0 * Math.PI * u2));
        }

        // Handle odd-length arrays
        if (totalSize % 2 == 1)
        {
            double u1 = 1.0 - rng.NextDouble();
            double u2 = rng.NextDouble();
            result[totalSize - 1] = (float)(Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2));
        }

        return result;
    }

    // =========================================================================
    // Element-wise Operations
    // =========================================================================

    /// <summary>Element-wise addition: result[i] = a[i] + b[i].</summary>
    public static float[] Add(float[] a, float[] b)
    {
        ValidateSameLength(a, b);
        var result = new float[a.Length];
        var spanA = new ReadOnlySpan<float>(a);
        var spanB = new ReadOnlySpan<float>(b);

        int simdLen = Vector<float>.Count;
        int i = 0;

        // SIMD path
        for (; i <= a.Length - simdLen; i += simdLen)
        {
            var va = new Vector<float>(spanA[i..]);
            var vb = new Vector<float>(spanB[i..]);
            (va + vb).CopyTo(result, i);
        }

        // Scalar remainder
        for (; i < a.Length; i++)
            result[i] = a[i] + b[i];

        return result;
    }

    /// <summary>Element-wise subtraction: result[i] = a[i] - b[i].</summary>
    public static float[] Subtract(float[] a, float[] b)
    {
        ValidateSameLength(a, b);
        var result = new float[a.Length];
        var spanA = new ReadOnlySpan<float>(a);
        var spanB = new ReadOnlySpan<float>(b);

        int simdLen = Vector<float>.Count;
        int i = 0;

        for (; i <= a.Length - simdLen; i += simdLen)
        {
            var va = new Vector<float>(spanA[i..]);
            var vb = new Vector<float>(spanB[i..]);
            (va - vb).CopyTo(result, i);
        }

        for (; i < a.Length; i++)
            result[i] = a[i] - b[i];

        return result;
    }

    /// <summary>Element-wise multiplication: result[i] = a[i] * b[i].</summary>
    public static float[] Multiply(float[] a, float[] b)
    {
        ValidateSameLength(a, b);
        var result = new float[a.Length];
        var spanA = new ReadOnlySpan<float>(a);
        var spanB = new ReadOnlySpan<float>(b);

        int simdLen = Vector<float>.Count;
        int i = 0;

        for (; i <= a.Length - simdLen; i += simdLen)
        {
            var va = new Vector<float>(spanA[i..]);
            var vb = new Vector<float>(spanB[i..]);
            (va * vb).CopyTo(result, i);
        }

        for (; i < a.Length; i++)
            result[i] = a[i] * b[i];

        return result;
    }

    /// <summary>Scalar multiplication: result[i] = a[i] * scalar.</summary>
    public static float[] Multiply(float[] a, float scalar)
    {
        var result = new float[a.Length];
        var spanA = new ReadOnlySpan<float>(a);
        var scalarVec = new Vector<float>(scalar);

        int simdLen = Vector<float>.Count;
        int i = 0;

        for (; i <= a.Length - simdLen; i += simdLen)
        {
            var va = new Vector<float>(spanA[i..]);
            (va * scalarVec).CopyTo(result, i);
        }

        for (; i < a.Length; i++)
            result[i] = a[i] * scalar;

        return result;
    }

    // =========================================================================
    // Mathematical Functions
    // =========================================================================

    /// <summary>
    /// Computes softmax over a float array: softmax(x_i) = exp(x_i) / sum(exp(x_j)).
    /// Uses the max-subtraction trick for numerical stability.
    /// </summary>
    public static float[] Softmax(float[] input)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (input.Length == 0) return [];

        float max = input.Max();
        var result = new float[input.Length];
        float sum = 0;

        for (int i = 0; i < input.Length; i++)
        {
            result[i] = MathF.Exp(input[i] - max);
            sum += result[i];
        }

        if (sum > 0)
        {
            for (int i = 0; i < result.Length; i++)
                result[i] /= sum;
        }

        return result;
    }

    /// <summary>
    /// Normalizes a float array to unit L2 norm.
    /// </summary>
    public static float[] Normalize(float[] input)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (input.Length == 0) return [];

        double sumSquared = 0;
        for (int i = 0; i < input.Length; i++)
            sumSquared += (double)input[i] * input[i];

        float norm = (float)Math.Sqrt(sumSquared);
        if (norm < 1e-12f)
            return new float[input.Length]; // Zero vector stays zero

        var result = new float[input.Length];
        for (int i = 0; i < input.Length; i++)
            result[i] = input[i] / norm;

        return result;
    }

    // =========================================================================
    // Validation
    // =========================================================================

    private static void ValidateSameLength(float[] a, float[] b)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);
        if (a.Length != b.Length)
            throw new ArgumentException($"Array lengths must match: {a.Length} vs {b.Length}.");
    }
}
