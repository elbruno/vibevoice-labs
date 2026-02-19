"""
VibeVoice TTS - Real-Time Streaming Demo
=========================================
This script demonstrates VibeVoice's TTS capability with chunked playback.
Audio is generated using the VibeVoice-Realtime-0.5B model, then played
in chunks to simulate streaming playback for low-latency applications.

Model: microsoft/VibeVoice-Realtime-0.5B
- Full generation then chunked playback
- 24 kHz output sample rate

Prerequisites:
  pip install "vibevoice[streamingtts] @ git+https://github.com/microsoft/VibeVoice.git"
  pip install sounddevice soundfile
"""

# =============================================================================
# STEP 1: Import Required Libraries
# =============================================================================

from vibevoice.modular.modeling_vibevoice_streaming_inference import VibeVoiceStreamingForConditionalGenerationInference
from vibevoice.processor.vibevoice_streaming_processor import VibeVoiceStreamingProcessor
import torch
import copy
import numpy as np
import time
import sys
import os
import glob

# Try to import sounddevice for real-time playback.
try:
    import sounddevice as sd
    PLAYBACK_AVAILABLE = True
except (ImportError, OSError):
    PLAYBACK_AVAILABLE = False
    print("sounddevice not available -- will save to file instead.")

try:
    import soundfile as sf
    SAVE_AVAILABLE = True
except ImportError:
    SAVE_AVAILABLE = False

# =============================================================================
# STEP 2: Download Voice Presets (first run only)
# =============================================================================

VOICES_DIR = os.path.join(os.path.dirname(os.path.abspath(__file__)), "voices")
MODEL_NAME = "microsoft/VibeVoice-Realtime-0.5B"
SAMPLE_RATE = 24000


def download_voices():
    """Download English voice presets from the VibeVoice GitHub repo."""
    if os.path.exists(VOICES_DIR) and glob.glob(os.path.join(VOICES_DIR, "*.pt")):
        return
    os.makedirs(VOICES_DIR, exist_ok=True)
    import urllib.request
    base_url = "https://raw.githubusercontent.com/microsoft/VibeVoice/main/demo/voices/streaming_model"
    voices = ["en-Carter_man.pt", "en-Emma_woman.pt", "en-Frank_man.pt", "en-Grace_woman.pt"]
    for vf in voices:
        dest = os.path.join(VOICES_DIR, vf)
        if not os.path.exists(dest):
            print(f"  Downloading {vf}...")
            urllib.request.urlretrieve(f"{base_url}/{vf}", dest)


download_voices()

# =============================================================================
# STEP 3: Load the VibeVoice Model
# =============================================================================

print("Loading VibeVoice-Realtime-0.5B model...")
load_start = time.perf_counter()

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

load_elapsed = time.perf_counter() - load_start
print(f"Model loaded on {device} in {load_elapsed:.2f}s")

# =============================================================================
# STEP 4: Select Voice and Define Text
# =============================================================================

SPEAKER_NAME = "Carter"   # Default male voice
# SPEAKER_NAME = "Emma"    # Female voice
# SPEAKER_NAME = "Frank"   # Male voice
# SPEAKER_NAME = "Grace"   # Female voice

# Load voice preset
voice_files = [f for f in glob.glob(os.path.join(VOICES_DIR, "*.pt"))
               if SPEAKER_NAME.lower() in os.path.basename(f).lower()]
if not voice_files:
    raise FileNotFoundError(f"No voice preset for '{SPEAKER_NAME}'")

all_prefilled_outputs = torch.load(voice_files[0], map_location=device, weights_only=False)
print(f"Using voice: {SPEAKER_NAME}")

text = (
    "Welcome to VibeVoice Labs! This demonstration showcases real-time "
    "streaming text-to-speech synthesis. Instead of waiting for the entire "
    "audio to be generated before you hear anything, VibeVoice sends audio "
    "chunks to your speaker the moment they are ready. That means the first "
    "words reach your ears in roughly three hundred milliseconds. This is "
    "incredibly useful for voice assistants, accessibility tools, and any "
    "application where perceived latency matters."
)

# =============================================================================
# STEP 5: Generate Audio and Play in Chunks
# =============================================================================

CHUNK_SIZE = SAMPLE_RATE // 4  # 250ms chunks

print(f"\nGenerating TTS for {len(text)} characters...")
print(f"   Speaker:  {SPEAKER_NAME}")
print(f"   Playback: {'real-time via sounddevice' if PLAYBACK_AVAILABLE else 'file-only (no audio device)'}")
print()

gen_start = time.perf_counter()

inputs = processor.process_input_with_cached_prompt(
    text=text,
    cached_prompt=all_prefilled_outputs,
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
    all_prefilled_outputs=copy.deepcopy(all_prefilled_outputs),
)

full_audio = output.speech_outputs[0]
if hasattr(full_audio, "cpu"):
    full_audio = full_audio.cpu().numpy()

gen_elapsed = time.perf_counter() - gen_start

# Split into chunks for simulated streaming playback
all_chunks = [full_audio[i:i + CHUNK_SIZE] for i in range(0, len(full_audio), CHUNK_SIZE)]
chunk_count = len(all_chunks)

play_start = time.perf_counter()

for idx, chunk in enumerate(all_chunks):
    if PLAYBACK_AVAILABLE:
        sd.play(chunk, samplerate=SAMPLE_RATE, blocking=True)
    bar = "#" * (idx + 1)
    samples = sum(len(all_chunks[j]) for j in range(idx + 1))
    elapsed = time.perf_counter() - play_start
    sys.stdout.write(
        f"\r   Chunks: {bar} {idx + 1}/{chunk_count}  |  "
        f"Samples: {samples:,}  |  Elapsed: {elapsed:.2f}s"
    )
    sys.stdout.flush()

play_elapsed = time.perf_counter() - play_start

# =============================================================================
# STEP 6: Show Timing Info
# =============================================================================

audio_duration = len(full_audio) / SAMPLE_RATE

print("\n")
print("=" * 56)
print("  Streaming Performance Summary")
print("=" * 56)
print(f"  Generation time     : {gen_elapsed:.2f} s")
print(f"  Playback time       : {play_elapsed:.2f} s")
print(f"  Chunks played       : {chunk_count}")
print(f"  Audio duration      : {audio_duration:.2f} s")
print(f"  Real-time factor    : {audio_duration / gen_elapsed:.2f}x")
print("=" * 56)

# =============================================================================
# Optional: Save the Full Audio to a WAV File
# =============================================================================

output_filename = "stream_output.wav"

if SAVE_AVAILABLE and len(full_audio) > 0:
    sf.write(output_filename, full_audio, SAMPLE_RATE)
    file_size = os.path.getsize(output_filename)
    print(f"\nAudio saved to {output_filename} ({file_size / 1024:.1f} KB)")
else:
    if not SAVE_AVAILABLE:
        print("\nsoundfile not installed -- skipping WAV export.")
        print("   Install with: pip install soundfile")

print("\nDone! Real-time streaming TTS complete.")
