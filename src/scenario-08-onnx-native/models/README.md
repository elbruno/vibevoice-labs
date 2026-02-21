# ONNX Model Files

This directory stores the exported ONNX model files. They are **not committed to git** (too large).

## How to Generate

Run the export script from the repo root:

```bash
cd src/scenario-08-onnx-native/export
pip install -r requirements_export.txt
python export_model.py --output ../models
```

## Expected Files After Export

| File | Description | Approx. Size |
|------|-------------|-------------|
| `text_encoder.onnx` | LLM backbone (text → hidden states) | ~400 MB |
| `diffusion_step.onnx` | Single DDPM denoising step | ~200 MB |
| `acoustic_decoder.onnx` | Latent → waveform audio | ~100 MB |
| `tokenizer.json` | HuggingFace tokenizer vocabulary | ~2 MB |
| `voices/` | Voice preset `.npy` files | ~5 MB each |

## Quantized Models (Optional)

After export, you can quantize for smaller size and faster CPU inference:

```bash
python export_model.py --output ../models --quantize int8
```

This produces `*_int8.onnx` variants (~2-4× smaller).
