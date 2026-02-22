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

    // Matches model_config.json: latent_size=64, speech_vae_dim=64
    private const int LatentDim = 64;
    private const int LatentLength = 50;

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
        ArgumentException.ThrowIfNullOrWhiteSpace(voice);

        int[] tokenIds = _tokenizer.Encode(text);
        float[] hiddenStates = RunTextEncoder(tokenIds);
        float[] voiceConditioning = GetVoiceConditioning(voice);
        float[] latents = RunDiffusionLoop(hiddenStates, voiceConditioning);
        float[] audio = RunAcousticDecoder(latents);
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

    private float[] GetVoiceConditioning(string voice)
    {
        try
        {
            var presets = _voicePresets.GetVoicePreset(voice);

            if (presets.TryGetValue("speaker_embedding", out var embedding))
                return embedding;
            if (presets.TryGetValue("voice_conditioning", out var conditioning))
                return conditioning;
            if (presets.Count > 0)
                return presets.Values.First();
        }
        catch (FileNotFoundException)
        {
            // Fall back to default conditioning
        }

        return new float[256];
    }

    private float[] RunDiffusionLoop(float[] hiddenStates, float[] voiceConditioning)
    {
        var scheduler = new DiffusionScheduler(DiffusionSteps);
        int[] timesteps = scheduler.GetTimesteps();

        int[] latentShape = [1, LatentDim, LatentLength];
        float[] latents = Utils.TensorHelpers.Randn(latentShape, Seed >= 0 ? Seed : Random.Shared.Next());

        for (int i = 0; i < timesteps.Length; i++)
        {
            float[] noisePrediction = RunDiffusionStep(latents, hiddenStates, voiceConditioning, timesteps[i]);

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

            latents = scheduler.Step(noisePrediction, timesteps[i], latents);
        }

        return latents;
    }

    private float[] RunDiffusionStep(float[] latents, float[] hiddenStates, float[] voiceConditioning, int timestep)
    {
        var latentTensor = Utils.TensorHelpers.CreateTensor(latents, [1, LatentDim, LatentLength]);
        var hiddenTensor = Utils.TensorHelpers.CreateTensor(hiddenStates,
            [1, hiddenStates.Length / LatentDim, LatentDim]);
        var condTensor = Utils.TensorHelpers.CreateTensor(voiceConditioning, [1, voiceConditioning.Length]);
        var timestepTensor = Utils.TensorHelpers.CreateTensor(new long[] { timestep }, [1]);

        var inputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor("latent_sample", latentTensor),
            NamedOnnxValue.CreateFromTensor("encoder_hidden_states", hiddenTensor),
            NamedOnnxValue.CreateFromTensor("voice_conditioning", condTensor),
            NamedOnnxValue.CreateFromTensor("timestep", timestepTensor)
        };

        using var results = _diffusionStep.Run(inputs);
        var outputTensor = results.First().AsTensor<float>();
        return outputTensor.ToArray();
    }

    private float[] RunAcousticDecoder(float[] latents)
    {
        var latentTensor = Utils.TensorHelpers.CreateTensor(latents, [1, LatentDim, LatentLength]);

        var inputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor("latent_input", latentTensor)
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
