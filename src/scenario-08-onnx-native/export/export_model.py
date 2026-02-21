#!/usr/bin/env python3
"""
Export VibeVoice-Realtime-0.5B PyTorch model to ONNX subcomponents.

This is a ONE-TIME tool for converting the HuggingFace PyTorch model into
three ONNX subgraphs that can be loaded independently by ONNX Runtime in C#:
  - text_encoder.onnx   — LLM backbone (tokenized text → hidden states)
  - diffusion_step.onnx — single DDPM denoising step
  - acoustic_decoder.onnx — latent codes → 24 kHz waveform

Usage:
    python export_model.py --output ../models
    python export_model.py --output ../models --quantize int8 --device cuda
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
# Wrapper modules – used when direct sub-module access is not straightforward
# ---------------------------------------------------------------------------

class TextEncoderWrapper(nn.Module):
    """Wraps the LLM backbone to expose a clean (input_ids, attention_mask) → hidden_states interface."""

    def __init__(self, model):
        super().__init__()
        # The VibeVoice model is built on Qwen2.5.  Typical attribute path
        # is model.model (the base transformer) or model.language_model.
        # We try several known paths and fall back gracefully.
        self._inner = None
        for attr_path in ("model.model", "model", "language_model", "transformer"):
            obj = model
            try:
                for part in attr_path.split("."):
                    obj = getattr(obj, part)
                self._inner = obj
                log.info("TextEncoderWrapper: resolved backbone via '%s'", attr_path)
                break
            except AttributeError:
                continue

        if self._inner is None:
            # TODO: If none of the known paths work with a future model
            # revision, inspect `model.named_children()` and update.
            raise RuntimeError(
                "Could not locate the LLM backbone inside the model. "
                "Available top-level children: "
                + ", ".join(n for n, _ in model.named_children())
            )

    def forward(self, input_ids: torch.Tensor, attention_mask: torch.Tensor) -> torch.Tensor:
        outputs = self._inner(input_ids=input_ids, attention_mask=attention_mask)
        # HuggingFace models return BaseModelOutput or similar
        if hasattr(outputs, "last_hidden_state"):
            return outputs.last_hidden_state
        if isinstance(outputs, (tuple, list)):
            return outputs[0]
        return outputs


class DiffusionStepWrapper(nn.Module):
    """Wraps a single denoising step of the DDPM diffusion head.

    Inputs:
        noisy_latent  – (B, latent_dim) or (B, C, T) noisy latent
        timestep      – (B,) integer timestep
        conditioning  – (B, seq_len, hidden_dim) encoder hidden states

    Output:
        predicted noise or denoised latent (same shape as noisy_latent)
    """

    def __init__(self, model):
        super().__init__()
        self._diffusion = None
        # Try common attribute names for the diffusion head
        for attr_name in ("ddpm", "diffusion_head", "diffusion", "denoise_head",
                          "noise_predictor", "ddpm_head"):
            if hasattr(model, attr_name):
                self._diffusion = getattr(model, attr_name)
                log.info("DiffusionStepWrapper: resolved via '%s'", attr_name)
                break

        if self._diffusion is None:
            # Fallback: search children for modules whose name contains 'diffus' or 'ddpm'
            for name, child in model.named_modules():
                lower = name.lower()
                if "diffus" in lower or "ddpm" in lower or "denois" in lower:
                    self._diffusion = child
                    log.info("DiffusionStepWrapper: resolved via named_module '%s'", name)
                    break

        if self._diffusion is None:
            # TODO: Manually inspect model architecture and update paths.
            raise RuntimeError(
                "Could not locate diffusion head. "
                "Available top-level children: "
                + ", ".join(n for n, _ in model.named_children())
            )

    def forward(
        self,
        noisy_latent: torch.Tensor,
        timestep: torch.Tensor,
        conditioning: torch.Tensor,
    ) -> torch.Tensor:
        return self._diffusion(noisy_latent, timestep, conditioning)


class AcousticDecoderWrapper(nn.Module):
    """Wraps the σ-VAE decoder that converts clean latents to a waveform.

    Input:  latent – (B, latent_dim) or (B, C, T)
    Output: waveform – (B, 1, num_samples) at 24 kHz
    """

    def __init__(self, model):
        super().__init__()
        self._decoder = None
        for attr_name in ("acoustic_decoder", "vae_decoder", "decoder",
                          "vocoder", "audio_decoder", "sigma_vae"):
            if hasattr(model, attr_name):
                self._decoder = getattr(model, attr_name)
                log.info("AcousticDecoderWrapper: resolved via '%s'", attr_name)
                break

        if self._decoder is None:
            for name, child in model.named_modules():
                lower = name.lower()
                if "decoder" in lower or "vocoder" in lower or "vae" in lower:
                    self._decoder = child
                    log.info("AcousticDecoderWrapper: resolved via named_module '%s'", name)
                    break

        if self._decoder is None:
            # TODO: Manually inspect model architecture and update paths.
            raise RuntimeError(
                "Could not locate acoustic decoder. "
                "Available top-level children: "
                + ", ".join(n for n, _ in model.named_children())
            )

    def forward(self, latent: torch.Tensor) -> torch.Tensor:
        return self._decoder(latent)


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
    """Export a single component to ONNX.  Returns True on success."""
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
    """Apply post-training quantization and write a new file.  Returns path."""
    from onnxruntime.quantization import quantize_dynamic, QuantType

    qtype = QuantType.QInt8 if quant_type == "int8" else QuantType.QUInt8
    stem = onnx_path.stem
    out_path = onnx_path.with_name(f"{stem}_{quant_type}.onnx")
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

    dtype = torch.float32  # ONNX export requires fp32
    attn_impl = "sdpa"     # flash_attention_2 unsupported in tracing

    log.info("Loading model from %s (dtype=%s, attn=%s) …", MODEL_NAME, dtype, attn_impl)
    model = VibeVoiceStreamingForConditionalGenerationInference.from_pretrained(
        MODEL_NAME,
        torch_dtype=dtype,
        attn_implementation=attn_impl,
        device_map=device,
    )
    model.eval()
    model.set_ddpm_inference_steps(num_steps=5)
    log.info("Model loaded successfully on %s", device)

    return model, processor


def _dump_model_structure(model, output_dir: Path):
    """Write model architecture summary for debugging."""
    info_path = output_dir / "model_structure.txt"
    lines = ["VibeVoice model structure\n", "=" * 60 + "\n\n"]
    lines.append("Top-level children:\n")
    for name, child in model.named_children():
        lines.append(f"  {name}: {type(child).__name__}\n")
    lines.append("\nAll named modules (first 200):\n")
    for i, (name, mod) in enumerate(model.named_modules()):
        if i >= 200:
            lines.append("  … (truncated)\n")
            break
        lines.append(f"  {name}: {type(mod).__name__}\n")
    info_path.write_text("".join(lines))
    log.info("Model structure dumped to %s", info_path)


def export_text_encoder(model, processor, output_dir: Path, device: str) -> bool:
    """Export the LLM backbone to ONNX."""
    try:
        wrapper = TextEncoderWrapper(model)
        wrapper.eval()
    except RuntimeError as exc:
        log.error("Cannot create TextEncoderWrapper: %s", exc)
        _dump_model_structure(model, output_dir)
        return False

    # Dummy inputs: batch=1, seq_len=64
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
        component_name="text_encoder",
    )


def export_diffusion_step(model, output_dir: Path, device: str) -> bool:
    """Export a single DDPM denoising step to ONNX."""
    try:
        wrapper = DiffusionStepWrapper(model)
        wrapper.eval()
    except RuntimeError as exc:
        log.error("Cannot create DiffusionStepWrapper: %s", exc)
        _dump_model_structure(model, output_dir)
        return False

    # Estimate dimensions from model config or use defaults
    # TODO: Refine these dimensions after inspecting the actual model config
    latent_dim = getattr(model.config, "latent_dim", 128)
    hidden_dim = getattr(model.config, "hidden_size", 896)  # Qwen2.5-0.5B default
    seq_len = 64

    noisy_latent = torch.randn(1, latent_dim, device=device)
    timestep = torch.tensor([500], dtype=torch.long, device=device)
    conditioning = torch.randn(1, seq_len, hidden_dim, device=device)

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
        output_path=output_dir / "diffusion_step.onnx",
        component_name="diffusion_step",
    )


def export_acoustic_decoder(model, output_dir: Path, device: str) -> bool:
    """Export the σ-VAE decoder to ONNX."""
    try:
        wrapper = AcousticDecoderWrapper(model)
        wrapper.eval()
    except RuntimeError as exc:
        log.error("Cannot create AcousticDecoderWrapper: %s", exc)
        _dump_model_structure(model, output_dir)
        return False

    # TODO: Refine latent shape after inspecting model internals
    latent_dim = getattr(model.config, "latent_dim", 128)
    latent = torch.randn(1, latent_dim, device=device)

    return _export_onnx(
        module=wrapper,
        dummy_inputs=(latent,),
        input_names=["latent"],
        output_names=["waveform"],
        dynamic_axes={
            "latent": {0: "batch"},
            "waveform": {0: "batch"},
        },
        output_path=output_dir / "acoustic_decoder.onnx",
        component_name="acoustic_decoder",
    )


def save_tokenizer(processor, output_dir: Path):
    """Save tokenizer config for C# reimplementation."""
    tok_dir = output_dir / "tokenizer"
    tok_dir.mkdir(parents=True, exist_ok=True)

    log.info("Saving tokenizer to %s", tok_dir)
    processor.tokenizer.save_pretrained(str(tok_dir))

    # Also copy the most critical file to the top-level models dir
    tokenizer_json = tok_dir / "tokenizer.json"
    if tokenizer_json.exists():
        import shutil
        shutil.copy2(tokenizer_json, output_dir / "tokenizer.json")
        log.info("  ✓ tokenizer.json copied to %s", output_dir / "tokenizer.json")
    else:
        log.warning("  tokenizer.json not found in saved pretrained output")


def main():
    parser = argparse.ArgumentParser(
        description="Export VibeVoice-Realtime-0.5B to ONNX subcomponents",
    )
    parser.add_argument(
        "--output", type=str, default="../models",
        help="Output directory for ONNX files (default: ../models)",
    )
    parser.add_argument(
        "--quantize", type=str, choices=["int8", "uint8"], default=None,
        help="Post-training dynamic quantization type (optional)",
    )
    parser.add_argument(
        "--device", type=str, default="cpu", choices=["cpu", "cuda"],
        help="Device to load the PyTorch model on (default: cpu)",
    )
    args = parser.parse_args()

    if args.device == "cuda" and not torch.cuda.is_available():
        log.warning("CUDA requested but not available — falling back to CPU")
        args.device = "cpu"

    output_dir = Path(args.output).resolve()
    output_dir.mkdir(parents=True, exist_ok=True)
    log.info("Output directory: %s", output_dir)

    # ── Load model ────────────────────────────────────────────────────
    model, processor = load_model(args.device)
    _dump_model_structure(model, output_dir)

    # ── Export components ─────────────────────────────────────────────
    results: dict[str, bool] = {}

    with torch.no_grad():
        results["text_encoder"] = export_text_encoder(
            model, processor, output_dir, args.device,
        )
        results["diffusion_step"] = export_diffusion_step(
            model, output_dir, args.device,
        )
        results["acoustic_decoder"] = export_acoustic_decoder(
            model, output_dir, args.device,
        )

    # ── Tokenizer ─────────────────────────────────────────────────────
    save_tokenizer(processor, output_dir)

    # ── Optional quantization ─────────────────────────────────────────
    if args.quantize:
        for name in ("text_encoder", "diffusion_step", "acoustic_decoder"):
            onnx_path = output_dir / f"{name}.onnx"
            if onnx_path.exists():
                try:
                    _quantize_model(onnx_path, args.quantize)
                except Exception as exc:
                    log.error("Quantization failed for %s: %s", name, exc)

    # ── Summary ───────────────────────────────────────────────────────
    log.info("")
    log.info("═" * 50)
    log.info("Export Summary")
    log.info("═" * 50)
    for name, ok in results.items():
        status = "✓ SUCCESS" if ok else "✗ FAILED"
        log.info("  %s : %s", name.ljust(20), status)
    log.info("═" * 50)

    if all(results.values()):
        log.info("All components exported successfully.")
        log.info("Next step: python validate_export.py --models-dir %s", output_dir)
    else:
        log.warning(
            "Some exports failed. Check model_structure.txt in %s "
            "and update wrapper attribute paths.", output_dir,
        )
        sys.exit(1)


if __name__ == "__main__":
    main()
