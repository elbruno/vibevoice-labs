---
language:
  - en
tags:
  - onnx
  - text-to-speech
  - tts
  - vibevoice
  - microsoft
  - diffusion
  - streaming
  - realtime
  - speech-synthesis
license: mit
library_name: onnxruntime
pipeline_tag: text-to-speech
base_model: microsoft/VibeVoice-Realtime-0.5B
datasets:
  - librispeech_asr
model-index:
  - name: VibeVoice-Realtime-0.5B-ONNX
    results:
      - task:
          type: text-to-speech
          name: Text-to-Speech
        dataset:
          name: LibriSpeech test-clean
          type: librispeech_asr
          split: test
        metrics:
          - name: WER
            type: wer
            value: 2.00
          - name: Speaker Similarity
            type: speaker_similarity
            value: 0.695
---

# VibeVoice-Realtime-0.5B — ONNX

> ONNX export of Microsoft's [VibeVoice-Realtime-0.5B](https://huggingface.co/microsoft/VibeVoice-Realtime-0.5B) text-to-speech model for **native C# / .NET / cross-platform inference** without Python.

This repository contains the VibeVoice-Realtime-0.5B model exported to ONNX format as three subcomponents. It enables running VibeVoice TTS inference using [ONNX Runtime](https://onnxruntime.ai/) in **C#, Python, C++, Java, JavaScript**, or any language with an ONNX Runtime binding — no PyTorch or Python required at runtime.

📦 **Source code & examples:** [github.com/elbruno/vibevoice-labs](https://github.com/elbruno/vibevoice-labs) (see `src/scenario-08-onnx-native/`)

## Model Overview

| Property | Value |
|----------|-------|
| **Original model** | [microsoft/VibeVoice-Realtime-0.5B](https://huggingface.co/microsoft/VibeVoice-Realtime-0.5B) |
| **Parameters** | ~0.5B |
| **Format** | ONNX (opset 17) |
| **License** | MIT |
| **Audio output** | 24 kHz, mono, 16-bit PCM |
| **First audible latency** | ~300 ms (hardware dependent) |
| **Voices** | 6 English presets (Carter, Davis, Emma, Frank, Grace, Mike) |
| **Languages** | English (primary), experimental multilingual |
| **GitHub repo** | [elbruno/vibevoice-labs](https://github.com/elbruno/vibevoice-labs) |

## Architecture — Three ONNX Subcomponents

VibeVoice uses a diffusion-based architecture that cannot be exported as a single ONNX graph (the denoising loop is iterative). Instead, the model is split into three stages:

```
Text → [Tokenize] → text_encoder.onnx → hidden states
                                           ↓
Noise → diffusion_step.onnx (×5 steps) → clean latents
                                           ↓
               acoustic_decoder.onnx → 24kHz WAV audio
```

| File | Description | Approx. Size |
|------|-------------|-------------|
| `text_encoder.onnx` | LLM backbone (Qwen2.5) — text tokens → hidden states | ~400 MB |
| `diffusion_step.onnx` | Single DDPM denoising step — called iteratively | ~200 MB |
| `acoustic_decoder.onnx` | σ-VAE decoder — latents → 24kHz waveform | ~100 MB |
| `tokenizer.json` | HuggingFace BPE tokenizer vocabulary | ~2 MB |
| `voices/` | 6 English voice presets (.npy format) | ~5 MB each |

## Quick Start — Python (onnxruntime)

```python
import onnxruntime as ort
import numpy as np
from huggingface_hub import hf_hub_download

# Download model files
repo_id = "elbruno/VibeVoice-Realtime-0.5B-ONNX"
text_encoder_path = hf_hub_download(repo_id, "text_encoder.onnx")
diffusion_path = hf_hub_download(repo_id, "diffusion_step.onnx")
decoder_path = hf_hub_download(repo_id, "acoustic_decoder.onnx")

# Load ONNX sessions
text_encoder = ort.InferenceSession(text_encoder_path)
diffusion = ort.InferenceSession(diffusion_path)
decoder = ort.InferenceSession(decoder_path)

# Run inference (see example_inference.py for full pipeline)
print("✅ All ONNX models loaded successfully!")
print(f"Text encoder inputs: {[i.name for i in text_encoder.get_inputs()]}")
print(f"Diffusion inputs: {[i.name for i in diffusion.get_inputs()]}")
print(f"Decoder inputs: {[i.name for i in decoder.get_inputs()]}")
```

## Quick Start — C# (.NET / ONNX Runtime)

```csharp
using Microsoft.ML.OnnxRuntime;

// Load ONNX models (download from HuggingFace or local path)
using var textEncoder = new InferenceSession("text_encoder.onnx");
using var diffusion = new InferenceSession("diffusion_step.onnx");
using var decoder = new InferenceSession("acoustic_decoder.onnx");

Console.WriteLine("✅ All ONNX models loaded!");
// See example_csharp.md for the full inference pipeline
```

**NuGet package:** `Microsoft.ML.OnnxRuntime` (1.17+)

For the complete C# inference pipeline with tokenizer, diffusion scheduler, and audio output, see: [vibevoice-labs/scenario-08-onnx-native](https://github.com/elbruno/vibevoice-labs/tree/main/src/scenario-08-onnx-native/csharp)

## How This Was Created

The ONNX files were exported from the original PyTorch model using `torch.onnx.export()` with opset version 17. Each subcomponent was traced and exported individually:

1. **Text Encoder** — The LLM backbone (Qwen2.5-based) wrapped as a standalone module
2. **Diffusion Step** — A single denoising step of the DDPM head, exported with timestep and conditioning inputs
3. **Acoustic Decoder** — The σ-VAE decoder that converts latent representations to audio waveforms

Voice presets were converted from PyTorch `.pt` tensors to NumPy `.npy` format.

Export scripts: [vibevoice-labs/scenario-08-onnx-native/export](https://github.com/elbruno/vibevoice-labs/tree/main/src/scenario-08-onnx-native/export)

## Inference Pipeline

The inference pipeline (implemented in your language of choice) follows these steps:

1. **Tokenize** — Encode input text to BPE token IDs using `tokenizer.json`
2. **Text Encoder** — Run `text_encoder.onnx` to get hidden states
3. **Diffusion Loop** — Starting from Gaussian noise, run `diffusion_step.onnx` for 5 iterations (DDPM denoising), conditioned on hidden states + voice preset
4. **Acoustic Decoder** — Run `acoustic_decoder.onnx` to convert clean latents to 24kHz audio
5. **Save WAV** — Write float audio samples as 16-bit PCM WAV

## Voice Presets

| Voice | Gender | Style |
|-------|--------|-------|
| Carter | Male | Clear American English |
| Davis | Male | Warm tone |
| Emma | Female | Clear articulation |
| Frank | Male | Deep voice |
| Grace | Female | Soft, natural |
| Mike | Male | Conversational |

## Evaluation Results

Results from the original model (from [microsoft/VibeVoice-Realtime-0.5B](https://huggingface.co/microsoft/VibeVoice-Realtime-0.5B)):

### LibriSpeech test-clean

| Model | WER (%) ↓ | Speaker Similarity ↑ |
|-------|-----------|---------------------|
| VALL-E 2 | 2.40 | 0.643 |
| Voicebox | 1.90 | 0.662 |
| **VibeVoice-Realtime-0.5B** | **2.00** | **0.695** |

### SEED test-en

| Model | WER (%) ↓ | Speaker Similarity ↑ |
|-------|-----------|---------------------|
| MaskGCT | 2.62 | 0.714 |
| CosyVoice2 | 2.57 | 0.652 |
| **VibeVoice-Realtime-0.5B** | **2.05** | **0.633** |

> **Note:** ONNX conversion may introduce small numerical differences (~1e-4 tolerance). Benchmark results should be verified independently on the ONNX variant.

## Responsible Usage

> **This section is reproduced from the [original model card](https://huggingface.co/microsoft/VibeVoice-Realtime-0.5B) per Microsoft's responsible AI guidelines.**

### Intended Uses

The VibeVoice-Realtime model is intended for **research purposes** exploring real-time highly realistic audio generation as detailed in the [technical report](https://arxiv.org/pdf/2508.19205).

### Out-of-Scope Uses

This release is **NOT** intended or licensed for:

- **Voice impersonation** without explicit, recorded consent — including cloning a real individual's voice for satire, advertising, ransom, social engineering, or authentication bypass
- **Disinformation or impersonation** — creating audio presented as genuine recordings of real people or events
- **Real-time voice conversion** — telephone or video-conference "live deep-fake" applications
- **Circumventing safeguards** — any act to disable watermarking, AI disclaimers, or security controls
- **Unsupported languages** — the model is trained only on English data; outputs in other languages are unsupported
- **Non-speech audio** — music, Foley, or ambient sound generation

### Safety Mitigations

Microsoft has implemented the following safeguards:
- **Removed acoustic tokenizer** to prevent users from creating voice embeddings for cloning
- **Audible AI disclaimer** automatically embedded in every synthesized audio file
- **Imperceptible watermark** added to generated audio for provenance verification

### Recommendation

We do not recommend using VibeVoice in commercial or real-world applications without further testing and development. If you use this model to generate speech, **please disclose to the end user that they are listening to AI-generated content**.

## Limitations

- **ONNX-specific:** Small numerical differences (~1e-4) compared to PyTorch inference
- **English only:** Other languages may produce unpredictable results
- **No overlapping speech:** Does not model or generate overlapping speech
- **No code/formulas:** Cannot read code, mathematical formulas, or uncommon symbols
- **Single speaker:** For multi-speaker, use [VibeVoice-1.5B](https://huggingface.co/microsoft/VibeVoice-1.5B)

## Technical Details

- **LLM Backbone:** [Qwen2.5-0.5B](https://huggingface.co/Qwen/Qwen2.5-0.5B)
- **Acoustic Tokenizer:** σ-VAE variant (from [LatentLM](https://arxiv.org/pdf/2412.08635)), ~340M parameters decoder
- **Diffusion Head:** 4 layers, ~40M parameters, DDPM with DPM-Solver inference
- **Context Length:** Up to 8,192 tokens
- **Frame Rate:** 7.5 Hz (ultra-low for efficiency)
- **ONNX Opset:** 17
- **Precision:** float32

## Citation

```bibtex
@article{vibevoice2025,
  title={VibeVoice Technical Report},
  author={Microsoft Research},
  journal={arXiv preprint arXiv:2508.19205},
  year={2025},
  url={https://arxiv.org/abs/2508.19205}
}
```

## Links

- 📄 **Technical Report:** [arXiv:2508.19205](https://arxiv.org/abs/2508.19205)
- 🏠 **Project Page:** [microsoft.github.io/VibeVoice](https://microsoft.github.io/VibeVoice)
- 💻 **Source Code:** [github.com/microsoft/VibeVoice](https://github.com/microsoft/VibeVoice)
- 🔧 **Export Tools:** [vibevoice-labs/scenario-08-onnx-native](https://github.com/elbruno/vibevoice-labs/tree/main/src/scenario-08-onnx-native)
- 📦 **Original Model:** [microsoft/VibeVoice-Realtime-0.5B](https://huggingface.co/microsoft/VibeVoice-Realtime-0.5B)

## Contact

For issues with the ONNX conversion, open an issue at [vibevoice-labs](https://github.com/elbruno/vibevoice-labs/issues).

For issues with the original VibeVoice model, contact [VibeVoice@microsoft.com](mailto:VibeVoice@microsoft.com).

---

*This is a derivative work. The original VibeVoice model is © Microsoft Corporation, licensed under MIT.*
