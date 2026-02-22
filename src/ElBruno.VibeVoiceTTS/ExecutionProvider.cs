namespace ElBruno.VibeVoiceTTS;

/// <summary>
/// Specifies the ONNX Runtime execution provider for model inference.
/// </summary>
public enum ExecutionProvider
{
    /// <summary>
    /// CPU execution (default). Uses the built-in Microsoft.ML.OnnxRuntime package.
    /// </summary>
    Cpu = 0,

    /// <summary>
    /// DirectML GPU acceleration (NVIDIA, AMD, Intel — Windows only).
    /// Requires the Microsoft.ML.OnnxRuntime.DirectML NuGet package.
    /// </summary>
    DirectML = 1,

    /// <summary>
    /// CUDA GPU acceleration (NVIDIA only — Windows and Linux).
    /// Requires the Microsoft.ML.OnnxRuntime.Gpu NuGet package.
    /// </summary>
    Cuda = 2
}
