#!/usr/bin/env python3
"""
Export VibeVoice-Realtime-0.5B PyTorch model to ONNX subcomponents.

Actual model architecture (from model inspection):
  model.model.language_model      — Qwen2Model (195.8M) text encoder
  model.model.tts_language_model  — Qwen2Model (434.4M) TTS backbone
  model.model.prediction_head     — VibeVoiceDiffusionHead (42.1M) diffusion
  model.model.acoustic_tokenizer  — σ-VAE decoder (687.4M, encoder not pretrained)
  model.model.acoustic_connector  — SpeechConnector (0.9M)

Exported ONNX files:
  - text_encoder.onnx     — language_model: text → hidden states
  - prediction_head.onnx  — diffusion head: single denoising step
  - acoustic_decoder.onnx — σ-VAE decoder: latents → waveform

Usage:
    python export_model.py --output ../models
"""

import argparse
import json
import logging
import os
import sys
import time
from pathlib import Path

import numpy as np
import torch
import torch.nn as nn

# Force legacy ONNX export (PyTorch 2.x defaults to torch.export which fails
# on complex models with dynamic control flow)
os.environ["TORCH_ONNX_USE_LEGACY"] = "1"

logging.basicConfig(
    level=logging.INFO,
    format="%(asctime)s [%(levelname)s] %(message)s",
    datefmt="%H:%M:%S",
)
log = logging.getLogger(__name__)

MODEL_NAME = "microsoft/VibeVoice-Realtime-0.5B"
ONNX_OPSET = 17
SAMPLE_RATE = 24_000


# ---------------------------------------------------------------------------
# Wrapper modules for clean ONNX export interfaces
# ---------------------------------------------------------------------------

class TextEncoderWrapper(nn.Module):
    """Wraps model.model.language_model (Qwen2Model) for ONNX export."""

    def __init__(self, language_model):
        super().__init__()
        self.lm = language_model

    def forward(self, input_ids: torch.Tensor, attention_mask: torch.Tensor) -> torch.Tensor:
        outputs = self.lm(input_ids=input_ids, attention_mask=attention_mask)
        if hasattr(outputs, "last_hidden_state"):
            return outputs.last_hidden_state
        if isinstance(outputs, (tuple, list)):
            return outputs[0]
        return outputs


class PredictionHeadWrapper(nn.Module):
    """Wraps model.model.prediction_head (VibeVoiceDiffusionHead) for single step export."""

    def __init__(self, prediction_head):
        super().__init__()
        self.head = prediction_head

    def forward(self, noisy_latent: torch.Tensor, timestep: torch.Tensor,
                conditioning: torch.Tensor) -> torch.Tensor:
        # Cast timestep to float — internal linear layers expect float, not int64
        return self.head(noisy_latent, timestep.float(), conditioning)


class AcousticDecoderWrapper(nn.Module):
    """Wraps the decoder part of model.model.acoustic_tokenizer for ONNX export."""

    def __init__(self, acoustic_tokenizer):
        super().__init__()
        # The acoustic_tokenizer has .encoder and .decoder
        if hasattr(acoustic_tokenizer, 'decoder'):
            self.dec = acoustic_tokenizer.decoder
            log.info("AcousticDecoderWrapper: using acoustic_tokenizer.decoder")
        elif hasattr(acoustic_tokenizer, 'quantizer') and hasattr(acoustic_tokenizer, 'decoder'):
            self.dec = acoustic_tokenizer.decoder
        else:
            # Fallback: use the whole tokenizer (it may have a decode method)
            self.dec = acoustic_tokenizer
            log.warning("AcousticDecoderWrapper: using full acoustic_tokenizer as decoder")

    def forward(self, latent: torch.Tensor) -> torch.Tensor:
        return self.dec(latent)


# ---------------------------------------------------------------------------
# Export helpers
# ---------------------------------------------------------------------------

def _export_onnx(
    module: nn.Module,
    dummy_inputs: tuple,
    input_names: list[str],
    output_names: list[str],
    dynamic_axes: dict,
    output_path: Path,
    component_name: str,
) -> bool:
    """Export a single component to ONNX using legacy exporter. Returns True on success."""
    log.info("Exporting %s → %s", component_name, output_path)
    t0 = time.perf_counter()
    try:
        torch.onnx.export(
            module,
            dummy_inputs,
            str(output_path),
            opset_version=ONNX_OPSET,
            input_names=input_names,
            output_names=output_names,
            dynamic_axes=dynamic_axes,
            do_constant_folding=True,
        )
        elapsed = time.perf_counter() - t0
        size_mb = output_path.stat().st_size / (1024 * 1024)
        log.info("  ✓ %s exported in %.1fs (%.1f MB)", component_name, elapsed, size_mb)
        return True
    except Exception as exc:
        log.error("  ✗ %s export failed: %s", component_name, exc, exc_info=True)
        return False


def _quantize_model(onnx_path: Path, quant_type: str) -> Path:
    """Apply post-training quantization."""
    from onnxruntime.quantization import quantize_dynamic, QuantType
    qtype = QuantType.QInt8 if quant_type == "int8" else QuantType.QUInt8
    out_path = onnx_path.with_name(f"{onnx_path.stem}_{quant_type}.onnx")
    log.info("Quantizing %s → %s (%s)", onnx_path.name, out_path.name, quant_type)
    quantize_dynamic(str(onnx_path), str(out_path), weight_type=qtype)
    size_mb = out_path.stat().st_size / (1024 * 1024)
    log.info("  ✓ Quantized model: %.1f MB", size_mb)
    return out_path


# ---------------------------------------------------------------------------
# Main export pipeline
# ---------------------------------------------------------------------------

def load_model(device: str):
    """Load the VibeVoice model and processor from HuggingFace."""
    from vibevoice.modular.modeling_vibevoice_streaming_inference import (
        VibeVoiceStreamingForConditionalGenerationInference,
    )
    from vibevoice.processor.vibevoice_streaming_processor import (
        VibeVoiceStreamingProcessor,
    )

    log.info("Loading processor from %s …", MODEL_NAME)
    processor = VibeVoiceStreamingProcessor.from_pretrained(MODEL_NAME)

    log.info("Loading model from %s (fp32, sdpa) …", MODEL_NAME)
    model = VibeVoiceStreamingForConditionalGenerationInference.from_pretrained(
        MODEL_NAME,
        torch_dtype=torch.float32,
        attn_implementation="eager",  # eager is most compatible with ONNX tracing
        device_map=device,
    )
    model.eval()
    log.info("Model loaded successfully on %s", device)

    return model, processor


def _dump_model_structure(model, output_dir: Path):
    """Write model architecture summary for debugging."""
    info_path = output_dir / "model_structure.txt"
    lines = ["VibeVoice model structure\n", "=" * 60 + "\n\n"]
    lines.append("Top-level children:\n")
    for name, child in model.named_children():
        params = sum(p.numel() for p in child.parameters()) / 1e6
        lines.append(f"  {name}: {type(child).__name__} ({params:.1f}M params)\n")
    lines.append("\nmodel.model children:\n")
    for name, child in model.model.named_children():
        params = sum(p.numel() for p in child.parameters()) / 1e6
        lines.append(f"  {name}: {type(child).__name__} ({params:.1f}M params)\n")
    lines.append("\nAll named modules (first 300):\n")
    for i, (name, mod) in enumerate(model.named_modules()):
        if i >= 300:
            lines.append("  … (truncated)\n")
            break
        lines.append(f"  {name}: {type(mod).__name__}\n")
    info_path.write_text("".join(lines))
    log.info("Model structure dumped to %s", info_path)


def export_text_encoder(model, processor, output_dir: Path, device: str) -> bool:
    """Export model.model.language_model to ONNX."""
    lm = model.model.language_model
    wrapper = TextEncoderWrapper(lm)
    wrapper.eval()

    seq_len = 64
    input_ids = torch.randint(0, processor.tokenizer.vocab_size, (1, seq_len), device=device)
    attention_mask = torch.ones(1, seq_len, dtype=torch.long, device=device)

    return _export_onnx(
        module=wrapper,
        dummy_inputs=(input_ids, attention_mask),
        input_names=["input_ids", "attention_mask"],
        output_names=["hidden_states"],
        dynamic_axes={
            "input_ids": {0: "batch", 1: "seq_len"},
            "attention_mask": {0: "batch", 1: "seq_len"},
            "hidden_states": {0: "batch", 1: "seq_len"},
        },
        output_path=output_dir / "text_encoder.onnx",
        component_name="text_encoder (language_model)",
    )


def export_prediction_head(model, output_dir: Path, device: str) -> bool:
    """Export model.model.prediction_head (diffusion head) to ONNX."""
    head = model.model.prediction_head
    wrapper = PredictionHeadWrapper(head)
    wrapper.eval()

    cfg = model.config.diffusion_head_config
    latent_size = cfg.latent_size  # 64
    hidden_size = cfg.hidden_size  # 896
    seq_len = 64

    noisy_latent = torch.randn(1, latent_size, device=device)
    timestep = torch.tensor([500], dtype=torch.long, device=device)
    conditioning = torch.randn(1, seq_len, hidden_size, device=device)

    return _export_onnx(
        module=wrapper,
        dummy_inputs=(noisy_latent, timestep, conditioning),
        input_names=["noisy_latent", "timestep", "conditioning"],
        output_names=["predicted_noise"],
        dynamic_axes={
            "noisy_latent": {0: "batch"},
            "timestep": {0: "batch"},
            "conditioning": {0: "batch", 1: "seq_len"},
            "predicted_noise": {0: "batch"},
        },
        output_path=output_dir / "prediction_head.onnx",
        component_name="prediction_head (diffusion)",
    )


def export_acoustic_decoder(model, output_dir: Path, device: str) -> bool:
    """Export the decoder from model.model.acoustic_tokenizer to ONNX."""
    tokenizer = model.model.acoustic_tokenizer
    wrapper = AcousticDecoderWrapper(tokenizer)
    wrapper.eval()

    cfg = model.config.diffusion_head_config
    latent_size = cfg.speech_vae_dim  # 64

    latent = torch.randn(1, latent_size, 50, device=device)  # (B, C, T)

    return _export_onnx(
        module=wrapper,
        dummy_inputs=(latent,),
        input_names=["latent"],
        output_names=["waveform"],
        dynamic_axes={
            "latent": {0: "batch", 2: "time"},
            "waveform": {0: "batch"},
        },
        output_path=output_dir / "acoustic_decoder.onnx",
        component_name="acoustic_decoder (σ-VAE)",
    )


def save_tokenizer(processor, output_dir: Path):
    """Save tokenizer config for C# reimplementation."""
    tok_dir = output_dir / "tokenizer"
    tok_dir.mkdir(parents=True, exist_ok=True)
    log.info("Saving tokenizer to %s", tok_dir)
    processor.tokenizer.save_pretrained(str(tok_dir))

    tokenizer_json = tok_dir / "tokenizer.json"
    if tokenizer_json.exists():
        import shutil
        shutil.copy2(tokenizer_json, output_dir / "tokenizer.json")
        log.info("  ✓ tokenizer.json copied to %s", output_dir / "tokenizer.json")
    else:
        log.warning("  tokenizer.json not found in saved pretrained output")


def save_config_metadata(model, output_dir: Path):
    """Save model config as JSON for C# to read dimensions."""
    cfg = model.config
    diff_cfg = cfg.diffusion_head_config
    metadata = {
        "model_name": MODEL_NAME,
        "sample_rate": SAMPLE_RATE,
        "hidden_size": diff_cfg.hidden_size,
        "latent_size": diff_cfg.latent_size,
        "speech_vae_dim": diff_cfg.speech_vae_dim,
        "head_layers": diff_cfg.head_layers,
        "ddpm_num_steps": diff_cfg.ddpm_num_steps,
        "ddpm_num_inference_steps": diff_cfg.ddpm_num_inference_steps,
        "prediction_type": diff_cfg.prediction_type,
        "ddpm_beta_schedule": diff_cfg.ddpm_beta_schedule,
        "tts_backbone_num_hidden_layers": cfg.tts_backbone_num_hidden_layers,
        "onnx_opset": ONNX_OPSET,
    }
    meta_path = output_dir / "model_config.json"
    with open(meta_path, "w") as f:
        json.dump(metadata, f, indent=2)
    log.info("Model config saved to %s", meta_path)


def main():
    parser = argparse.ArgumentParser(
        description="Export VibeVoice-Realtime-0.5B to ONNX subcomponents",
    )
    parser.add_argument("--output", type=str, default="../models",
                        help="Output directory for ONNX files")
    parser.add_argument("--quantize", type=str, choices=["int8", "uint8"], default=None,
                        help="Post-training dynamic quantization type")
    parser.add_argument("--device", type=str, default="cpu", choices=["cpu", "cuda"],
                        help="Device to load model on")
    args = parser.parse_args()

    if args.device == "cuda" and not torch.cuda.is_available():
        log.warning("CUDA not available — falling back to CPU")
        args.device = "cpu"

    output_dir = Path(args.output).resolve()
    output_dir.mkdir(parents=True, exist_ok=True)
    log.info("Output directory: %s", output_dir)

    model, processor = load_model(args.device)
    _dump_model_structure(model, output_dir)
    save_config_metadata(model, output_dir)

    results: dict[str, bool] = {}
    with torch.no_grad():
        results["text_encoder"] = export_text_encoder(model, processor, output_dir, args.device)
        results["prediction_head"] = export_prediction_head(model, output_dir, args.device)
        results["acoustic_decoder"] = export_acoustic_decoder(model, output_dir, args.device)

    save_tokenizer(processor, output_dir)

    if args.quantize:
        for name in results:
            onnx_path = output_dir / f"{name}.onnx"
            if onnx_path.exists():
                try:
                    _quantize_model(onnx_path, args.quantize)
                except Exception as exc:
                    log.error("Quantization failed for %s: %s", name, exc)

    log.info("")
    log.info("═" * 50)
    log.info("Export Summary")
    log.info("═" * 50)
    for name, ok in results.items():
        status = "✓ SUCCESS" if ok else "✗ FAILED"
        log.info("  %s : %s", name.ljust(20), status)
    log.info("═" * 50)

    if all(results.values()):
        log.info("All components exported successfully!")
    else:
        log.warning("Some exports failed. Check model_structure.txt for details.")
        sys.exit(1)


if __name__ == "__main__":
    main()
