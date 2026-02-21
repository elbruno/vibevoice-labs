#!/usr/bin/env python3
"""
Validate exported ONNX models against the original PyTorch model.

Loads both the PyTorch VibeVoice model and the exported ONNX subcomponents,
feeds identical inputs through each, and compares outputs numerically to
ensure the conversion is faithful.

Usage:
    python validate_export.py --models-dir ../models
    python validate_export.py --models-dir ../models --device cuda
"""

import argparse
import json
import logging
import sys
from pathlib import Path

import numpy as np
import torch

logging.basicConfig(
    level=logging.INFO,
    format="%(asctime)s [%(levelname)s] %(message)s",
    datefmt="%H:%M:%S",
)
log = logging.getLogger(__name__)

MODEL_NAME = "microsoft/VibeVoice-Realtime-0.5B"
ATOL = 1e-4
RTOL = 1e-3


def load_pytorch_model(device: str):
    """Load the original PyTorch model for reference."""
    from vibevoice.modular.modeling_vibevoice_streaming_inference import (
        VibeVoiceStreamingForConditionalGenerationInference,
    )
    from vibevoice.processor.vibevoice_streaming_processor import (
        VibeVoiceStreamingProcessor,
    )

    log.info("Loading PyTorch model from %s …", MODEL_NAME)
    processor = VibeVoiceStreamingProcessor.from_pretrained(MODEL_NAME)
    model = VibeVoiceStreamingForConditionalGenerationInference.from_pretrained(
        MODEL_NAME,
        torch_dtype=torch.float32,
        attn_implementation="sdpa",
        device_map=device,
    )
    model.eval()
    model.set_ddpm_inference_steps(num_steps=5)
    return model, processor


def load_onnx_session(onnx_path: Path):
    """Create an ONNX Runtime InferenceSession."""
    import onnxruntime as ort

    log.info("Loading ONNX model: %s", onnx_path)
    opts = ort.SessionOptions()
    opts.graph_optimization_level = ort.GraphOptimizationLevel.ORT_ENABLE_ALL
    session = ort.InferenceSession(str(onnx_path), opts, providers=["CPUExecutionProvider"])
    return session


def _compare(name: str, pytorch_out: np.ndarray, onnx_out: np.ndarray) -> bool:
    """Compare two arrays and report results."""
    if pytorch_out.shape != onnx_out.shape:
        log.error(
            "  %s — shape mismatch: PyTorch %s vs ONNX %s",
            name, pytorch_out.shape, onnx_out.shape,
        )
        return False

    max_diff = float(np.max(np.abs(pytorch_out - onnx_out)))
    mean_diff = float(np.mean(np.abs(pytorch_out - onnx_out)))
    close = np.allclose(pytorch_out, onnx_out, atol=ATOL, rtol=RTOL)

    status = "✓ PASS" if close else "✗ FAIL"
    log.info(
        "  %s — %s  (max_diff=%.6e, mean_diff=%.6e)",
        name, status, max_diff, mean_diff,
    )
    return close


# ---------------------------------------------------------------------------
# Per-component validators
# ---------------------------------------------------------------------------

def validate_text_encoder(
    model, processor, onnx_session, device: str,
) -> bool:
    """Validate text_encoder.onnx against the PyTorch LLM backbone."""
    # Import the wrapper used during export for consistent behaviour
    from export_model import TextEncoderWrapper

    seq_len = 64
    input_ids = torch.randint(
        0, processor.tokenizer.vocab_size, (1, seq_len), device=device,
    )
    attention_mask = torch.ones(1, seq_len, dtype=torch.long, device=device)

    # PyTorch forward
    wrapper = TextEncoderWrapper(model)
    wrapper.eval()
    with torch.no_grad():
        pt_out = wrapper(input_ids, attention_mask).cpu().numpy()

    # ONNX forward
    feeds = {
        "input_ids": input_ids.cpu().numpy(),
        "attention_mask": attention_mask.cpu().numpy(),
    }
    onnx_out = onnx_session.run(["hidden_states"], feeds)[0]

    return _compare("text_encoder", pt_out, onnx_out)


def validate_diffusion_step(
    model, onnx_session, device: str,
) -> bool:
    """Validate diffusion_step.onnx against the PyTorch diffusion head."""
    from export_model import DiffusionStepWrapper

    latent_dim = getattr(model.config, "latent_dim", 128)
    hidden_dim = getattr(model.config, "hidden_size", 896)
    seq_len = 64

    noisy_latent = torch.randn(1, latent_dim, device=device)
    timestep = torch.tensor([500], dtype=torch.long, device=device)
    conditioning = torch.randn(1, seq_len, hidden_dim, device=device)

    # PyTorch forward
    wrapper = DiffusionStepWrapper(model)
    wrapper.eval()
    with torch.no_grad():
        pt_out = wrapper(noisy_latent, timestep, conditioning).cpu().numpy()

    # ONNX forward
    feeds = {
        "noisy_latent": noisy_latent.cpu().numpy(),
        "timestep": timestep.cpu().numpy(),
        "conditioning": conditioning.cpu().numpy(),
    }
    onnx_out = onnx_session.run(["predicted_noise"], feeds)[0]

    return _compare("diffusion_step", pt_out, onnx_out)


def validate_acoustic_decoder(
    model, onnx_session, device: str,
) -> bool:
    """Validate acoustic_decoder.onnx against the PyTorch σ-VAE decoder."""
    from export_model import AcousticDecoderWrapper

    latent_dim = getattr(model.config, "latent_dim", 128)
    latent = torch.randn(1, latent_dim, device=device)

    # PyTorch forward
    wrapper = AcousticDecoderWrapper(model)
    wrapper.eval()
    with torch.no_grad():
        pt_out = wrapper(latent).cpu().numpy()

    # ONNX forward
    feeds = {"latent": latent.cpu().numpy()}
    onnx_out = onnx_session.run(["waveform"], feeds)[0]

    return _compare("acoustic_decoder", pt_out, onnx_out)


# ---------------------------------------------------------------------------
# Main
# ---------------------------------------------------------------------------

def main():
    parser = argparse.ArgumentParser(
        description="Validate ONNX exports against PyTorch VibeVoice model",
    )
    parser.add_argument(
        "--models-dir", type=str, default="../models",
        help="Directory containing exported ONNX files (default: ../models)",
    )
    parser.add_argument(
        "--device", type=str, default="cpu", choices=["cpu", "cuda"],
        help="Device for PyTorch model (default: cpu)",
    )
    args = parser.parse_args()

    if args.device == "cuda" and not torch.cuda.is_available():
        log.warning("CUDA requested but not available — falling back to CPU")
        args.device = "cpu"

    models_dir = Path(args.models_dir).resolve()
    if not models_dir.is_dir():
        log.error("Models directory does not exist: %s", models_dir)
        sys.exit(1)

    # ── Load PyTorch model ────────────────────────────────────────────
    model, processor = load_pytorch_model(args.device)

    # ── Validate each component ───────────────────────────────────────
    components = {
        "text_encoder": ("text_encoder.onnx", validate_text_encoder),
        "diffusion_step": ("diffusion_step.onnx", validate_diffusion_step),
        "acoustic_decoder": ("acoustic_decoder.onnx", validate_acoustic_decoder),
    }

    results: dict[str, str] = {}
    for name, (filename, validator) in components.items():
        onnx_path = models_dir / filename
        if not onnx_path.exists():
            log.warning("  %s not found — skipping", onnx_path)
            results[name] = "SKIPPED"
            continue

        session = load_onnx_session(onnx_path)
        try:
            if name == "text_encoder":
                ok = validator(model, processor, session, args.device)
            elif name == "diffusion_step":
                ok = validator(model, session, args.device)
            else:
                ok = validator(model, session, args.device)
            results[name] = "PASS" if ok else "FAIL"
        except Exception as exc:
            log.error("  %s validation error: %s", name, exc, exc_info=True)
            results[name] = "ERROR"

    # ── Summary ───────────────────────────────────────────────────────
    log.info("")
    log.info("═" * 50)
    log.info("Validation Summary  (atol=%.0e, rtol=%.0e)", ATOL, RTOL)
    log.info("═" * 50)
    for name, status in results.items():
        icon = {"PASS": "✓", "FAIL": "✗", "SKIPPED": "–", "ERROR": "!"}.get(status, "?")
        log.info("  %s %s : %s", icon, name.ljust(20), status)
    log.info("═" * 50)

    # Write machine-readable results
    results_path = models_dir / "validation_results.json"
    results_path.write_text(json.dumps(results, indent=2))
    log.info("Results written to %s", results_path)

    if any(v == "FAIL" for v in results.values()):
        log.error("Some components failed validation — review differences above")
        sys.exit(1)
    if any(v == "ERROR" for v in results.values()):
        log.error("Some components errored during validation")
        sys.exit(1)

    log.info("All available components passed validation.")


if __name__ == "__main__":
    main()
