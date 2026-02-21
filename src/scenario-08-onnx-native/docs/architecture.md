# Scenario 8 — Technical Architecture

## Overview

This scenario exports the VibeVoice-Realtime-0.5B model from PyTorch to ONNX subcomponents, then runs inference entirely in C# using ONNX Runtime.

## Model Architecture Breakdown

VibeVoice is a multi-component generative TTS model:

```
┌─────────────────────────────────────────────────────────────────┐
│                    VibeVoice-Realtime-0.5B                       │
│                                                                 │
│  ┌──────────────────┐   ┌──────────────────┐                    │
│  │  Text Tokenizer   │   │  Voice Presets    │                    │
│  │  (BPE vocabulary) │   │  (.pt → .npy)     │                    │
│  └────────┬─────────┘   └────────┬─────────┘                    │
│           │                       │                              │
│  ┌────────▼──────────────────────▼──────────┐                    │
│  │         LLM Backbone (Qwen2.5)            │ → text_encoder.onnx│
│  │         Text → Hidden States              │                    │
│  └────────────────────┬─────────────────────┘                    │
│                       │                                          │
│  ┌────────────────────▼─────────────────────┐                    │
│  │         Diffusion Head (DDPM)             │ → diffusion_step.onnx
│  │         Iterative Denoising (5 steps)     │   (called per step)│
│  │         Noise → Clean Latents             │                    │
│  └────────────────────┬─────────────────────┘                    │
│                       │                                          │
│  ┌────────────────────▼─────────────────────┐                    │
│  │         Acoustic Decoder (σ-VAE)          │ → acoustic_decoder.onnx
│  │         Latents → 24kHz Waveform          │                    │
│  └──────────────────────────────────────────┘                    │
└─────────────────────────────────────────────────────────────────┘
```

## Why Subcomponent Export?

The diffusion head uses an **iterative denoising loop** (DDPM with DPM-Solver). This loop cannot be represented as a single ONNX graph because:

1. The loop has variable iterations (configurable steps)
2. Each step depends on the output of the previous step
3. The noise scheduler logic (timestep computation, alpha/beta schedules) is algorithmic, not neural

**Solution:** Export the single denoising step as one ONNX model, and implement the loop in C#.

## ONNX Export Strategy

### Text Encoder
- **Input:** `input_ids` (int64, shape [1, seq_len]), `attention_mask` (int64, shape [1, seq_len])
- **Output:** `hidden_states` (float32, shape [1, seq_len, hidden_dim])
- **Export method:** `torch.onnx.export` with traced model

### Diffusion Step
- **Input:** `noisy_latent` (float32), `timestep` (int64), `conditioning` (float32)
- **Output:** `predicted_noise` (float32, same shape as noisy_latent)
- **Export method:** `torch.onnx.export` with wrapper module

### Acoustic Decoder
- **Input:** `latent` (float32, shape [1, latent_dim, latent_seq_len])
- **Output:** `audio` (float32, shape [1, audio_samples])
- **Export method:** `torch.onnx.export` with traced decoder

## C# Pipeline Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                   VibeVoiceOnnxPipeline                       │
│                                                             │
│  ┌─────────────────┐                                         │
│  │ VibeVoiceTokenizer│  tokenizer.json → BPE encode          │
│  └────────┬────────┘                                         │
│           │ int[] tokenIds                                   │
│  ┌────────▼────────┐                                         │
│  │ InferenceSession │  text_encoder.onnx                     │
│  │ (Text Encoder)   │  ONNX Runtime session                  │
│  └────────┬────────┘                                         │
│           │ float[] hiddenStates                             │
│  ┌────────▼────────┐  ┌──────────────────┐                   │
│  │ InferenceSession │  │ DiffusionScheduler│                   │
│  │ (Diffusion Step) │◄─┤ Timesteps, alphas │                   │
│  │ × N iterations   │  │ Step() method     │                   │
│  └────────┬────────┘  └──────────────────┘                   │
│           │ float[] cleanLatents                             │
│  ┌────────▼────────┐  ┌──────────────────┐                   │
│  │ InferenceSession │  │ VoicePresetManager│                   │
│  │ (Acoustic Decoder│◄─┤ .npy file loader  │                   │
│  └────────┬────────┘  └──────────────────┘                   │
│           │ float[] audioSamples                             │
│  ┌────────▼────────┐                                         │
│  │   AudioWriter    │  → output.wav (24kHz, 16-bit PCM)      │
│  └─────────────────┘                                         │
└─────────────────────────────────────────────────────────────┘
```

## Voice Preset Format

Original PyTorch presets are dictionaries of tensors. They are converted to:

```
models/voices/
├── manifest.json           # Maps voice names → tensor files
├── Carter/
│   ├── speaker_embedding.npy
│   ├── style_embedding.npy
│   └── ...
├── Emma/
│   └── ...
```

The `.npy` format (NumPy binary) is simple to parse in C#: magic bytes + header + raw float32 data.

## Performance Considerations

| Metric | Estimated | Notes |
|--------|-----------|-------|
| Model load time | ~3-5s | ONNX session initialization |
| Inference time (CPU) | ~2-5s per sentence | Depends on text length |
| Inference time (GPU) | ~0.5-1s per sentence | With DirectML or CUDA EP |
| Memory usage | ~1-2 GB | Model weights in memory |
| Disk usage | ~700 MB - 1 GB | ONNX model files |

## Quantization Options

INT8 quantization can reduce model size by 2-4× with minimal quality loss:

```bash
python export_model.py --output ../models --quantize int8
```

| Format | Size | Quality | Speed |
|--------|------|---------|-------|
| FP32 (default) | ~1 GB | Best | Baseline |
| INT8 | ~250-500 MB | Good (~98% quality) | 1.5-2× faster on CPU |
