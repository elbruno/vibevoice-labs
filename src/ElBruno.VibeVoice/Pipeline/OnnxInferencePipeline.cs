using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace ElBruno.VibeVoice.Pipeline;

/// <summary>
/// Orchestrates the full VibeVoice TTS pipeline using three ONNX models.
/// Flow: text → encoder → mean-pool condition → N × (diffusion → scale → decode) → concat audio
/// </summary>
internal sealed class OnnxInferencePipeline : IDisposable
{
    private readonly InferenceSession _textEncoder;
    private readonly InferenceSession _predictionHead;
    private readonly InferenceSession _acousticDecoder;
    private readonly BpeTokenizer _tokenizer;
    private readonly VoicePresetLoader _voicePresets;
    private bool _disposed;

    public int DiffusionSteps { get; set; } = 20;
    public float CfgScale { get; set; } = 3.0f;
    public int Seed { get; set; } = 42;

    // From model inspection
    private const int LatentDim = 64;
    private const int HiddenSize = 896;
    private const float SpeechScalingFactor = 0.2333984375f;
    private const float SpeechBiasFactor = -0.0703125f;
    private const int SamplesPerFrame = 3200; // acoustic decoder produces 3200 samples per latent

    public OnnxInferencePipeline(string modelsDir, int diffusionSteps = 20, float cfgScale = 3.0f, int seed = 42)
    {
        DiffusionSteps = diffusionSteps;
        CfgScale = cfgScale;
        Seed = seed;

        var sessionOptions = new SessionOptions
        {
            GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL
        };

        _textEncoder = new InferenceSession(Path.Combine(modelsDir, "text_encoder.onnx"), sessionOptions);
        _predictionHead = new InferenceSession(Path.Combine(modelsDir, "prediction_head.onnx"), sessionOptions);
        _acousticDecoder = new InferenceSession(Path.Combine(modelsDir, "acoustic_decoder.onnx"), sessionOptions);

        _tokenizer = new BpeTokenizer(Path.Combine(modelsDir, "tokenizer.json"));
        _voicePresets = new VoicePresetLoader(Path.Combine(modelsDir, "voices"));
    }

    public float[] GenerateAudio(string text, string voice)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(text);

        int[] tokenIds = _tokenizer.Encode(text);
        int seqLen = tokenIds.Length;

        // 1. Text encoder: tokens → hidden states [1, seqLen, 896]
        float[] hiddenStates = RunTextEncoder(tokenIds);

        // 2. Mean-pool hidden states to get condition vector [896]
        float[] condition = MeanPoolHiddenStates(hiddenStates, seqLen);

        // 3. Generate speech latents via diffusion (one per frame)
        //    Heuristic: ~1 frame per token, minimum 10 frames
        int numFrames = Math.Max(seqLen, 10);
        var rng = new Random(Seed >= 0 ? Seed : Random.Shared.Next());

        // Generate all latents and scale them
        float[] allLatents = new float[LatentDim * numFrames]; // [64, numFrames] interleaved as [d0_f0, d0_f1, ... d0_fN, d1_f0, ...]
        for (int frame = 0; frame < numFrames; frame++)
        {
            float[] latent = SampleSpeechLatent(condition, rng);
            for (int d = 0; d < LatentDim; d++)
                allLatents[d * numFrames + frame] = latent[d] / SpeechScalingFactor - SpeechBiasFactor;
        }

        // 4. Batch decode all frames: [1, 64, numFrames] → [1, 1, numFrames * 3200]
        float[] audio = RunAcousticDecoderBatch(allLatents, numFrames);
        return audio;
    }

    public string[] GetAvailableVoices() => _voicePresets.GetAvailableVoices();

    private float[] RunTextEncoder(int[] tokenIds)
    {
        var inputIdsTensor = Utils.TensorHelpers.CreateTensor(
            tokenIds.Select(id => (long)id).ToArray(),
            [1, tokenIds.Length]);

        var attentionMask = new long[tokenIds.Length];
        Array.Fill(attentionMask, 1L);
        var attentionMaskTensor = Utils.TensorHelpers.CreateTensor(attentionMask, [1, tokenIds.Length]);

        var inputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor("input_ids", inputIdsTensor),
            NamedOnnxValue.CreateFromTensor("attention_mask", attentionMaskTensor)
        };

        using var results = _textEncoder.Run(inputs);
        var outputTensor = results.First().AsTensor<float>();
        return outputTensor.ToArray();
    }

    private static float[] MeanPoolHiddenStates(float[] hiddenStates, int seqLen)
    {
        // hiddenStates is [1, seqLen, HiddenSize] flattened → mean across seqLen → [HiddenSize]
        float[] pooled = new float[HiddenSize];
        for (int h = 0; h < HiddenSize; h++)
        {
            float sum = 0;
            for (int s = 0; s < seqLen; s++)
                sum += hiddenStates[s * HiddenSize + h];
            pooled[h] = sum / seqLen;
        }
        return pooled;
    }

    /// <summary>
    /// Runs the diffusion denoising loop to produce a single speech latent [64].
    /// Mirrors Python sample_speech_tokens: uses CFG with positive + negative conditions.
    /// </summary>
    private float[] SampleSpeechLatent(float[] condition, Random rng)
    {
        var scheduler = new DiffusionScheduler(DiffusionSteps);
        int[] timesteps = scheduler.GetTimesteps();

        // Initialize noise [64]
        float[] speech = Utils.TensorHelpers.Randn([LatentDim], rng.Next());

        for (int i = 0; i < timesteps.Length; i++)
        {
            // Positive pass: prediction_head(speech, timestep, condition)
            float[] condPred = RunPredictionHead(speech, condition, timesteps[i]);

            if (CfgScale > 1.0f)
            {
                // Negative pass: prediction_head(speech, timestep, zeros)
                float[] negCondition = new float[HiddenSize];
                float[] uncondPred = RunPredictionHead(speech, negCondition, timesteps[i]);

                // CFG: uncond + scale * (cond - uncond)
                for (int j = 0; j < condPred.Length; j++)
                    condPred[j] = uncondPred[j] + CfgScale * (condPred[j] - uncondPred[j]);
            }

            speech = scheduler.Step(condPred, timesteps[i], speech);
        }

        return speech;
    }

    private float[] RunPredictionHead(float[] latent, float[] condition, int timestep)
    {
        // noisy_latent: [1, 64]
        var latentTensor = Utils.TensorHelpers.CreateTensor(latent, [1, LatentDim]);
        // conditioning: [1, 1, 896] — single condition vector wrapped as seq_len=1
        var condTensor = Utils.TensorHelpers.CreateTensor(condition, [1, 1, HiddenSize]);
        // timestep: [1]
        var timestepTensor = Utils.TensorHelpers.CreateTensor(new long[] { timestep }, [1]);

        var inputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor("noisy_latent", latentTensor),
            NamedOnnxValue.CreateFromTensor("timestep", timestepTensor),
            NamedOnnxValue.CreateFromTensor("conditioning", condTensor)
        };

        using var results = _predictionHead.Run(inputs);
        var outputTensor = results.First().AsTensor<float>();
        float[] output = outputTensor.ToArray();

        // Output is [1, 1, 64] → extract just the [64] portion
        if (output.Length > LatentDim)
        {
            float[] trimmed = new float[LatentDim];
            Array.Copy(output, trimmed, LatentDim);
            return trimmed;
        }
        return output;
    }

    private float[] RunAcousticDecoderBatch(float[] scaledLatents, int numFrames)
    {
        // latent: [1, 64, numFrames]
        var latentTensor = Utils.TensorHelpers.CreateTensor(scaledLatents, [1, LatentDim, numFrames]);

        var inputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor("latent", latentTensor)
        };

        using var results = _acousticDecoder.Run(inputs);
        var outputTensor = results.First().AsTensor<float>();
        float[] audio = outputTensor.ToArray();

        for (int i = 0; i < audio.Length; i++)
            audio[i] = Math.Clamp(audio[i], -1.0f, 1.0f);

        return audio;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _textEncoder.Dispose();
        _predictionHead.Dispose();
        _acousticDecoder.Dispose();
    }
}
