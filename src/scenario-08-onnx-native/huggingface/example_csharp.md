# C# Inference Example — VibeVoice-Realtime-0.5B ONNX

Complete example showing how to run VibeVoice TTS inference in C# using ONNX Runtime.

## Prerequisites

```bash
dotnet new console -n VibeVoiceTTS
cd VibeVoiceTTS
dotnet add package Microsoft.ML.OnnxRuntime --version 1.17.*
```

## Download Models

Download from this Hugging Face repository:

```bash
# Using git (requires git-lfs)
git lfs install
git clone https://huggingface.co/elbruno/VibeVoice-Realtime-0.5B-ONNX models

# Or download individual files
# https://huggingface.co/elbruno/VibeVoice-Realtime-0.5B-ONNX/tree/main
```

## Full Pipeline Example

```csharp
using System.Diagnostics;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

// =============================================================================
// Step 1: Load ONNX Models
// =============================================================================
var modelsDir = "models"; // Path to downloaded ONNX files

var sessionOptions = new SessionOptions
{
    GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL
};

using var textEncoder = new InferenceSession(
    Path.Combine(modelsDir, "text_encoder.onnx"), sessionOptions);
using var diffusion = new InferenceSession(
    Path.Combine(modelsDir, "diffusion_step.onnx"), sessionOptions);
using var decoder = new InferenceSession(
    Path.Combine(modelsDir, "acoustic_decoder.onnx"), sessionOptions);

Console.WriteLine("✅ ONNX models loaded!");

// =============================================================================
// Step 2: Tokenize Input Text
// =============================================================================
// NOTE: For a full BPE tokenizer implementation, see:
// https://github.com/elbruno/ElBruno.VibeVoiceTTS/blob/main/src/scenario-08-onnx-native/csharp/Pipeline/VibeVoiceTokenizer.cs

string text = "Hello! This is VibeVoice running natively in C#.";
// Placeholder: replace with actual tokenization
long[] tokenIds = { 15496, 0, 1212, 318, 569, 571, 29379, 2491, 38157, 287, 327, 2, 13 };
int seqLen = tokenIds.Length;

// =============================================================================
// Step 3: Run Text Encoder
// =============================================================================
var inputIdsTensor = new DenseTensor<long>(tokenIds, new[] { 1, seqLen });
var attentionMask = new long[seqLen];
Array.Fill(attentionMask, 1L);
var maskTensor = new DenseTensor<long>(attentionMask, new[] { 1, seqLen });

var encoderInputs = new List<NamedOnnxValue>
{
    NamedOnnxValue.CreateFromTensor("input_ids", inputIdsTensor),
    NamedOnnxValue.CreateFromTensor("attention_mask", maskTensor)
};

// TODO: Verify tensor names match exported model
using var encoderResults = textEncoder.Run(encoderInputs);
var hiddenStates = encoderResults.First().AsTensor<float>().ToArray();
Console.WriteLine($"📝 Text encoded: {hiddenStates.Length} values");

// =============================================================================
// Step 4: Diffusion Denoising Loop (5 steps)
// =============================================================================
int latentDim = 1024, latentLen = 50;
int numSteps = 5;
float cfgScale = 1.5f;

// Initialize with random noise
var rng = new Random(42);
float[] latents = new float[latentDim * latentLen];
for (int i = 0; i < latents.Length - 1; i += 2)
{
    double u1 = 1.0 - rng.NextDouble();
    double u2 = rng.NextDouble();
    double mag = Math.Sqrt(-2.0 * Math.Log(u1));
    latents[i] = (float)(mag * Math.Cos(2.0 * Math.PI * u2));
    latents[i + 1] = (float)(mag * Math.Sin(2.0 * Math.PI * u2));
}

// Voice conditioning (load from .npy file in production)
float[] voiceConditioning = new float[256]; // Zero = default voice

// Compute timesteps
int numTrain = 1000;
int[] timesteps = new int[numSteps];
float stepSize = (float)numTrain / numSteps;
for (int i = 0; i < numSteps; i++)
    timesteps[i] = Math.Max((int)Math.Round(numTrain - 1 - i * stepSize), 0);

// Beta schedule
float[] betas = new float[numTrain];
float[] alphasCumprod = new float[numTrain];
for (int i = 0; i < numTrain; i++)
    betas[i] = 0.00085f + (0.012f - 0.00085f) * i / (numTrain - 1);
alphasCumprod[0] = 1.0f - betas[0];
for (int i = 1; i < numTrain; i++)
    alphasCumprod[i] = alphasCumprod[i - 1] * (1.0f - betas[i]);

// Denoising loop
for (int step = 0; step < numSteps; step++)
{
    int t = timesteps[step];

    var latentTensor = new DenseTensor<float>(latents, new[] { 1, latentDim, latentLen });
    var hiddenTensor = new DenseTensor<float>(hiddenStates,
        new[] { 1, hiddenStates.Length / latentDim, latentDim });
    var condTensor = new DenseTensor<float>(voiceConditioning, new[] { 1, 256 });
    var tsTensor = new DenseTensor<long>(new long[] { t }, new[] { 1 });

    var diffInputs = new List<NamedOnnxValue>
    {
        NamedOnnxValue.CreateFromTensor("latent_sample", latentTensor),
        NamedOnnxValue.CreateFromTensor("encoder_hidden_states", hiddenTensor),
        NamedOnnxValue.CreateFromTensor("voice_conditioning", condTensor),
        NamedOnnxValue.CreateFromTensor("timestep", tsTensor)
    };

    using var diffResults = diffusion.Run(diffInputs);
    float[] noisePred = diffResults.First().AsTensor<float>().ToArray();

    // DDPM step
    float alphaT = alphasCumprod[t];
    float alphaPrev = t > 0 ? alphasCumprod[t - 1] : 1.0f;

    for (int j = 0; j < latents.Length; j++)
    {
        float x0 = (latents[j] - MathF.Sqrt(1 - alphaT) * noisePred[j]) / MathF.Sqrt(alphaT);
        x0 = Math.Clamp(x0, -1f, 1f);
        latents[j] = MathF.Sqrt(alphaPrev) * x0 + MathF.Sqrt(1 - alphaPrev) * noisePred[j];
    }

    Console.WriteLine($"🔄 Diffusion step {step + 1}/{numSteps} (t={t})");
}

// =============================================================================
// Step 5: Acoustic Decoder → Audio
// =============================================================================
var decoderInput = new DenseTensor<float>(latents, new[] { 1, latentDim, latentLen });
var decoderInputs = new List<NamedOnnxValue>
{
    NamedOnnxValue.CreateFromTensor("latent_input", decoderInput)
};

using var decoderResults = decoder.Run(decoderInputs);
float[] audio = decoderResults.First().AsTensor<float>().ToArray();

// Clamp to valid audio range
for (int i = 0; i < audio.Length; i++)
    audio[i] = Math.Clamp(audio[i], -1f, 1f);

Console.WriteLine($"🔊 Audio: {audio.Length} samples ({audio.Length / 24000.0:F2}s @ 24kHz)");

// =============================================================================
// Step 6: Save WAV File
// =============================================================================
using var wav = new FileStream("output.wav", FileMode.Create);
using var writer = new BinaryWriter(wav);

int dataSize = audio.Length * 2; // 16-bit = 2 bytes per sample
writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
writer.Write(36 + dataSize);
writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVE"));
writer.Write(System.Text.Encoding.ASCII.GetBytes("fmt "));
writer.Write(16);            // fmt chunk size
writer.Write((short)1);      // PCM
writer.Write((short)1);      // mono
writer.Write(24000);          // sample rate
writer.Write(24000 * 2);     // byte rate
writer.Write((short)2);      // block align
writer.Write((short)16);     // bits per sample
writer.Write(System.Text.Encoding.ASCII.GetBytes("data"));
writer.Write(dataSize);

foreach (float s in audio)
    writer.Write((short)(Math.Clamp(s, -1f, 1f) * short.MaxValue));

Console.WriteLine("✅ Saved output.wav");
```

## For a Complete Implementation

The full C# inference pipeline (with proper BPE tokenizer, diffusion scheduler, voice preset loading, and SIMD-optimized math) is available at:

**[ElBruno.VibeVoiceTTS/scenario-08-onnx-native/csharp](https://github.com/elbruno/ElBruno.VibeVoiceTTS/tree/main/src/scenario-08-onnx-native/csharp)**

Key classes:
- `VibeVoiceOnnxPipeline` — Main orchestrator
- `VibeVoiceTokenizer` — Full BPE tokenizer from tokenizer.json
- `DiffusionScheduler` — DDPM noise schedule
- `VoicePresetManager` — Voice preset .npy loader
- `AudioWriter` — WAV file writer
- `TensorHelpers` — SIMD-accelerated tensor operations
