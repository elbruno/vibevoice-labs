"""
VibeVoice TTS - Real-Time Streaming Demo
=========================================
This script demonstrates VibeVoice's streaming TTS capability using
generate_stream(). Audio chunks are played through the speaker the instant
they arrive, showcasing the ~300ms first-audible-latency that makes
VibeVoice ideal for real-time voice applications.

Model: microsoft/VibeVoice-Realtime-0.5B
- Streaming via generate_stream()
- ~300 ms time-to-first-audio
- 24 kHz output sample rate
"""

# =============================================================================
# STEP 1: Import Required Libraries
# =============================================================================
# vibevoice_realtime : The TTS model from Microsoft
# sounddevice       : Real-time audio playback through speakers
# numpy             : Audio buffer manipulation
# queue             : Thread-safe queue for audio chunks
# time              : Measuring latency and throughput
# soundfile         : (optional) saving the full audio to disk

from vibevoice_realtime import VibeVoiceRealtime
import numpy as np
import time
import sys

# Try to import sounddevice for real-time playback.
# If unavailable (e.g. headless server), we fall back to file-only mode.
try:
    import sounddevice as sd
    PLAYBACK_AVAILABLE = True
except (ImportError, OSError):
    PLAYBACK_AVAILABLE = False
    print("⚠️  sounddevice not available — will save to file instead.")

try:
    import soundfile as sf
    SAVE_AVAILABLE = True
except ImportError:
    SAVE_AVAILABLE = False

# =============================================================================
# STEP 2: Load the VibeVoice Model
# =============================================================================
# Downloaded from HuggingFace on first run (~1 GB).
# GPU with CUDA is recommended; CPU works but is slower.

print("🔄 Loading VibeVoice-Realtime-0.5B model...")
load_start = time.perf_counter()
model = VibeVoiceRealtime.from_pretrained("microsoft/VibeVoice-Realtime-0.5B")
load_elapsed = time.perf_counter() - load_start
print(f"✅ Model loaded in {load_elapsed:.2f}s")

# =============================================================================
# STEP 3: Audio Output Configuration
# =============================================================================
# VibeVoice outputs 24 kHz mono audio.

SAMPLE_RATE = 24000  # Hz — VibeVoice native sample rate
CHANNELS = 1

# =============================================================================
# STEP 4: Define the Text to Synthesize
# =============================================================================
# A longer paragraph highlights the streaming benefit: you hear the first
# words almost instantly while the rest is still being generated.

text = (
    "Welcome to VibeVoice Labs! This demonstration showcases real-time "
    "streaming text-to-speech synthesis. Instead of waiting for the entire "
    "audio to be generated before you hear anything, VibeVoice sends audio "
    "chunks to your speaker the moment they are ready. That means the first "
    "words reach your ears in roughly three hundred milliseconds. This is "
    "incredibly useful for voice assistants, accessibility tools, and any "
    "application where perceived latency matters."
)

# --- Commented alternatives for different voices ---
# speaker = "EN-US"       # American English
# speaker = "EN-BR"       # British English
# speaker = "EN-AU"       # Australian English
# speaker = "DE"          # German  — change text accordingly
# speaker = "FR"          # French
# speaker = "ES"          # Spanish
# speaker = "JP"          # Japanese
speaker = "EN-Default"  # Standard English (active)

# =============================================================================
# STEP 5: Stream Generation — Play Each Chunk Immediately
# =============================================================================
# model.generate_stream() yields audio chunks as numpy arrays.
# We push each chunk straight to the speaker via sounddevice and collect
# all chunks so we can optionally save the full audio afterwards.

print(f"\n🎙️  Streaming TTS for {len(text)} characters...")
print(f"   Speaker : {speaker}")
print(f"   Playback: {'🔊 real-time via sounddevice' if PLAYBACK_AVAILABLE else '💾 file-only (no audio device)'}")
print()

all_chunks: list[np.ndarray] = []
chunk_count = 0
first_chunk_time = None

gen_start = time.perf_counter()

for chunk in model.generate_stream(text=text, speaker=speaker):
    now = time.perf_counter()

    # Record time-to-first-chunk (the headline latency metric)
    if first_chunk_time is None:
        first_chunk_time = now - gen_start

    chunk_count += 1
    all_chunks.append(chunk)

    # --- Real-time playback ---
    if PLAYBACK_AVAILABLE:
        # Play chunk immediately (blocking until the device consumes it so
        # chunks queue up naturally without overruns)
        sd.play(chunk, samplerate=SAMPLE_RATE, blocking=True)

    # --- Terminal progress indicator ---
    bar = "█" * chunk_count
    samples = sum(len(c) for c in all_chunks)
    elapsed = now - gen_start
    sys.stdout.write(
        f"\r   Chunks: {bar} {chunk_count}  |  "
        f"Samples: {samples:,}  |  "
        f"Elapsed: {elapsed:.2f}s"
    )
    sys.stdout.flush()

gen_elapsed = time.perf_counter() - gen_start

# =============================================================================
# STEP 6: Show Timing Info
# =============================================================================
# These numbers let you verify the ~300 ms first-audible-latency claim.

full_audio = np.concatenate(all_chunks) if all_chunks else np.array([])
audio_duration = len(full_audio) / SAMPLE_RATE

print("\n")
print("=" * 56)
print("  📊  Streaming Performance Summary")
print("=" * 56)
print(f"  ⏱️  Time to first chunk : {first_chunk_time * 1000:.0f} ms")
print(f"  ⏱️  Total generation    : {gen_elapsed:.2f} s")
print(f"  🔢  Chunks received     : {chunk_count}")
print(f"  🎵  Audio duration      : {audio_duration:.2f} s")
print(f"  ⚡  Real-time factor    : {audio_duration / gen_elapsed:.2f}x")
print("=" * 56)

# =============================================================================
# Optional: Save the Full Audio to a WAV File
# =============================================================================
# Even in streaming mode it's handy to keep a copy on disk.

output_filename = "stream_output.wav"

if SAVE_AVAILABLE and len(full_audio) > 0:
    sf.write(output_filename, full_audio, SAMPLE_RATE)
    import os
    file_size = os.path.getsize(output_filename)
    print(f"\n💾 Audio saved to {output_filename} ({file_size / 1024:.1f} KB)")
else:
    if not SAVE_AVAILABLE:
        print("\n⚠️  soundfile not installed — skipping WAV export.")
    print("   Install with: pip install soundfile")

print("\n🎉 Done! Real-time streaming TTS complete.")
