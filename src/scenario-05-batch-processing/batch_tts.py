"""
VibeVoice TTS — Batch Processing CLI Tool
==========================================
This script reads .txt files from an input directory and generates
corresponding .wav files using the VibeVoice-Realtime-0.5B model.

No HTTP server needed — this tool talks to the model directly!

Features:
- Process entire directories of text files at once
- YAML front-matter support for per-file voice override
- Parallel processing for faster batch jobs
- Progress bar and summary report

Model: microsoft/VibeVoice-Realtime-0.5B
"""

# =============================================================================
# STEP 1: Import Required Libraries
# =============================================================================
# click: For building the CLI interface
# tqdm: For progress bars
# pyyaml: For parsing YAML front-matter in text files
# vibevoice_realtime: The TTS model from Microsoft
# soundfile: For saving audio to WAV files
# concurrent.futures: For parallel processing

import os
import re
import time
from pathlib import Path
from concurrent.futures import ThreadPoolExecutor, as_completed

import click
import yaml
import numpy as np
import soundfile as sf
from tqdm import tqdm
from vibevoice_realtime import VibeVoiceRealtime

# =============================================================================
# STEP 2: Constants
# =============================================================================
# VibeVoice outputs audio at 24kHz — this is fixed by the model

SAMPLE_RATE = 24000
MODEL_NAME = "microsoft/VibeVoice-Realtime-0.5B"

# =============================================================================
# STEP 3: YAML Front-Matter Parser
# =============================================================================
# Text files can optionally include YAML front-matter at the top to override
# the voice setting on a per-file basis. The format looks like:
#
#   ---
#   voice: FR
#   ---
#   Bonjour le monde!
#
# If no front-matter is found, the file content is used as-is.

FRONT_MATTER_PATTERN = re.compile(r"^---\s*\n(.*?)\n---\s*\n", re.DOTALL)


def parse_text_file(filepath: Path) -> tuple[str, str | None]:
    """
    Parse a text file, extracting optional YAML front-matter.

    Returns:
        (text_content, voice_override) — voice_override is None if not specified
    """
    raw = filepath.read_text(encoding="utf-8")
    match = FRONT_MATTER_PATTERN.match(raw)

    if match:
        # Parse the YAML block between the --- markers
        front_matter = yaml.safe_load(match.group(1))
        text = raw[match.end():].strip()
        voice = front_matter.get("voice") if isinstance(front_matter, dict) else None
        return text, voice
    else:
        return raw.strip(), None


# =============================================================================
# STEP 4: Single File Processor
# =============================================================================
# This function handles generating audio for one text file.
# It's designed to be called in parallel without issues.

def process_file(
    filepath: Path,
    output_dir: Path,
    default_voice: str,
    model: VibeVoiceRealtime,
) -> dict:
    """
    Process a single text file and generate the corresponding WAV file.

    Returns a result dict with status, timing, and error info.
    """
    result = {
        "file": filepath.name,
        "status": "success",
        "duration": 0.0,
        "audio_duration": 0.0,
        "error": None,
    }

    start = time.time()

    try:
        # Parse text and optional voice override
        text, voice_override = parse_text_file(filepath)
        voice = voice_override or default_voice

        if not text:
            raise ValueError("File is empty (no text content found)")

        # Generate audio using VibeVoice
        audio = model.generate(text=text, speaker=voice)

        # Build output filename: hello-english.txt → hello-english.wav
        output_path = output_dir / filepath.with_suffix(".wav").name
        sf.write(str(output_path), audio, SAMPLE_RATE)

        result["audio_duration"] = len(audio) / SAMPLE_RATE

    except Exception as e:
        result["status"] = "failed"
        result["error"] = str(e)

    result["duration"] = time.time() - start
    return result


# =============================================================================
# STEP 5: CLI Entry Point
# =============================================================================
# We use Click to define a clean command-line interface with sensible defaults.

@click.command()
@click.option(
    "--input-dir",
    type=click.Path(exists=True, file_okay=False),
    default="./sample-texts",
    show_default=True,
    help="Directory containing .txt files to process.",
)
@click.option(
    "--output-dir",
    type=click.Path(file_okay=False),
    default="./output",
    show_default=True,
    help="Directory where .wav files will be saved.",
)
@click.option(
    "--voice",
    default="EN-Default",
    show_default=True,
    help="Default voice/speaker for TTS (can be overridden per file via YAML front-matter).",
)
@click.option(
    "--parallel",
    default=1,
    show_default=True,
    type=int,
    help="Number of files to process concurrently.",
)
def main(input_dir: str, output_dir: str, voice: str, parallel: int):
    """
    🎤 VibeVoice Batch TTS — Convert text files to speech!

    Reads .txt files from INPUT_DIR, generates .wav audio files,
    and saves them to OUTPUT_DIR. Supports YAML front-matter for
    per-file voice overrides.
    """
    input_path = Path(input_dir)
    output_path = Path(output_dir)

    # -------------------------------------------------------------------------
    # STEP 5a: Discover text files
    # -------------------------------------------------------------------------
    txt_files = sorted(input_path.glob("*.txt"))

    if not txt_files:
        click.echo(f"⚠️  No .txt files found in {input_path}")
        return

    click.echo(f"\n🎤 VibeVoice Batch TTS")
    click.echo(f"   Input:    {input_path.resolve()}")
    click.echo(f"   Output:   {output_path.resolve()}")
    click.echo(f"   Voice:    {voice}")
    click.echo(f"   Files:    {len(txt_files)}")
    click.echo(f"   Parallel: {parallel}\n")

    # -------------------------------------------------------------------------
    # STEP 5b: Create output directory
    # -------------------------------------------------------------------------
    output_path.mkdir(parents=True, exist_ok=True)

    # -------------------------------------------------------------------------
    # STEP 5c: Load the VibeVoice model
    # -------------------------------------------------------------------------
    click.echo("🔄 Loading VibeVoice-Realtime-0.5B model...")
    batch_start = time.time()
    model = VibeVoiceRealtime.from_pretrained(MODEL_NAME)
    click.echo("✅ Model loaded!\n")

    # -------------------------------------------------------------------------
    # STEP 5d: Process files (with progress bar)
    # -------------------------------------------------------------------------
    results = []

    if parallel <= 1:
        # Sequential processing — simpler and easier to debug
        for filepath in tqdm(txt_files, desc="Processing", unit="file"):
            result = process_file(filepath, output_path, voice, model)
            results.append(result)
            if result["status"] == "failed":
                tqdm.write(f"   ❌ {result['file']}: {result['error']}")
    else:
        # Parallel processing — faster for large batches
        with ThreadPoolExecutor(max_workers=parallel) as executor:
            futures = {
                executor.submit(process_file, fp, output_path, voice, model): fp
                for fp in txt_files
            }
            for future in tqdm(
                as_completed(futures), total=len(futures), desc="Processing", unit="file"
            ):
                result = future.result()
                results.append(result)
                if result["status"] == "failed":
                    tqdm.write(f"   ❌ {result['file']}: {result['error']}")

    # -------------------------------------------------------------------------
    # STEP 5e: Summary report
    # -------------------------------------------------------------------------
    total_time = time.time() - batch_start
    succeeded = [r for r in results if r["status"] == "success"]
    failed = [r for r in results if r["status"] == "failed"]
    total_audio = sum(r["audio_duration"] for r in succeeded)

    click.echo(f"\n{'=' * 50}")
    click.echo(f"📊 Batch Processing Summary")
    click.echo(f"{'=' * 50}")
    click.echo(f"   ✅ Succeeded:       {len(succeeded)}")
    click.echo(f"   ❌ Failed:          {len(failed)}")
    click.echo(f"   📁 Total files:     {len(results)}")
    click.echo(f"   🎵 Audio generated: {total_audio:.1f}s")
    click.echo(f"   ⏱️  Total time:      {total_time:.1f}s")

    if failed:
        click.echo(f"\n⚠️  Failed files:")
        for r in failed:
            click.echo(f"   • {r['file']}: {r['error']}")

    click.echo()


# =============================================================================
# STEP 6: Run!
# =============================================================================

if __name__ == "__main__":
    main()
