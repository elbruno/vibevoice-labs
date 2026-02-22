#!/usr/bin/env python3
"""
Export VibeVoice-Realtime-0.5B PyTorch model to ONNX subcomponents.

Actual model architecture (from model inspection):
  model.model.language_model      — Qwen2Model (195.8M) text encoder (4 layers)
  model.model.tts_language_model  — Qwen2Model (434.4M) TTS backbone (20 layers)
  model.model.prediction_head     — VibeVoiceDiffusionHead (42.1M) diffusion
  model.model.acoustic_tokenizer  — σ-VAE decoder (687.4M, encoder not pretrained)
  model.model.acoustic_connector  — SpeechConnector (0.9M)
  tts_eos_classifier              — BinaryClassifier (0.8M)
  model.model.tts_input_types     — Embedding(2, 896) type embeddings

Exported ONNX files (autoregressive pipeline with KV-cache):
  - lm_with_kv.onnx           — language model with KV-cache: tokens + past → hidden + updated KV
  - tts_lm_prefill.onnx       — TTS-LM multi-token prefill with KV-cache: embeds + past → hidden + KV
  - tts_lm_step.onnx          — TTS-LM single-token step with KV-cache: embed + past → hidden + KV
  - prediction_head.onnx       — diffusion head: (noisy, timestep, condition) → predicted
  - acoustic_decoder.onnx      — σ-VAE decoder: latents → waveform
  - acoustic_connector.onnx    — speech latent → embedding (64 → 896)
  - eos_classifier.onnx        — hidden state → end-of-speech logit
  - type_embeddings.npy        — [2, 896] type embeddings (0=speech, 1=text)

Legacy (not used by C# pipeline, kept for reference):
  - language_model.onnx        — text encoder without KV-cache
  - tts_language_model.onnx    — TTS backbone without KV-cache
  - text_to_condition.onnx     — fused model (broken for speech, kept for compat)

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
from transformers.cache_utils import DynamicCache

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
ONNX_OPSET = 18
SAMPLE_RATE = 24_000

# KV-cache model dimensions (from model architecture)
NUM_TTS_LAYERS = 20
NUM_LM_LAYERS = 4
NUM_KV_HEADS = 2
HEAD_DIM = 64
HIDDEN = 896


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


class TtsLanguageModelWrapper(nn.Module):
    """Wraps model.model.tts_language_model (Qwen2Model) for ONNX export.
    
    Takes pre-computed inputs_embeds (already includes type embeddings)
    and returns hidden states. No KV-cache (recompute approach).
    """

    def __init__(self, tts_language_model):
        super().__init__()
        self.tts_lm = tts_language_model

    def forward(self, inputs_embeds: torch.Tensor, attention_mask: torch.Tensor) -> torch.Tensor:
        outputs = self.tts_lm(inputs_embeds=inputs_embeds, attention_mask=attention_mask, use_cache=False)
        if hasattr(outputs, "last_hidden_state"):
            return outputs.last_hidden_state
        return outputs[0]


class AcousticConnectorWrapper(nn.Module):
    """Wraps model.model.acoustic_connector for ONNX export.
    Projects speech latent (64) to embedding space (896)."""

    def __init__(self, acoustic_connector):
        super().__init__()
        self.connector = acoustic_connector

    def forward(self, speech_latent: torch.Tensor) -> torch.Tensor:
        return self.connector(speech_latent)


class EosClassifierWrapper(nn.Module):
    """Wraps tts_eos_classifier for ONNX export.
    Binary classifier: hidden_state (896) → logit (1)."""

    def __init__(self, eos_classifier):
        super().__init__()
        self.classifier = eos_classifier

    def forward(self, hidden_state: torch.Tensor) -> torch.Tensor:
        return self.classifier(hidden_state)


class TextToConditionWrapper(nn.Module):
    """Fuses language_model + tts_language_model for single-pass text-to-condition.
    LEGACY: Proven broken for speech generation (static condition cannot produce correct text).
    Kept for backward compatibility only.
    """

    def __init__(self, language_model, tts_language_model):
        super().__init__()
        self.lm = language_model
        self.tts_lm = tts_language_model

    def forward(self, input_ids: torch.Tensor, attention_mask: torch.Tensor) -> torch.Tensor:
        hidden = self.lm(input_ids=input_ids, attention_mask=attention_mask).last_hidden_state
        tts_out = self.tts_lm(inputs_embeds=hidden).last_hidden_state
        return tts_out


class PredictionHeadWrapper(nn.Module):
    """Wraps model.model.prediction_head (VibeVoiceDiffusionHead) for single step export.
    condition is 2D [batch, 896], NOT 3D."""

    def __init__(self, prediction_head):
        super().__init__()
        self.head = prediction_head

    def forward(self, noisy_latent: torch.Tensor, timestep: torch.Tensor,
                conditioning: torch.Tensor) -> torch.Tensor:
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


class KVWrapper(nn.Module):
    """Base wrapper for models with KV-cache support."""

    def __init__(self, model, num_layers, is_embedding_input=True):
        super().__init__()
        self.model = model
        self.num_layers = num_layers
        self.is_embedding_input = is_embedding_input

    def _run(self, first_input, attention_mask, position_ids, past_keys, past_values):
        past_key_values = DynamicCache()
        for i in range(self.num_layers):
            past_key_values.update(past_keys[i:i+1].squeeze(0), past_values[i:i+1].squeeze(0), i)
        kwargs = {'attention_mask': attention_mask, 'past_key_values': past_key_values, 'use_cache': True}
        if position_ids is not None:
            kwargs['position_ids'] = position_ids
        if self.is_embedding_input:
            kwargs['inputs_embeds'] = first_input
        else:
            kwargs['input_ids'] = first_input
        outputs = self.model(**kwargs)
        hidden = outputs.last_hidden_state
        new_kv = outputs.past_key_values
        pk = torch.stack([new_kv[i][0] for i in range(self.num_layers)])
        pv = torch.stack([new_kv[i][1] for i in range(self.num_layers)])
        return hidden, pk, pv


class TtsLmKV(KVWrapper):
    """TTS language model with KV-cache (embedding input)."""

    def forward(self, inputs_embeds, attention_mask, position_ids, past_keys, past_values):
        return self._run(inputs_embeds, attention_mask, position_ids, past_keys, past_values)


class LmKV(KVWrapper):
    """Language model with KV-cache (token ID input)."""

    def forward(self, input_ids, attention_mask, past_keys, past_values):
        return self._run(input_ids, attention_mask, None, past_keys, past_values)


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
    """Export model.model.language_model (Qwen2Model, 196M, 4 layers) to ONNX."""
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
        output_path=output_dir / "language_model.onnx",
        component_name="language_model (Qwen2, 196M)",
    )


def export_text_to_condition(model, processor, output_dir: Path, device: str) -> bool:
    """Export fused language_model + tts_language_model to ONNX.
    
    This produces the correct speech-conditioned hidden states needed
    by the prediction_head for diffusion. The C# pipeline should use
    the last token's hidden state as the condition vector.
    """
    wrapper = TextToConditionWrapper(model.model.language_model, model.model.tts_language_model)
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
        output_path=output_dir / "text_to_condition.onnx",
        component_name="text_to_condition (language_model + tts_language_model)",
    )


def export_prediction_head(model, output_dir: Path, device: str) -> bool:
    """Export model.model.prediction_head (diffusion head) to ONNX.
    
    condition is [batch, hidden_size] (2D), NOT [batch, seq_len, hidden_size].
    """
    head = model.model.prediction_head
    wrapper = PredictionHeadWrapper(head)
    wrapper.eval()

    cfg = model.config.diffusion_head_config
    latent_size = cfg.latent_size  # 64
    hidden_size = cfg.hidden_size  # 896

    noisy_latent = torch.randn(1, latent_size, device=device)
    timestep = torch.tensor([500], dtype=torch.long, device=device)
    conditioning = torch.randn(1, hidden_size, device=device)  # 2D: [batch, 896]

    return _export_onnx(
        module=wrapper,
        dummy_inputs=(noisy_latent, timestep, conditioning),
        input_names=["noisy_latent", "timestep", "conditioning"],
        output_names=["predicted_noise"],
        dynamic_axes={
            "noisy_latent": {0: "batch"},
            "timestep": {0: "batch"},
            "conditioning": {0: "batch"},
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


def export_tts_language_model(model, output_dir: Path, device: str) -> bool:
    """Export model.model.tts_language_model (Qwen2Model, 434M, 20 layers) to ONNX.
    
    No KV-cache: takes full inputs_embeds sequence, returns full hidden states.
    The C# pipeline handles type embedding addition and sequence management.
    """
    tts_lm = model.model.tts_language_model
    wrapper = TtsLanguageModelWrapper(tts_lm)
    wrapper.eval()

    seq_len = 16  # typical short sequence for tracing
    hidden_size = 896
    inputs_embeds = torch.randn(1, seq_len, hidden_size, device=device)
    attention_mask = torch.ones(1, seq_len, dtype=torch.long, device=device)

    return _export_onnx(
        module=wrapper,
        dummy_inputs=(inputs_embeds, attention_mask),
        input_names=["inputs_embeds", "attention_mask"],
        output_names=["hidden_states"],
        dynamic_axes={
            "inputs_embeds": {0: "batch", 1: "seq_len"},
            "attention_mask": {0: "batch", 1: "seq_len"},
            "hidden_states": {0: "batch", 1: "seq_len"},
        },
        output_path=output_dir / "tts_language_model.onnx",
        component_name="tts_language_model (Qwen2, 434M)",
    )


def export_acoustic_connector(model, output_dir: Path, device: str) -> bool:
    """Export model.model.acoustic_connector to ONNX.
    Projects speech latent (64) → embedding (896).
    """
    connector = model.model.acoustic_connector
    wrapper = AcousticConnectorWrapper(connector)
    wrapper.eval()

    speech_latent = torch.randn(1, 64, device=device)

    return _export_onnx(
        module=wrapper,
        dummy_inputs=(speech_latent,),
        input_names=["speech_latent"],
        output_names=["embedding"],
        dynamic_axes={
            "speech_latent": {0: "batch"},
            "embedding": {0: "batch"},
        },
        output_path=output_dir / "acoustic_connector.onnx",
        component_name="acoustic_connector (64→896)",
    )


def export_eos_classifier(model, output_dir: Path, device: str) -> bool:
    """Export tts_eos_classifier to ONNX.
    Binary classifier: hidden_state (896) → logit (1).
    """
    classifier = model.tts_eos_classifier
    wrapper = EosClassifierWrapper(classifier)
    wrapper.eval()

    hidden_state = torch.randn(1, 896, device=device)

    return _export_onnx(
        module=wrapper,
        dummy_inputs=(hidden_state,),
        input_names=["hidden_state"],
        output_names=["logit"],
        dynamic_axes={
            "hidden_state": {0: "batch"},
            "logit": {0: "batch"},
        },
        output_path=output_dir / "eos_classifier.onnx",
        component_name="eos_classifier (binary)",
    )


def export_tts_lm_prefill(model, output_dir: Path, device: str) -> bool:
    """Export TTS language model for multi-token prefill with KV-cache.

    Used for the initial text conditioning pass: processes all text tokens
    at once and produces the first KV-cache state.
    """
    tts_lm = model.model.tts_language_model
    wrapper = TtsLmKV(tts_lm, NUM_TTS_LAYERS, is_embedding_input=True)
    wrapper.eval()

    TEXT_LEN = 12
    PAST_SEQ = 316
    inputs_embeds = torch.randn(1, TEXT_LEN, HIDDEN, device=device)
    attention_mask = torch.ones(1, PAST_SEQ + TEXT_LEN, dtype=torch.long, device=device)
    position_ids = torch.arange(PAST_SEQ, PAST_SEQ + TEXT_LEN, device=device).unsqueeze(0)
    past_keys = torch.zeros(NUM_TTS_LAYERS, 1, NUM_KV_HEADS, PAST_SEQ, HEAD_DIM, device=device)
    past_values = torch.zeros(NUM_TTS_LAYERS, 1, NUM_KV_HEADS, PAST_SEQ, HEAD_DIM, device=device)

    return _export_onnx(
        module=wrapper,
        dummy_inputs=(inputs_embeds, attention_mask, position_ids, past_keys, past_values),
        input_names=["inputs_embeds", "attention_mask", "position_ids", "past_keys", "past_values"],
        output_names=["hidden_states", "new_keys", "new_values"],
        dynamic_axes={
            "inputs_embeds": {0: "batch", 1: "seq_len"},
            "attention_mask": {0: "batch", 1: "total_len"},
            "position_ids": {0: "batch", 1: "seq_len"},
            "past_keys": {2: "batch", 3: "past_seq"},
            "past_values": {2: "batch", 3: "past_seq"},
            "hidden_states": {0: "batch", 1: "seq_len"},
            "new_keys": {2: "batch", 3: "new_seq"},
            "new_values": {2: "batch", 3: "new_seq"},
        },
        output_path=output_dir / "tts_lm_prefill.onnx",
        component_name="tts_lm_prefill (KV-cache, 434M)",
    )


def export_tts_lm_step(model, output_dir: Path, device: str) -> bool:
    """Export TTS language model for single-token step with KV-cache.

    Used in the autoregressive loop: processes one new speech token at a time
    using the cached key/values from previous steps.
    """
    tts_lm = model.model.tts_language_model
    wrapper = TtsLmKV(tts_lm, NUM_TTS_LAYERS, is_embedding_input=True)
    wrapper.eval()

    PAST_SEQ = 316
    TEXT_LEN = 12
    STEP_PAST = PAST_SEQ + TEXT_LEN
    inputs_embeds = torch.randn(1, 1, HIDDEN, device=device)
    attention_mask = torch.ones(1, STEP_PAST + 1, dtype=torch.long, device=device)
    position_ids = torch.tensor([[STEP_PAST]], dtype=torch.long, device=device)
    past_keys = torch.zeros(NUM_TTS_LAYERS, 1, NUM_KV_HEADS, STEP_PAST, HEAD_DIM, device=device)
    past_values = torch.zeros(NUM_TTS_LAYERS, 1, NUM_KV_HEADS, STEP_PAST, HEAD_DIM, device=device)

    return _export_onnx(
        module=wrapper,
        dummy_inputs=(inputs_embeds, attention_mask, position_ids, past_keys, past_values),
        input_names=["inputs_embeds", "attention_mask", "position_ids", "past_keys", "past_values"],
        output_names=["hidden_states", "new_keys", "new_values"],
        dynamic_axes={
            "inputs_embeds": {0: "batch", 1: "seq_len"},
            "attention_mask": {0: "batch", 1: "total_len"},
            "position_ids": {0: "batch", 1: "seq_len"},
            "past_keys": {2: "batch", 3: "past_seq"},
            "past_values": {2: "batch", 3: "past_seq"},
            "hidden_states": {0: "batch", 1: "seq_len"},
            "new_keys": {2: "batch", 3: "new_seq"},
            "new_values": {2: "batch", 3: "new_seq"},
        },
        output_path=output_dir / "tts_lm_step.onnx",
        component_name="tts_lm_step (KV-cache, single token)",
    )


def export_lm_with_kv(model, processor, output_dir: Path, device: str) -> bool:
    """Export language model with KV-cache.

    Used for text encoding with KV-cache support, enabling incremental
    processing of input tokens.
    """
    lm = model.model.language_model
    wrapper = LmKV(lm, NUM_LM_LAYERS, is_embedding_input=False)
    wrapper.eval()

    TEXT_LEN = 12
    LM_PAST = 108
    input_ids = torch.randint(0, processor.tokenizer.vocab_size, (1, TEXT_LEN), device=device)
    attention_mask = torch.ones(1, LM_PAST + TEXT_LEN, dtype=torch.long, device=device)
    past_keys = torch.zeros(NUM_LM_LAYERS, 1, NUM_KV_HEADS, LM_PAST, HEAD_DIM, device=device)
    past_values = torch.zeros(NUM_LM_LAYERS, 1, NUM_KV_HEADS, LM_PAST, HEAD_DIM, device=device)

    return _export_onnx(
        module=wrapper,
        dummy_inputs=(input_ids, attention_mask, past_keys, past_values),
        input_names=["input_ids", "attention_mask", "past_keys", "past_values"],
        output_names=["hidden_states", "new_keys", "new_values"],
        dynamic_axes={
            "input_ids": {0: "batch", 1: "seq_len"},
            "attention_mask": {0: "batch", 1: "total_len"},
            "past_keys": {2: "batch", 3: "past_seq"},
            "past_values": {2: "batch", 3: "past_seq"},
            "hidden_states": {0: "batch", 1: "seq_len"},
            "new_keys": {2: "batch", 3: "new_seq"},
            "new_values": {2: "batch", 3: "new_seq"},
        },
        output_path=output_dir / "lm_with_kv.onnx",
        component_name="lm_with_kv (Qwen2, 196M, KV-cache)",
    )


def save_type_embeddings(model, output_dir: Path):
    """Save tts_input_types embedding weights as numpy array.
    Shape: [2, 896] — index 0 = speech type, index 1 = text type.
    """
    type_embed = model.model.tts_input_types.weight.detach().cpu().numpy()
    npy_path = output_dir / "type_embeddings.npy"
    np.save(str(npy_path), type_embed)
    log.info("Type embeddings saved to %s (shape: %s)", npy_path, type_embed.shape)


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
        # Core autoregressive pipeline models
        results["language_model"] = export_text_encoder(model, processor, output_dir, args.device)
        results["tts_language_model"] = export_tts_language_model(model, output_dir, args.device)
        results["prediction_head"] = export_prediction_head(model, output_dir, args.device)
        results["acoustic_decoder"] = export_acoustic_decoder(model, output_dir, args.device)
        results["acoustic_connector"] = export_acoustic_connector(model, output_dir, args.device)
        results["eos_classifier"] = export_eos_classifier(model, output_dir, args.device)

        # KV-cache models for autoregressive pipeline
        results["tts_lm_prefill"] = export_tts_lm_prefill(model, output_dir, args.device)
        results["tts_lm_step"] = export_tts_lm_step(model, output_dir, args.device)
        results["lm_with_kv"] = export_lm_with_kv(model, processor, output_dir, args.device)

    save_type_embeddings(model, output_dir)
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
