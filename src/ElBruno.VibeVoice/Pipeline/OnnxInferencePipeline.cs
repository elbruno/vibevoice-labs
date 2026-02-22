using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace ElBruno.VibeVoice.Pipeline;

/// <summary>
/// Orchestrates the full VibeVoice TTS pipeline using three ONNX models.
/// </summary>
internal sealed class OnnxInferencePipeline : IDisposable
{
    private readonly InferenceSession _textEncoder;
    private readonly InferenceSession _diffusionStep;
    private readonly InferenceSession _acousticDecoder;
    private readonly BpeTokenizer _tokenizer;
    private readonly VoicePresetLoader _voicePresets;
    private bool _disposed;

    public int DiffusionSteps { get; set; } = 20;
    public float CfgScale { get; set; } = 1.5f;
    public int Seed { get; set; } = 42;

    // Matches model_config.json: latent_size=64, hidden_size=896
    private const int LatentDim = 64;
    private const int HiddenSize = 896;

    public OnnxInferencePipeline(string modelsDir, int diffusionSteps = 20, float cfgScale = 1.5f, int seed = 42)
    {
        DiffusionSteps = diffusionSteps;
        CfgScale = cfgScale;
        Seed = seed;

        var sessionOptions = new SessionOptions
        {
            GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL
        };

        _textEncoder = new InferenceSession(Path.Combine(modelsDir, "text_encoder.onnx"), sessionOptions);
        _diffusionStep = new InferenceSession(Path.Combine(modelsDir, "prediction_head.onnx"), sessionOptions);
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

        // 2. Diffusion loop: denoise latents conditioned on hidden states
        //    noisy_latent [1, 64] → predicted [1, seqLen, 64] per step
        float[] latents = RunDiffusionLoop(hiddenStates, seqLen);

        // 3. Acoustic decoder: latents [1, 64, seqLen] → waveform
        float[] audio = RunAcousticDecoder(latents, seqLen);
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

    private float[] RunDiffusionLoop(float[] hiddenStates, int seqLen)
    {
        var scheduler = new DiffusionScheduler(DiffusionSteps);
        int[] timesteps = scheduler.GetTimesteps();

        // Latents: [1, LatentDim] — a single latent vector that gets denoised
        float[] latents = Utils.TensorHelpers.Randn([1, LatentDim], Seed >= 0 ? Seed : Random.Shared.Next());

        for (int i = 0; i < timesteps.Length; i++)
        {
            float[] noisePrediction = RunDiffusionStep(latents, hiddenStates, seqLen, timesteps[i]);

            // Flatten prediction to match latent size for scheduler step
            // prediction_head output is [1, seqLen, 64] but we denoise [1, 64]
            // Take the mean across the sequence dimension
            float[] meanPrediction = new float[LatentDim];
            for (int d = 0; d < LatentDim; d++)
            {
                float sum = 0;
                for (int s = 0; s < seqLen; s++)
                    sum += noisePrediction[s * LatentDim + d];
                meanPrediction[d] = sum / seqLen;
            }

            latents = scheduler.Step(meanPrediction, timesteps[i], latents);
        }

        // Expand final latent [LatentDim] to [seqLen, LatentDim] then transpose to [LatentDim, seqLen]
        // for acoustic decoder input [1, 64, time]
        float[] expandedLatents = new float[LatentDim * seqLen];
        for (int d = 0; d < LatentDim; d++)
            for (int s = 0; s < seqLen; s++)
                expandedLatents[d * seqLen + s] = latents[d];

        return expandedLatents;
    }

    private float[] RunDiffusionStep(float[] latents, float[] hiddenStates, int seqLen, int timestep)
    {
        // noisy_latent: [1, 64]
        var latentTensor = Utils.TensorHelpers.CreateTensor(latents, [1, LatentDim]);
        // conditioning: [1, seqLen, 896]
        var condTensor = Utils.TensorHelpers.CreateTensor(hiddenStates, [1, seqLen, HiddenSize]);
        // timestep: [1]
        var timestepTensor = Utils.TensorHelpers.CreateTensor(new long[] { timestep }, [1]);

        var inputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor("noisy_latent", latentTensor),
            NamedOnnxValue.CreateFromTensor("timestep", timestepTensor),
            NamedOnnxValue.CreateFromTensor("conditioning", condTensor)
        };

        using var results = _diffusionStep.Run(inputs);
        var outputTensor = results.First().AsTensor<float>();
        return outputTensor.ToArray();
    }

    private float[] RunAcousticDecoder(float[] latents, int seqLen)
    {
        // latent: [1, 64, time]
        var latentTensor = Utils.TensorHelpers.CreateTensor(latents, [1, LatentDim, seqLen]);

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
        _diffusionStep.Dispose();
        _acousticDecoder.Dispose();
    }
}
