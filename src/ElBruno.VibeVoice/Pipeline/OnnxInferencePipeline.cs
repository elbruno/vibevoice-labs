using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace ElBruno.VibeVoice.Pipeline;

/// <summary>
/// Orchestrates the full VibeVoice TTS pipeline using three ONNX models.
/// Flow: text → text_to_condition (fused LM + TTS-LM) → last-token condition →
///       N × (diffusion with CFG → scale latent) → batch acoustic decode → audio
/// </summary>
internal sealed class OnnxInferencePipeline : IDisposable
{
    private readonly InferenceSession _textToCondition;
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
    private const int SamplesPerFrame = 3200;

    // Heuristic: ~5 characters per speech frame at 24kHz
    private const int CharsPerFrame = 5;

    public OnnxInferencePipeline(string modelsDir, int diffusionSteps = 20, float cfgScale = 3.0f, int seed = 42)
    {
        DiffusionSteps = diffusionSteps;
        CfgScale = cfgScale;
        Seed = seed;

        var sessionOptions = new SessionOptions
        {
            GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL
        };

        _textToCondition = new InferenceSession(Path.Combine(modelsDir, "text_to_condition.onnx"), sessionOptions);
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

        // 1. Fused text_to_condition: tokens → tts_lm hidden states [1, seqLen, 896]
        float[] hiddenStates = RunTextToCondition(tokenIds);

        // 2. Extract last-token hidden state as condition vector [896]
        int seqLen = tokenIds.Length;
        float[] condition = new float[HiddenSize];
        int lastTokenOffset = (seqLen - 1) * HiddenSize;
        Array.Copy(hiddenStates, lastTokenOffset, condition, 0, HiddenSize);

        // 3. Estimate number of speech frames from text length
        int numFrames = Math.Max(text.Length / CharsPerFrame, 8);
        var rng = new Random(Seed >= 0 ? Seed : Random.Shared.Next());

        // 4. Generate all speech latents via diffusion and scale them
        float[] allLatents = new float[LatentDim * numFrames];
        for (int frame = 0; frame < numFrames; frame++)
        {
            float[] latent = SampleSpeechLatent(condition, rng);
            for (int d = 0; d < LatentDim; d++)
                allLatents[d * numFrames + frame] = latent[d] / SpeechScalingFactor - SpeechBiasFactor;
        }

        // 5. Batch decode all frames: [1, 64, numFrames] → audio
        float[] audio = RunAcousticDecoderBatch(allLatents, numFrames);
        return audio;
    }

    public string[] GetAvailableVoices() => _voicePresets.GetAvailableVoices();

    private float[] RunTextToCondition(int[] tokenIds)
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

        using var results = _textToCondition.Run(inputs);
        var outputTensor = results.First().AsTensor<float>();
        return outputTensor.ToArray();
    }

    /// <summary>
    /// Runs the DPM-Solver++ diffusion loop to produce a single speech latent [64].
    /// Uses classifier-free guidance (CFG) with positive + negative (zeros) conditions.
    /// </summary>
    private float[] SampleSpeechLatent(float[] condition, Random rng)
    {
        var scheduler = new DiffusionScheduler(DiffusionSteps);
        int[] timesteps = scheduler.GetTimesteps();

        float[] speech = Utils.TensorHelpers.Randn([LatentDim], rng.Next());

        for (int i = 0; i < timesteps.Length; i++)
        {
            float[] condPred = RunPredictionHead(speech, condition, timesteps[i]);

            if (CfgScale > 1.0f)
            {
                float[] negCondition = new float[HiddenSize];
                float[] uncondPred = RunPredictionHead(speech, negCondition, timesteps[i]);

                for (int j = 0; j < condPred.Length; j++)
                    condPred[j] = uncondPred[j] + CfgScale * (condPred[j] - uncondPred[j]);
            }

            speech = scheduler.Step(condPred, timesteps[i], speech);
        }

        return speech;
    }

    private float[] RunPredictionHead(float[] latent, float[] condition, int timestep)
    {
        var latentTensor = Utils.TensorHelpers.CreateTensor(latent, [1, LatentDim]);
        var condTensor = Utils.TensorHelpers.CreateTensor(condition, [1, 1, HiddenSize]);
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
        _textToCondition.Dispose();
        _predictionHead.Dispose();
        _acousticDecoder.Dispose();
    }
}
