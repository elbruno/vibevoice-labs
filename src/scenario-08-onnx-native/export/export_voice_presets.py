#!/usr/bin/env python3
"""
Export VibeVoice voice presets as KV-cache numpy arrays for C# consumption.

The autoregressive pipeline requires pre-computed KV-cache data for each voice.
This script loads the VibeVoice model, runs each voice preset through the
language model and TTS language model to generate the KV-cache state, then
saves the per-layer key/value tensors as .npy files.

Output structure per voice:
    voices/{voice_name}/
        metadata.json                    # Voice info + tensor shapes
        tts_kv_key_{0..19}.npy          # TTS-LM positive KV-cache keys (20 layers)
        tts_kv_value_{0..19}.npy        # TTS-LM positive KV-cache values (20 layers)
        lm_kv_key_{0..3}.npy           # LM KV-cache keys (4 layers)
        lm_kv_value_{0..3}.npy         # LM KV-cache values (4 layers)
        negative/
            tts_kv_key_{0..19}.npy      # TTS-LM negative KV-cache keys
            tts_kv_value_{0..19}.npy    # TTS-LM negative KV-cache values

Usage:
    python export_voice_presets.py --output ../models/voices
    python export_voice_presets.py --output ../models/voices --device cuda
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
NUM_TTS_LAYERS = 20
NUM_LM_LAYERS = 4

VOICE_BASE_URL = (
    "https://raw.githubusercontent.com/microsoft/VibeVoice/main/"
    "demo/voices/streaming_model/"
)

AVAILABLE_VOICES = [
    "en-Carter_man.pt",
    "en-Davis_man.pt",
    "en-Emma_woman.pt",
    "en-Frank_man.pt",
    "en-Grace_woman.pt",
    "en-Mike_man.pt",
]


def download_voice(name: str, dest_dir: Path) -> Path:
    """Download a single voice preset if not already present."""
    from urllib.request import urlretrieve
    dest = dest_dir / name
    if dest.exists():
        log.info("  ↳ %s already cached", name)
        return dest
    url = VOICE_BASE_URL + name
    log.info("  ↳ Downloading %s …", url)
    try:
        urlretrieve(url, str(dest))
        log.info("    saved → %s (%.1f KB)", dest, dest.stat().st_size / 1024)
    except Exception as exc:
        log.error("    Failed to download %s: %s", name, exc)
        raise
    return dest


def ensure_voices(voices_dir: Path) -> list[Path]:
    """Make sure all voice presets are available locally."""
    voices_dir.mkdir(parents=True, exist_ok=True)
    paths = []
    for name in AVAILABLE_VOICES:
        try:
            paths.append(download_voice(name, voices_dir))
        except Exception:
            log.warning("Skipping voice %s due to download failure", name)
    return paths


def load_model(device: str):
    """Load the VibeVoice model and processor."""
    from vibevoice.modular.modeling_vibevoice_streaming_inference import (
        VibeVoiceStreamingForConditionalGenerationInference,
    )
    from vibevoice.processor.vibevoice_streaming_processor import (
        VibeVoiceStreamingProcessor,
    )

    log.info("Loading model from %s …", MODEL_NAME)
    processor = VibeVoiceStreamingProcessor.from_pretrained(MODEL_NAME)
    model = VibeVoiceStreamingForConditionalGenerationInference.from_pretrained(
        MODEL_NAME,
        torch_dtype=torch.float32,
        attn_implementation="eager",
        device_map=device,
    )
    model.eval()
    return model, processor


def save_kv_cache(kv_cache, num_layers: int, output_dir: Path, prefix: str):
    """Save KV-cache as per-layer .npy files."""
    output_dir.mkdir(parents=True, exist_ok=True)
    for i in range(num_layers):
        key = kv_cache[i][0].detach().cpu().float().numpy()
        value = kv_cache[i][1].detach().cpu().float().numpy()
        np.save(str(output_dir / f"{prefix}_key_{i}.npy"), key)
        np.save(str(output_dir / f"{prefix}_value_{i}.npy"), value)


def export_voice(model, processor, pt_path: Path, output_dir: Path, device: str) -> dict | None:
    """Export a single voice preset to KV-cache .npy files."""
    voice_name = pt_path.stem  # e.g. "en-Carter_man"
    voice_dir = output_dir / voice_name
    voice_dir.mkdir(parents=True, exist_ok=True)
    neg_dir = voice_dir / "negative"
    neg_dir.mkdir(parents=True, exist_ok=True)

    log.info("Processing voice: %s", voice_name)

    try:
        data = torch.load(str(pt_path), map_location="cpu", weights_only=False)
    except Exception as exc:
        log.error("  Failed to load %s: %s", pt_path, exc)
        return None

    # Extract prompt tokens from the voice preset
    prompt_speech_tokens = data.get("prompt_speech_tokens")
    prompt_text_tokens = data.get("prompt_text_tokens")

    if prompt_speech_tokens is None or prompt_text_tokens is None:
        log.error("  Voice preset %s missing prompt tokens", voice_name)
        return None

    if isinstance(prompt_speech_tokens, torch.Tensor):
        prompt_speech_tokens = prompt_speech_tokens.to(device)
    if isinstance(prompt_text_tokens, torch.Tensor):
        prompt_text_tokens = prompt_text_tokens.to(device)

    log.info("  prompt_speech_tokens shape: %s", prompt_speech_tokens.shape if hasattr(prompt_speech_tokens, 'shape') else 'N/A')
    log.info("  prompt_text_tokens shape: %s", prompt_text_tokens.shape if hasattr(prompt_text_tokens, 'shape') else 'N/A')

    with torch.no_grad():
        # ── LM KV-cache: run text tokens through language_model ──
        if prompt_text_tokens.dim() == 1:
            prompt_text_tokens = prompt_text_tokens.unsqueeze(0)
        lm_attn = torch.ones_like(prompt_text_tokens)
        lm_out = model.model.language_model(
            input_ids=prompt_text_tokens,
            attention_mask=lm_attn,
            use_cache=True,
        )
        lm_kv = lm_out.past_key_values
        lm_prompt_len = prompt_text_tokens.shape[1]
        log.info("  LM KV-cache: %d layers, prompt_len=%d", NUM_LM_LAYERS, lm_prompt_len)
        save_kv_cache(lm_kv, NUM_LM_LAYERS, voice_dir, "lm_kv")

        # ── TTS-LM positive KV-cache: run speech tokens through tts_language_model ──
        # Get speech embeddings via acoustic_connector
        speech_latents = data.get("prompt_speech_latents")
        if speech_latents is not None:
            speech_latents = speech_latents.to(device).float()
            if speech_latents.dim() == 2:
                # [num_frames, latent_dim] → process each frame
                num_frames = speech_latents.shape[0]
                speech_embeds_list = []
                for f in range(num_frames):
                    emb = model.model.acoustic_connector(speech_latents[f:f+1])
                    speech_embeds_list.append(emb)
                speech_embeds = torch.cat(speech_embeds_list, dim=0).unsqueeze(0)  # [1, num_frames, 896]
            elif speech_latents.dim() == 3:
                # [1, num_frames, latent_dim]
                num_frames = speech_latents.shape[1]
                speech_embeds_list = []
                for f in range(num_frames):
                    emb = model.model.acoustic_connector(speech_latents[:, f, :])
                    speech_embeds_list.append(emb)
                speech_embeds = torch.stack(speech_embeds_list, dim=1)  # [1, num_frames, 896]
            else:
                log.error("  Unexpected speech_latents shape: %s", speech_latents.shape)
                return None
        else:
            # Fallback: use prompt_speech_tokens directly with embedding
            log.warning("  No prompt_speech_latents found, using speech tokens with embedding lookup")
            if prompt_speech_tokens.dim() == 1:
                prompt_speech_tokens = prompt_speech_tokens.unsqueeze(0)
            # Process through tts_language_model's embedding
            speech_embeds = model.model.acoustic_connector(prompt_speech_tokens.float())
            if speech_embeds.dim() == 2:
                speech_embeds = speech_embeds.unsqueeze(0)

        # Add type embedding for speech (index 0)
        type_embed_speech = model.model.tts_input_types.weight[0:1]  # [1, 896]
        speech_embeds = speech_embeds + type_embed_speech.unsqueeze(0)

        tts_attn = torch.ones(1, speech_embeds.shape[1], dtype=torch.long, device=device)
        tts_out = model.model.tts_language_model(
            inputs_embeds=speech_embeds,
            attention_mask=tts_attn,
            use_cache=True,
        )
        tts_kv = tts_out.past_key_values
        tts_prompt_len = speech_embeds.shape[1]
        log.info("  TTS KV-cache (positive): %d layers, prompt_len=%d", NUM_TTS_LAYERS, tts_prompt_len)
        save_kv_cache(tts_kv, NUM_TTS_LAYERS, voice_dir, "tts_kv")

        # ── TTS-LM negative KV-cache: minimal (1-token) negative path ──
        neg_embed = torch.zeros(1, 1, speech_embeds.shape[2], device=device)
        neg_attn = torch.ones(1, 1, dtype=torch.long, device=device)
        neg_out = model.model.tts_language_model(
            inputs_embeds=neg_embed,
            attention_mask=neg_attn,
            use_cache=True,
        )
        neg_kv = neg_out.past_key_values
        neg_prompt_len = 1
        log.info("  TTS KV-cache (negative): %d layers, prompt_len=%d", NUM_TTS_LAYERS, neg_prompt_len)
        save_kv_cache(neg_kv, NUM_TTS_LAYERS, neg_dir, "tts_kv")

    # ── Write metadata ──
    metadata = {
        "name": voice_name,
        "source_file": pt_path.name,
        "lm_prompt_len": lm_prompt_len,
        "tts_prompt_len": tts_prompt_len,
        "neg_prompt_len": neg_prompt_len,
        "num_tts_layers": NUM_TTS_LAYERS,
        "num_lm_layers": NUM_LM_LAYERS,
    }
    meta_path = voice_dir / "metadata.json"
    meta_path.write_text(json.dumps(metadata, indent=2))
    log.info("  Metadata → %s", meta_path)

    return metadata


def main():
    parser = argparse.ArgumentParser(
        description="Export VibeVoice voice presets as KV-cache .npy files for C#",
    )
    parser.add_argument(
        "--output", type=str, default="../models/voices",
        help="Output directory for voice KV-cache files (default: ../models/voices)",
    )
    parser.add_argument(
        "--voices-dir", type=str, default=None,
        help="Directory containing .pt voice files. "
             "If omitted, voices are downloaded from GitHub.",
    )
    parser.add_argument(
        "--device", type=str, default="cpu", choices=["cpu", "cuda"],
        help="Device to load model on (default: cpu)",
    )
    args = parser.parse_args()

    if args.device == "cuda" and not torch.cuda.is_available():
        log.warning("CUDA not available — falling back to CPU")
        args.device = "cpu"

    output_dir = Path(args.output).resolve()
    output_dir.mkdir(parents=True, exist_ok=True)

    # ── Obtain voice presets ──
    if args.voices_dir:
        voices_dir = Path(args.voices_dir).resolve()
        if not voices_dir.is_dir():
            log.error("--voices-dir %s does not exist", voices_dir)
            sys.exit(1)
        pt_files = sorted(voices_dir.glob("*.pt"))
        if not pt_files:
            log.error("No .pt files found in %s", voices_dir)
            sys.exit(1)
    else:
        cache_dir = output_dir / ".voice_cache"
        log.info("Downloading voice presets to cache: %s", cache_dir)
        pt_files = ensure_voices(cache_dir)

    if not pt_files:
        log.error("No voice presets available — aborting")
        sys.exit(1)

    # ── Load model (needed for KV-cache generation) ──
    model, processor = load_model(args.device)

    # ── Export each voice ──
    results = []
    for pt_path in pt_files:
        with torch.no_grad():
            entry = export_voice(model, processor, pt_path, output_dir, args.device)
            if entry:
                results.append(entry)

    log.info("")
    log.info("═" * 50)
    log.info("Voice Preset Export Summary")
    log.info("═" * 50)
    log.info("  Voices exported : %d / %d", len(results), len(pt_files))
    for r in results:
        log.info("    ✓ %s (tts_len=%d, lm_len=%d)",
                 r["name"], r["tts_prompt_len"], r["lm_prompt_len"])
    log.info("═" * 50)

    if len(results) < len(pt_files):
        log.warning("Some voices failed — check logs above")
        sys.exit(1)
    else:
        log.info("All voice presets exported successfully!")


if __name__ == "__main__":
    main()
