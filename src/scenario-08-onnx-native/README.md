# Scenario 8 — Native C# ONNX Inference (No Python)

Run VibeVoice TTS **entirely in C#** using ONNX Runtime. Zero Python dependency at runtime.

## Architecture

```
                    ┌─────────────────────────────────────────────────┐
                    │         C# Application (ONNX Runtime)           │
                    │                                                 │
  "Hello world" ──►│  Tokenizer ──► text_encoder.onnx                │
                    │                     │                           │
                    │              hidden states                      │
                    │                     │                           │
                    │  noise ──► diffusion_step.onnx (×5 steps)      │
                    │                     │                           │
                    │              clean latents                      │
                    │                     │                           │
                    │            acoustic_decoder.onnx ──► WAV audio  │
                    └─────────────────────────────────────────────────┘
```

## Prerequisites

- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) or later
- ONNX model files (see [Export Models](#export-models) below)

## Export Models (One-Time Setup)

The ONNX models must be exported from the PyTorch model first. This is a one-time step requiring Python.

```bash
# Set up Python environment for export
cd src/scenario-08-onnx-native/export
pip install -r requirements_export.txt

# Export model subcomponents to ONNX
python export_model.py --output ../models

# Export voice presets to .npy format
python export_voice_presets.py --output ../models/voices

# Validate export accuracy (optional)
python validate_export.py --models-dir ../models
```

## Run the C# App

```bash
cd src/scenario-08-onnx-native/csharp
dotnet run -- --text "Hello! This is VibeVoice running natively in C sharp." --voice Carter
```

### CLI Options

| Option | Default | Description |
|--------|---------|-------------|
| `--text` | *(required)* | Text to synthesize |
| `--voice` | `Carter` | Voice preset (Carter, Davis, Emma, Frank, Grace, Mike) |
| `--output` | `output.wav` | Output WAV file path |
| `--models-dir` | `../models` | Directory containing ONNX models |

## Project Structure

```
scenario-08-onnx-native/
├── README.md                           # This file
├── export/                             # Python export tools (one-time use)
│   ├── export_model.py                 # Export PyTorch → ONNX subcomponents
│   ├── export_voice_presets.py         # Convert .pt voice presets → .npy
│   ├── validate_export.py             # Compare PyTorch vs ONNX accuracy
│   └── requirements_export.txt         # Python deps for export only
├── models/                             # Exported ONNX models (gitignored)
│   └── README.md                       # How to generate model files
├── csharp/                             # C# native inference app
│   ├── VibeVoiceOnnx.csproj
│   ├── Program.cs                      # CLI entry point
│   ├── Pipeline/
│   │   ├── VibeVoiceOnnxPipeline.cs   # Main inference orchestrator
│   │   ├── VibeVoiceTokenizer.cs      # BPE text tokenization
│   │   ├── DiffusionScheduler.cs      # DDPM noise schedule
│   │   └── VoicePresetManager.cs      # Voice preset loading
│   └── Utils/
│       ├── AudioWriter.cs              # WAV file writer
│       └── TensorHelpers.cs            # Tensor math utilities
└── docs/
    └── architecture.md                 # Technical deep-dive
```

## How It Works

1. **Text Tokenization** — Input text is tokenized using the HuggingFace BPE vocabulary (ported to C#)
2. **Text Encoding** — Token IDs run through `text_encoder.onnx` (LLM backbone) producing hidden states
3. **Diffusion Loop** — Starting from random noise, `diffusion_step.onnx` is called 5 times to denoise latent audio representations, conditioned on the text encoding and voice preset
4. **Audio Decoding** — Clean latents pass through `acoustic_decoder.onnx` to produce 24kHz audio samples
5. **WAV Output** — Audio samples are written as a standard WAV file

## Key Differences from Other Scenarios

| Aspect | Previous Approach | ONNX Native (Current) |
|--------|--------------------------|--------------------------|
| Runtime dependency | Python + PyTorch | None (pure C#) |
| Model format | PyTorch weights | ONNX |
| Deployment size | ~4 GB (Python + model) | ~1 GB (ONNX models only) |
| Startup time | ~30s (Python + model load) | ~5s (ONNX session load) |
| Cross-platform | Limited by Python | Full .NET support |
| Mobile/MAUI ready | No | Yes |

## Limitations

- **Export step requires Python** — a one-time requirement to convert the model
- **Numerical precision** — small floating-point differences vs. PyTorch (~1e-4 tolerance)
- **Model size** — ~1 GB total for the ONNX models (can be reduced with INT8 quantization)
- **Tokenizer** — simplified BPE implementation; may need updates for edge-case text inputs

## GPU Acceleration

For GPU inference on Windows, use the DirectML execution provider:

```bash
dotnet add package Microsoft.ML.OnnxRuntime.DirectML
```

For NVIDIA GPUs:

```bash
dotnet add package Microsoft.ML.OnnxRuntime.Gpu
```

## Hugging Face

The exported ONNX models are published on Hugging Face for easy download:

**🤗 [elbruno/VibeVoice-Realtime-0.5B-ONNX](https://huggingface.co/elbruno/VibeVoice-Realtime-0.5B-ONNX)**

### Download from Hugging Face (Python)

```python
from huggingface_hub import snapshot_download

local_dir = snapshot_download(
    "elbruno/VibeVoice-Realtime-0.5B-ONNX",
    allow_patterns=["*.onnx", "*.json", "*.npy", "voices/**"],
)
print(f"Models downloaded to: {local_dir}")
```

### Download from Hugging Face (Git)

```bash
git lfs install
git clone https://huggingface.co/elbruno/VibeVoice-Realtime-0.5B-ONNX models
```

### Publish Updated Models

After exporting new ONNX models, upload them to Hugging Face:

```bash
cd src/scenario-08-onnx-native/huggingface
pip install huggingface_hub
huggingface-cli login
python upload_to_hf.py --repo elbruno/VibeVoice-Realtime-0.5B-ONNX --models-dir ../models
```
