# Scenario 8 — ONNX Export & Native C# Inference Pipeline

Export VibeVoice-Realtime-0.5B to ONNX for **native C# inference** via the [ElBruno.VibeVoiceTTS](../../src/ElBruno.VibeVoiceTTS/) NuGet library. Zero Python dependency at runtime.

## Architecture

The pipeline uses an **autoregressive loop with KV-cache** — the same approach as the original PyTorch model:

```
                    ┌─────────────────────────────────────────────────────────┐
                    │         C# Application (ONNX Runtime)                   │
                    │                                                         │
  "Hello world" ──►│  BpeTokenizer ──► lm_with_kv.onnx (4-layer Qwen2)     │
                    │                        │                                │
                    │                  lm_hidden_states + type_embed(text)    │
                    │                        │                                │
                    │            tts_lm_prefill.onnx (20-layer Qwen2)        │
                    │              + voice KV-cache (speaker identity)        │
                    │                        │                                │
                    │  ┌─── Autoregressive Loop (per speech frame) ──────┐   │
                    │  │  condition = tts_hidden[:, -1, :]  (896-dim)    │   │
                    │  │                     │                            │   │
                    │  │  noise ──► prediction_head.onnx (×20 diffusion) │   │
                    │  │            with CFG (pos + neg conditions)       │   │
                    │  │                     │                            │   │
                    │  │  speech_latent ──► acoustic_connector.onnx      │   │
                    │  │                     │                            │   │
                    │  │  embed + type_embed(speech) ──► tts_lm_step.onnx│   │
                    │  │                     │                            │   │
                    │  │  eos_classifier.onnx → sigmoid > 0.5 → stop     │   │
                    │  └─────────────────────────────────────────────────┘   │
                    │                        │                                │
                    │  acoustic_decoder.onnx (σ-VAE) ──► 24kHz WAV audio    │
                    └─────────────────────────────────────────────────────────┘
```

## ONNX Models

| Model | Params | Description |
|-------|--------|-------------|
| `lm_with_kv.onnx` | 196M | Language model (Qwen2, 4 layers) with KV-cache |
| `tts_lm_prefill.onnx` | 434M | TTS backbone multi-token prefill with KV-cache |
| `tts_lm_step.onnx` | 434M | TTS backbone single-token step with KV-cache |
| `prediction_head.onnx` | 42M | Diffusion head: (noisy, timestep, condition) → predicted |
| `acoustic_decoder.onnx` | 687M | σ-VAE decoder: latents → 24kHz waveform |
| `acoustic_connector.onnx` | 0.9M | Speech latent → embedding (64 → 896) |
| `eos_classifier.onnx` | 0.8M | End-of-speech classifier |
| `type_embeddings.npy` | — | Type embeddings [2, 896]: index 0=speech, 1=text |

### Voice Presets (KV-cache)

Each voice includes pre-computed KV-cache data:

```
voices/{voice_name}/
    metadata.json                  # Voice info + tensor shapes
    tts_kv_key_{0..19}.npy        # TTS-LM positive KV-cache keys (20 layers)
    tts_kv_value_{0..19}.npy      # TTS-LM positive KV-cache values
    lm_kv_key_{0..3}.npy          # LM KV-cache keys (4 layers)
    lm_kv_value_{0..3}.npy        # LM KV-cache values
    negative/
        tts_kv_key_{0..19}.npy    # TTS-LM negative KV-cache
        tts_kv_value_{0..19}.npy
```

Available voices: Carter, Davis, Emma, Frank, Grace, Mike (English only).

## Prerequisites

- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) or later
- Python 3.10+ (for one-time ONNX export only)
- ONNX model files (see export steps below, or download from HuggingFace)

## Export Models (One-Time Setup)

```bash
# Set up Python environment
cd src/scenario-08-onnx-native/export
pip install -r requirements_export.txt

# Step 1: Export base ONNX models + KV-cache models
python export_model.py --output ../models

# Step 2: Export voice presets as KV-cache .npy files
python export_voice_presets.py --output ../models/voices

# Step 3 (optional): Validate export accuracy
python validate_export.py --models-dir ../models
```

The export produces:
- 9 ONNX model files (6 base + 3 KV-cache variants)
- `type_embeddings.npy` and `tokenizer.json`
- ~184 voice preset KV-cache files (6 voices × ~30 files each)

## Download Pre-Exported Models

The models are published on HuggingFace — no Python/export step needed:

**🤗 [elbruno/VibeVoice-Realtime-0.5B-ONNX](https://huggingface.co/elbruno/VibeVoice-Realtime-0.5B-ONNX)**

```python
# Python
from huggingface_hub import snapshot_download
snapshot_download("elbruno/VibeVoice-Realtime-0.5B-ONNX",
                  allow_patterns=["*.onnx", "*.json", "*.npy", "voices/**"])
```

```bash
# Git LFS
git lfs install
git clone https://huggingface.co/elbruno/VibeVoice-Realtime-0.5B-ONNX models
```

The C# library (`ElBruno.VibeVoiceTTS`) auto-downloads models from HuggingFace on first use — no manual download needed for most users.

## Using in C#

See [scenario-03-csharp-simple](../scenario-03-csharp-simple/) for a complete example:

```csharp
using ElBruno.VibeVoiceTTS;

using var tts = new VibeVoiceSynthesizer();
await tts.EnsureModelAvailableAsync(); // auto-downloads from HuggingFace

float[] audio = await tts.GenerateAudioAsync("Hello!", "Carter");
tts.SaveWav("output.wav", audio);
```

## Project Structure

```
scenario-08-onnx-native/
├── README.md                           # This file
├── export/                             # Python export tools (one-time use)
│   ├── export_model.py                 # Export PyTorch → ONNX (base + KV-cache models)
│   ├── export_voice_presets.py         # Export voice presets → KV-cache .npy files
│   ├── validate_export.py             # Compare PyTorch vs ONNX accuracy
│   └── requirements_export.txt         # Python deps for export only
├── models/                             # Exported ONNX models (gitignored)
└── upload_models.py                    # Upload all models to HuggingFace
```

## Technical Details

### Autoregressive Pipeline

1. **Text encoding**: `lm_with_kv.onnx` processes text tokens with the voice's LM KV-cache → hidden_states
2. **Prefill**: hidden_states + type_embed(text) → `tts_lm_prefill.onnx` with TTS KV-cache → initial condition
3. **Per speech frame** (autoregressive):
   - Extract condition from last hidden state (896-dim)
   - 20-step DPMSolver++ diffusion with CFG (cfg_scale=1.5, v_prediction, cosine beta)
   - `acoustic_connector.onnx`: speech_latent (64) → embedding (896)
   - embedding + type_embed(speech) → `tts_lm_step.onnx` → updated condition + KV-cache
   - `eos_classifier.onnx`: sigmoid > 0.5 → stop generating
4. **Decode**: All speech latents → `acoustic_decoder.onnx` → 24kHz waveform

### Important Notes

- **Short text recommended**: Best results with ~10 tokens per sentence. Long text (30+ tokens) may produce artifacts — this is a model limitation, not a C# bug.
- **KV-cache is mandatory**: The static conditioning approach (without KV-cache) produces incorrect speech. The full autoregressive pipeline with KV-cache is required.
- **Opset 18**: KV-cache models require ONNX opset 18 for proper dynamic axis support.

## Key Differences from Python

| Aspect | Python (PyTorch) | C# (ONNX Native) |
|--------|-------------------|-------------------|
| Runtime dependency | Python + PyTorch | None (pure C#) |
| Model format | PyTorch weights | ONNX |
| Deployment size | ~4 GB (Python + model) | ~1.5 GB (ONNX models) |
| Startup time | ~30s (Python + model load) | ~5s (ONNX session load) |
| Cross-platform | Limited by Python | Full .NET support |
| Mobile/MAUI ready | No | Yes |

## GPU Acceleration

```bash
# Windows DirectML
dotnet add package Microsoft.ML.OnnxRuntime.DirectML

# NVIDIA CUDA
dotnet add package Microsoft.ML.OnnxRuntime.Gpu
```

## Upload to HuggingFace

After exporting new models:

```bash
pip install huggingface_hub
huggingface-cli login
python upload_models.py
```
