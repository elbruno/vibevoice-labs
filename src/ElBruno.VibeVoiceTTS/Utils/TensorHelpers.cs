using System.Numerics;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace ElBruno.VibeVoiceTTS.Utils;

/// <summary>
/// Tensor creation and numerical operations for ONNX Runtime.
/// </summary>
internal static class TensorHelpers
{
    public static DenseTensor<T> CreateTensor<T>(T[] data, int[] dimensions)
    {
        var tensor = new DenseTensor<T>(dimensions);
        data.AsSpan().CopyTo(tensor.Buffer.Span);
        return tensor;
    }

    public static DenseTensor<T> Zeros<T>(int[] dimensions) where T : struct
        => new(dimensions);

    public static DenseTensor<float> Ones(int[] dimensions)
    {
        var tensor = new DenseTensor<float>(dimensions);
        tensor.Fill(1.0f);
        return tensor;
    }

    public static float[] Randn(int[] shape, int seed)
    {
        int totalSize = 1;
        foreach (int dim in shape) totalSize *= dim;

        var rng = new Random(seed);
        var result = new float[totalSize];

        for (int i = 0; i < totalSize - 1; i += 2)
        {
            double u1 = 1.0 - rng.NextDouble();
            double u2 = rng.NextDouble();
            double magnitude = Math.Sqrt(-2.0 * Math.Log(u1));
            result[i] = (float)(magnitude * Math.Cos(2.0 * Math.PI * u2));
            result[i + 1] = (float)(magnitude * Math.Sin(2.0 * Math.PI * u2));
        }

        if (totalSize % 2 == 1)
        {
            double u1 = 1.0 - rng.NextDouble();
            double u2 = rng.NextDouble();
            result[totalSize - 1] = (float)(Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2));
        }

        return result;
    }

    public static float[] Add(float[] a, float[] b)
    {
        ValidateSameLength(a, b);
        var result = new float[a.Length];
        int simdLen = Vector<float>.Count;
        int i = 0;
        for (; i <= a.Length - simdLen; i += simdLen)
        {
            var va = new Vector<float>(a, i);
            var vb = new Vector<float>(b, i);
            (va + vb).CopyTo(result, i);
        }
        for (; i < a.Length; i++) result[i] = a[i] + b[i];
        return result;
    }

    public static float[] Subtract(float[] a, float[] b)
    {
        ValidateSameLength(a, b);
        var result = new float[a.Length];
        int simdLen = Vector<float>.Count;
        int i = 0;
        for (; i <= a.Length - simdLen; i += simdLen)
        {
            var va = new Vector<float>(a, i);
            var vb = new Vector<float>(b, i);
            (va - vb).CopyTo(result, i);
        }
        for (; i < a.Length; i++) result[i] = a[i] - b[i];
        return result;
    }

    public static float[] Multiply(float[] a, float[] b)
    {
        ValidateSameLength(a, b);
        var result = new float[a.Length];
        int simdLen = Vector<float>.Count;
        int i = 0;
        for (; i <= a.Length - simdLen; i += simdLen)
        {
            var va = new Vector<float>(a, i);
            var vb = new Vector<float>(b, i);
            (va * vb).CopyTo(result, i);
        }
        for (; i < a.Length; i++) result[i] = a[i] * b[i];
        return result;
    }

    public static float[] Multiply(float[] a, float scalar)
    {
        var result = new float[a.Length];
        var scalarVec = new Vector<float>(scalar);
        int simdLen = Vector<float>.Count;
        int i = 0;
        for (; i <= a.Length - simdLen; i += simdLen)
        {
            var va = new Vector<float>(a, i);
            (va * scalarVec).CopyTo(result, i);
        }
        for (; i < a.Length; i++) result[i] = a[i] * scalar;
        return result;
    }

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
            for (int i = 0; i < result.Length; i++)
                result[i] /= sum;
        return result;
    }

    public static float[] Normalize(float[] input)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (input.Length == 0) return [];
        double sumSquared = 0;
        for (int i = 0; i < input.Length; i++)
            sumSquared += (double)input[i] * input[i];
        float norm = (float)Math.Sqrt(sumSquared);
        if (norm < 1e-12f) return new float[input.Length];
        var result = new float[input.Length];
        for (int i = 0; i < input.Length; i++)
            result[i] = input[i] / norm;
        return result;
    }

    private static void ValidateSameLength(float[] a, float[] b)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);
        if (a.Length != b.Length)
            throw new ArgumentException($"Array lengths must match: {a.Length} vs {b.Length}.");
    }
}
