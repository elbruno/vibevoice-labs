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

Prerequisites:
  pip install "vibevoice[streamingtts] @ git+https://github.com/microsoft/VibeVoice.git"
  pip install click tqdm pyyaml
"""

# =============================================================================
# STEP 1: Import Required Libraries
# =============================================================================

import os
import re
import time
import copy
import glob
from pathlib import Path
from concurrent.futures import ThreadPoolExecutor, as_completed

import click
import yaml
import torch
from tqdm import tqdm

from vibevoice.modular.modeling_vibevoice_streaming_inference import VibeVoiceStreamingForConditionalGenerationInference
from vibevoice.processor.vibevoice_streaming_processor import VibeVoiceStreamingProcessor

# =============================================================================
# STEP 2: Constants and Voice Setup
# =============================================================================

SAMPLE_RATE = 24000
MODEL_NAME = "microsoft/VibeVoice-Realtime-0.5B"
VOICES_DIR = os.path.join(os.path.dirname(os.path.abspath(__file__)), "voices")

# Available voice presets (downloaded from VibeVoice GitHub repo)
VOICE_PRESETS = {
    "carter": "en-Carter_man.pt",
    "davis": "en-Davis_man.pt",
    "emma": "en-Emma_woman.pt",
    "frank": "en-Frank_man.pt",
    "grace": "en-Grace_woman.pt",
    "mike": "en-Mike_man.pt",
    "de-man": "de-Spk0_man.pt",
    "de-woman": "de-Spk1_woman.pt",
    "fr-man": "fr-Spk0_man.pt",
    "fr-woman": "fr-Spk1_woman.pt",
    "es-woman": "sp-Spk0_woman.pt",
    "es-man": "sp-Spk1_man.pt",
}


def download_voices():
    """Download English voice presets from the VibeVoice GitHub repo if not present."""
    if os.path.exists(VOICES_DIR) and glob.glob(os.path.join(VOICES_DIR, "*.pt")):
        return
    os.makedirs(VOICES_DIR, exist_ok=True)
    import urllib.request
    base_url = "https://raw.githubusercontent.com/microsoft/VibeVoice/main/demo/voices/streaming_model"
    for filename in set(VOICE_PRESETS.values()):
        dest = os.path.join(VOICES_DIR, filename)
        if not os.path.exists(dest):
            click.echo(f"  Downloading {filename}...")
            urllib.request.urlretrieve(f"{base_url}/{filename}", dest)


def load_voice(voice_name: str, device: str):
    """Load a voice preset .pt file and return prefilled outputs."""
    voice_name_lower = voice_name.lower()
    if voice_name_lower in VOICE_PRESETS:
        voice_file = VOICE_PRESETS[voice_name_lower]
    else:
        # Try to find a matching .pt file
        matches = [f for f in os.listdir(VOICES_DIR)
                   if voice_name_lower in f.lower() and f.endswith(".pt")]
        if not matches:
            raise FileNotFoundError(
                f"No voice preset for '{voice_name}'. "
                f"Available: {', '.join(VOICE_PRESETS.keys())}"
            )
        voice_file = matches[0]
    path = os.path.join(VOICES_DIR, voice_file)
    return torch.load(path, map_location=device, weights_only=False)


# =============================================================================
# STEP 3: YAML Front-Matter Parser
# =============================================================================
# Text files can optionally include YAML front-matter:
#   ---
#   voice: emma
#   ---
#   Hello world!

FRONT_MATTER_PATTERN = re.compile(r"^---\s*\n(.*?)\n---\s*\n", re.DOTALL)


def parse_text_file(filepath: Path) -> tuple[str, str | None]:
    """Parse a text file, extracting optional YAML front-matter."""
    raw = filepath.read_text(encoding="utf-8")
    match = FRONT_MATTER_PATTERN.match(raw)
    if match:
        front_matter = yaml.safe_load(match.group(1))
        text = raw[match.end():].strip()
        voice = front_matter.get("voice") if isinstance(front_matter, dict) else None
        return text, voice
    return raw.strip(), None


# =============================================================================
# STEP 4: Single File Processor
# =============================================================================

def process_file(
    filepath: Path,
    output_dir: Path,
    default_voice: str,
    model,
    processor,
    device: str,
    default_prefilled,
) -> dict:
    """Process a single text file and generate the corresponding WAV file."""
    result = {
        "file": filepath.name,
        "status": "success",
        "duration": 0.0,
        "audio_duration": 0.0,
        "error": None,
    }
    start = time.time()
    try:
        text, voice_override = parse_text_file(filepath)
        if not text:
            raise ValueError("File is empty")

        # Load voice override if specified, otherwise use default
        if voice_override:
            try:
                prefilled = load_voice(voice_override, device)
            except FileNotFoundError:
                prefilled = default_prefilled
        else:
            prefilled = default_prefilled

        # Generate audio
        inputs = processor.process_input_with_cached_prompt(
            text=text,
            cached_prompt=prefilled,
            padding=True,
            return_tensors="pt",
            return_attention_mask=True,
        )
        for k, v in inputs.items():
            if torch.is_tensor(v):
                inputs[k] = v.to(device)

        output = model.generate(
            **inputs,
            tokenizer=processor.tokenizer,
            cfg_scale=1.5,
            generation_config={"do_sample": False},
            all_prefilled_outputs=copy.deepcopy(prefilled),
        )

        audio = output.speech_outputs[0]
        if hasattr(audio, "cpu"):
            audio_np = audio.cpu().numpy()
        else:
            audio_np = audio

        output_path = output_dir / filepath.with_suffix(".wav").name
        import soundfile as sf
        sf.write(str(output_path), audio_np, SAMPLE_RATE)
        result["audio_duration"] = len(audio_np) / SAMPLE_RATE

    except Exception as e:
        result["status"] = "failed"
        result["error"] = str(e)

    result["duration"] = time.time() - start
    return result


# =============================================================================
# STEP 5: CLI Entry Point
# =============================================================================

@click.command()
@click.option("--input-dir", type=click.Path(exists=True, file_okay=False),
              default="./sample-texts", show_default=True,
              help="Directory containing .txt files to process.")
@click.option("--output-dir", type=click.Path(file_okay=False),
              default="./output", show_default=True,
              help="Directory where .wav files will be saved.")
@click.option("--voice", default="carter", show_default=True,
              help="Default voice preset (carter, emma, frank, grace, davis, mike).")
@click.option("--parallel", default=1, show_default=True, type=int,
              help="Number of files to process concurrently.")
def main(input_dir: str, output_dir: str, voice: str, parallel: int):
    """VibeVoice Batch TTS -- Convert text files to speech!"""
    input_path = Path(input_dir)
    output_path = Path(output_dir)

    txt_files = sorted(input_path.glob("*.txt"))
    if not txt_files:
        click.echo(f"No .txt files found in {input_path}")
        return

    click.echo(f"\nVibeVoice Batch TTS")
    click.echo(f"   Input:    {input_path.resolve()}")
    click.echo(f"   Output:   {output_path.resolve()}")
    click.echo(f"   Voice:    {voice}")
    click.echo(f"   Files:    {len(txt_files)}")
    click.echo(f"   Parallel: {parallel}\n")

    output_path.mkdir(parents=True, exist_ok=True)

    # Download voice presets if needed
    click.echo("Downloading voice presets (if needed)...")
    download_voices()

    # Load model
    click.echo("Loading VibeVoice-Realtime-0.5B model...")
    batch_start = time.time()
    processor = VibeVoiceStreamingProcessor.from_pretrained(MODEL_NAME)

    device = "cuda" if torch.cuda.is_available() else "cpu"
    dtype = torch.bfloat16 if device == "cuda" else torch.float32
    attn_impl = "flash_attention_2" if device == "cuda" else "sdpa"

    try:
        model = VibeVoiceStreamingForConditionalGenerationInference.from_pretrained(
            MODEL_NAME, torch_dtype=dtype, attn_implementation=attn_impl,
            device_map=device if device == "cuda" else "cpu",
        )
    except Exception:
        model = VibeVoiceStreamingForConditionalGenerationInference.from_pretrained(
            MODEL_NAME, torch_dtype=dtype, attn_implementation="sdpa", device_map="cpu",
        )
        device = "cpu"

    model.eval()
    model.set_ddpm_inference_steps(num_steps=5)
    click.echo(f"Model loaded on {device}!\n")

    # Load default voice preset
    default_prefilled = load_voice(voice, device)

    # Process files
    results = []
    if parallel <= 1:
        for filepath in tqdm(txt_files, desc="Processing", unit="file"):
            result = process_file(filepath, output_path, voice, model, processor, device, default_prefilled)
            results.append(result)
            if result["status"] == "failed":
                tqdm.write(f"   FAILED {result['file']}: {result['error']}")
    else:
        with ThreadPoolExecutor(max_workers=parallel) as executor:
            futures = {
                executor.submit(process_file, fp, output_path, voice, model, processor, device, default_prefilled): fp
                for fp in txt_files
            }
            for future in tqdm(as_completed(futures), total=len(futures), desc="Processing", unit="file"):
                result = future.result()
                results.append(result)
                if result["status"] == "failed":
                    tqdm.write(f"   FAILED {result['file']}: {result['error']}")

    # Summary
    total_time = time.time() - batch_start
    succeeded = [r for r in results if r["status"] == "success"]
    failed = [r for r in results if r["status"] == "failed"]
    total_audio = sum(r["audio_duration"] for r in succeeded)

    click.echo(f"\n{'=' * 50}")
    click.echo(f"Batch Processing Summary")
    click.echo(f"{'=' * 50}")
    click.echo(f"   Succeeded:       {len(succeeded)}")
    click.echo(f"   Failed:          {len(failed)}")
    click.echo(f"   Total files:     {len(results)}")
    click.echo(f"   Audio generated: {total_audio:.1f}s")
    click.echo(f"   Total time:      {total_time:.1f}s")

    if failed:
        click.echo(f"\nFailed files:")
        for r in failed:
            click.echo(f"   - {r['file']}: {r['error']}")
    click.echo()


if __name__ == "__main__":
    main()
