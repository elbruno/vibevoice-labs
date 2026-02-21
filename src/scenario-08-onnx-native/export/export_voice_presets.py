#!/usr/bin/env python3
"""
Convert VibeVoice voice presets (.pt) to NumPy arrays (.npy) for C# consumption.

Downloads voice presets from the official VibeVoice GitHub repo (if not cached
locally), inspects their tensor structure, and writes each tensor as a separate
.npy file alongside a manifest.json that the C# pipeline reads at startup.

Usage:
    python export_voice_presets.py --output ../models/voices
    python export_voice_presets.py --voices-dir ./my_voices --output ../models/voices
"""

import argparse
import json
import logging
import os
import sys
from pathlib import Path
from urllib.request import urlretrieve

import numpy as np
import torch

logging.basicConfig(
    level=logging.INFO,
    format="%(asctime)s [%(levelname)s] %(message)s",
    datefmt="%H:%M:%S",
)
log = logging.getLogger(__name__)

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
    """Make sure all voice presets are available locally.  Returns paths."""
    voices_dir.mkdir(parents=True, exist_ok=True)
    paths = []
    for name in AVAILABLE_VOICES:
        try:
            paths.append(download_voice(name, voices_dir))
        except Exception:
            log.warning("Skipping voice %s due to download failure", name)
    return paths


def inspect_preset(data) -> dict:
    """Recursively describe the structure of a voice preset."""
    if isinstance(data, torch.Tensor):
        return {
            "type": "tensor",
            "dtype": str(data.dtype),
            "shape": list(data.shape),
        }
    if isinstance(data, dict):
        return {k: inspect_preset(v) for k, v in data.items()}
    if isinstance(data, (list, tuple)):
        return [inspect_preset(v) for v in data]
    return {"type": type(data).__name__, "value": repr(data)[:200]}


def _flatten_tensors(data, prefix: str = "") -> list[tuple[str, torch.Tensor]]:
    """Recursively extract (key_path, tensor) pairs from a nested structure."""
    pairs = []
    if isinstance(data, torch.Tensor):
        pairs.append((prefix or "root", data))
    elif isinstance(data, dict):
        for k, v in data.items():
            child_key = f"{prefix}.{k}" if prefix else str(k)
            pairs.extend(_flatten_tensors(v, child_key))
    elif isinstance(data, (list, tuple)):
        for i, v in enumerate(data):
            child_key = f"{prefix}[{i}]"
            pairs.extend(_flatten_tensors(v, child_key))
    return pairs


def _safe_filename(key: str) -> str:
    """Convert a nested key path to a safe filename."""
    return key.replace(".", "_").replace("[", "_").replace("]", "").replace("/", "_")


def convert_voice(pt_path: Path, output_dir: Path) -> dict | None:
    """Load a .pt voice preset, save tensors as .npy, return manifest entry."""
    voice_name = pt_path.stem  # e.g. "en-Carter_man"
    voice_dir = output_dir / voice_name
    voice_dir.mkdir(parents=True, exist_ok=True)

    log.info("Processing voice: %s", voice_name)

    try:
        data = torch.load(str(pt_path), map_location="cpu", weights_only=False)
    except Exception as exc:
        log.error("  Failed to load %s: %s", pt_path, exc)
        return None

    # Dump structure for inspection
    structure = inspect_preset(data)
    structure_path = voice_dir / "structure.json"
    structure_path.write_text(json.dumps(structure, indent=2))
    log.info("  Structure written to %s", structure_path)

    # Flatten and save tensors
    tensors = _flatten_tensors(data)
    if not tensors:
        log.warning("  No tensors found in %s", pt_path.name)
        return None

    tensor_files: list[dict] = []
    for key, tensor in tensors:
        fname = _safe_filename(key) + ".npy"
        npy_path = voice_dir / fname
        arr = tensor.float().cpu().numpy()
        np.save(str(npy_path), arr)
        tensor_files.append({
            "key": key,
            "file": fname,
            "dtype": str(arr.dtype),
            "shape": list(arr.shape),
        })

    log.info("  Saved %d tensor(s) → %s", len(tensor_files), voice_dir)

    return {
        "name": voice_name,
        "source_file": pt_path.name,
        "directory": voice_name,
        "tensors": tensor_files,
    }


def main():
    parser = argparse.ArgumentParser(
        description="Convert VibeVoice voice presets (.pt) to .npy for C#",
    )
    parser.add_argument(
        "--output", type=str, default="../models/voices",
        help="Output directory for voice .npy files (default: ../models/voices)",
    )
    parser.add_argument(
        "--voices-dir", type=str, default=None,
        help="Directory containing .pt voice files.  "
             "If omitted, voices are downloaded from GitHub.",
    )
    args = parser.parse_args()

    output_dir = Path(args.output).resolve()
    output_dir.mkdir(parents=True, exist_ok=True)

    # ── Obtain voice presets ──────────────────────────────────────────
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

    # ── Convert each voice ────────────────────────────────────────────
    manifest_entries: list[dict] = []
    for pt_path in pt_files:
        entry = convert_voice(pt_path, output_dir)
        if entry:
            manifest_entries.append(entry)

    # ── Write manifest ────────────────────────────────────────────────
    manifest = {
        "version": 1,
        "sample_rate": 24000,
        "description": "VibeVoice voice presets exported as NumPy arrays",
        "voices": manifest_entries,
    }
    manifest_path = output_dir / "manifest.json"
    manifest_path.write_text(json.dumps(manifest, indent=2))
    log.info("")
    log.info("═" * 50)
    log.info("Voice Export Summary")
    log.info("═" * 50)
    log.info("  Voices exported : %d / %d", len(manifest_entries), len(pt_files))
    log.info("  Manifest        : %s", manifest_path)
    log.info("═" * 50)

    if len(manifest_entries) < len(pt_files):
        log.warning("Some voices failed — check logs above")
        sys.exit(1)


if __name__ == "__main__":
    main()
