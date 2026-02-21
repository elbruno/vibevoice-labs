// =============================================================================
// VibeVoiceOnnxPipeline — Main TTS Orchestrator (ONNX Runtime)
// =============================================================================
// Loads the three ONNX model stages and runs the full TTS pipeline:
//   Tokenize → TextEncoder → DiffusionLoop → AcousticDecoder → float[] audio
//
// All inference runs on ONNX Runtime — no Python, no HTTP, no subprocess.
// =============================================================================

using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using VoiceLabs.OnnxNative.Utils;

namespace VoiceLabs.OnnxNative.Pipeline;

/// <summary>
/// Orchestrates the full VibeVoice TTS pipeline using three ONNX models.
/// </summary>
public sealed class VibeVoiceOnnxPipeline : IDisposable
{
    private readonly InferenceSession _textEncoder;
    private readonly InferenceSession _diffusionStep;
    private readonly InferenceSession _acousticDecoder;
    private readonly VibeVoiceTokenizer _tokenizer;
    private readonly VoicePresetManager _voicePresets;
    private readonly DiffusionScheduler _scheduler;
    private readonly string _modelsDir;
    private bool _disposed;

    /// <summary>Number of diffusion denoising steps (fewer = faster, more = higher quality).</summary>
    public int DiffusionSteps { get; set; } = 5;

    /// <summary>Classifier-free guidance scale (higher = more adherence to text conditioning).</summary>
    public float CfgScale { get; set; } = 1.5f;

    /// <summary>Random seed for reproducible noise generation (-1 for random).</summary>
    public int Seed { get; set; } = 42;

    // TODO: Verify these dimensions after exporting models — they depend on the VibeVoice architecture.
    private const int LatentDim = 1024;
    private const int LatentLength = 50;

    /// <summary>
    /// Creates a new pipeline instance and loads all ONNX models.
    /// </summary>
    /// <param name="modelsDir">
    /// Path to the directory containing text_encoder.onnx, diffusion_step.onnx,
    /// acoustic_decoder.onnx, tokenizer.json, and the voices/ subdirectory.
    /// </param>
    /// <exception cref="FileNotFoundException">Thrown when required model files are missing.</exception>
    public VibeVoiceOnnxPipeline(string modelsDir)
    {
        _modelsDir = modelsDir;
        ValidateModelFiles(modelsDir);

        var sessionOptions = new SessionOptions
        {
            GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL
        };

        _textEncoder = new InferenceSession(Path.Combine(modelsDir, "text_encoder.onnx"), sessionOptions);
        _diffusionStep = new InferenceSession(Path.Combine(modelsDir, "diffusion_step.onnx"), sessionOptions);
        _acousticDecoder = new InferenceSession(Path.Combine(modelsDir, "acoustic_decoder.onnx"), sessionOptions);

        _tokenizer = new VibeVoiceTokenizer(Path.Combine(modelsDir, "tokenizer.json"));
        _voicePresets = new VoicePresetManager(Path.Combine(modelsDir, "voices"));
        _scheduler = new DiffusionScheduler(DiffusionSteps);
    }

    /// <summary>
    /// Generates audio samples from text using the specified voice preset.
    /// </summary>
    /// <param name="text">The text to synthesize.</param>
    /// <param name="voice">Voice preset name (e.g., "Carter", "Emma").</param>
    /// <returns>Float array of audio samples at 24kHz, normalized to [-1, 1].</returns>
    public float[] GenerateAudio(string text, string voice)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        ArgumentException.ThrowIfNullOrWhiteSpace(voice);

        // Step 1: Tokenize input text
        int[] tokenIds = _tokenizer.Encode(text);

        // Step 2: Run text encoder (tokens → hidden states)
        float[] hiddenStates = RunTextEncoder(tokenIds);

        // Step 3: Load voice preset conditioning
        float[] voiceConditioning = GetVoiceConditioning(voice);

        // Step 4: Diffusion loop (denoise latents from noise → clean latents)
        float[] latents = RunDiffusionLoop(hiddenStates, voiceConditioning);

        // Step 5: Acoustic decoder (latents → raw waveform)
        float[] audio = RunAcousticDecoder(latents);

        return audio;
    }

    /// <summary>Returns the list of available voice preset names.</summary>
    public string[] GetAvailableVoices() => _voicePresets.GetAvailableVoices();

    // =========================================================================
    // Pipeline Stages
    // =========================================================================

    /// <summary>
    /// Runs the text encoder model: token IDs → hidden states.
    /// </summary>
    private float[] RunTextEncoder(int[] tokenIds)
    {
        // Create input tensors
        // TODO: Verify input/output tensor names after model export (inspect with Netron or onnxruntime)
        var inputIdsTensor = TensorHelpers.CreateTensor(
            tokenIds.Select(id => (long)id).ToArray(),
            [1, tokenIds.Length]);

        var attentionMask = new long[tokenIds.Length];
        Array.Fill(attentionMask, 1L);
        var attentionMaskTensor = TensorHelpers.CreateTensor(attentionMask, [1, tokenIds.Length]);

        var inputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor("input_ids", inputIdsTensor),
            NamedOnnxValue.CreateFromTensor("attention_mask", attentionMaskTensor)
        };

        // Run inference
        using var results = _textEncoder.Run(inputs);

        // TODO: Verify output tensor name — commonly "last_hidden_state" or "hidden_states"
        var outputTensor = results.First().AsTensor<float>();
        return outputTensor.ToArray();
    }

    /// <summary>
    /// Loads voice conditioning vector from the preset manager.
    /// Falls back to a zero vector if the voice preset is not found.
    /// </summary>
    private float[] GetVoiceConditioning(string voice)
    {
        try
        {
            var presets = _voicePresets.GetVoicePreset(voice);

            // TODO: Verify the key name used for voice conditioning in the exported model
            if (presets.TryGetValue("speaker_embedding", out var embedding))
                return embedding;

            if (presets.TryGetValue("voice_conditioning", out var conditioning))
                return conditioning;

            // Return the first available tensor if key names don't match
            if (presets.Count > 0)
                return presets.Values.First();
        }
        catch (FileNotFoundException)
        {
            Console.WriteLine($"   ⚠️  Voice preset '{voice}' not found, using default conditioning.");
        }

        // Fallback: zero conditioning (no voice-specific style)
        // TODO: Verify the expected conditioning vector dimension
        return new float[256];
    }

    /// <summary>
    /// Runs the diffusion denoising loop: noise → clean latents.
    /// </summary>
    private float[] RunDiffusionLoop(float[] hiddenStates, float[] voiceConditioning)
    {
        var scheduler = new DiffusionScheduler(DiffusionSteps);
        int[] timesteps = scheduler.GetTimesteps();

        // Initialize with random Gaussian noise
        int[] latentShape = [1, LatentDim, LatentLength];
        float[] latents = TensorHelpers.Randn(latentShape, Seed >= 0 ? Seed : Random.Shared.Next());

        // Denoise iteratively
        for (int i = 0; i < timesteps.Length; i++)
        {
            // Predict noise at current timestep
            float[] noisePrediction = RunDiffusionStep(latents, hiddenStates, voiceConditioning, timesteps[i]);

            // Apply classifier-free guidance if scale > 1
            if (CfgScale > 1.0f)
            {
                float[] unconditionalPrediction = RunDiffusionStep(
                    latents, hiddenStates, new float[voiceConditioning.Length], timesteps[i]);

                for (int j = 0; j < noisePrediction.Length; j++)
                {
                    noisePrediction[j] = unconditionalPrediction[j]
                        + CfgScale * (noisePrediction[j] - unconditionalPrediction[j]);
                }
            }

            // Scheduler step: remove predicted noise
            latents = scheduler.Step(noisePrediction, timesteps[i], latents);
        }

        return latents;
    }

    /// <summary>
    /// Runs a single diffusion denoising step.
    /// </summary>
    private float[] RunDiffusionStep(float[] latents, float[] hiddenStates, float[] voiceConditioning, int timestep)
    {
        // TODO: Verify tensor names and shapes after model export
        var latentTensor = TensorHelpers.CreateTensor(latents, [1, LatentDim, LatentLength]);
        var hiddenTensor = TensorHelpers.CreateTensor(hiddenStates,
            [1, hiddenStates.Length / LatentDim, LatentDim]);
        var condTensor = TensorHelpers.CreateTensor(voiceConditioning, [1, voiceConditioning.Length]);
        var timestepTensor = TensorHelpers.CreateTensor(new long[] { timestep }, [1]);

        var inputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor("latent_sample", latentTensor),
            NamedOnnxValue.CreateFromTensor("encoder_hidden_states", hiddenTensor),
            NamedOnnxValue.CreateFromTensor("voice_conditioning", condTensor),
            NamedOnnxValue.CreateFromTensor("timestep", timestepTensor)
        };

        using var results = _diffusionStep.Run(inputs);

        // TODO: Verify output tensor name — commonly "noise_pred" or "sample"
        var outputTensor = results.First().AsTensor<float>();
        return outputTensor.ToArray();
    }

    /// <summary>
    /// Runs the acoustic decoder: latent representation → raw audio waveform.
    /// </summary>
    private float[] RunAcousticDecoder(float[] latents)
    {
        // TODO: Verify tensor names after model export
        var latentTensor = TensorHelpers.CreateTensor(latents, [1, LatentDim, LatentLength]);

        var inputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor("latent_input", latentTensor)
        };

        using var results = _acousticDecoder.Run(inputs);

        // TODO: Verify output tensor name — commonly "waveform" or "audio"
        var outputTensor = results.First().AsTensor<float>();
        float[] audio = outputTensor.ToArray();

        // Clamp output to [-1, 1] for valid audio range
        for (int i = 0; i < audio.Length; i++)
        {
            audio[i] = Math.Clamp(audio[i], -1.0f, 1.0f);
        }

        return audio;
    }

    // =========================================================================
    // Validation
    // =========================================================================

    private static void ValidateModelFiles(string modelsDir)
    {
        if (!Directory.Exists(modelsDir))
            throw new DirectoryNotFoundException($"Models directory not found: {modelsDir}");

        string[] required = ["text_encoder.onnx", "diffusion_step.onnx", "acoustic_decoder.onnx", "tokenizer.json"];
        foreach (var file in required)
        {
            var path = Path.Combine(modelsDir, file);
            if (!File.Exists(path))
                throw new FileNotFoundException($"Required model file not found: {file}. Run the export tool first.", path);
        }
    }

    // =========================================================================
    // IDisposable
    // =========================================================================

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _textEncoder.Dispose();
        _diffusionStep.Dispose();
        _acousticDecoder.Dispose();
    }
}
