"""
VibeVoice TTS - Simple Demo Script
==================================
This script demonstrates how to use the VibeVoice-Realtime-0.5B model
for text-to-speech synthesis. Follow along with the step-by-step comments
to understand how the TTS pipeline works.

Model: microsoft/VibeVoice-Realtime-0.5B
- 0.5 billion parameters
- ~300ms first audible latency (real-time capable)
- Supports streaming and up to ~10 minute long-form generation
"""

# =============================================================================
# STEP 1: Import Required Libraries
# =============================================================================
# vibevoice_realtime: The main TTS model from Microsoft
# soundfile: For saving audio to WAV files
# numpy: For audio data manipulation (comes with soundfile)

from vibevoice_realtime import VibeVoiceRealtime
import soundfile as sf
import os

# =============================================================================
# STEP 2: Load the VibeVoice Model
# =============================================================================
# The model is downloaded from HuggingFace on first run (~1GB download)
# Subsequent runs use the cached model
# Note: Requires a GPU with CUDA for best performance, but works on CPU too

print("Loading VibeVoice-Realtime-0.5B model...")
model = VibeVoiceRealtime.from_pretrained("microsoft/VibeVoice-Realtime-0.5B")
print("Model loaded successfully!")

# =============================================================================
# STEP 3: Define the Text to Synthesize
# =============================================================================
# VibeVoice supports up to ~10 minutes of audio generation
# For best results, use natural text with proper punctuation

text = "Hello! Welcome to VibeVoice Labs. This is a demonstration of the VibeVoice text-to-speech system."

# =============================================================================
# STEP 4: Generate Audio
# =============================================================================
# The generate() method takes text and returns audio samples
# Speaker parameter controls the voice style

print(f"Generating audio for: '{text}'")

# Default English voice
audio = model.generate(text=text, speaker="EN-Default")

# =============================================================================
# AVAILABLE VOICES / SPEAKERS
# =============================================================================
# VibeVoice supports multiple languages and voice styles.
# Uncomment any of the following lines to try different voices:

# --- English Voices (11 styles) ---
# audio = model.generate(text=text, speaker="EN-Default")     # Standard English
# audio = model.generate(text=text, speaker="EN-US")          # American English
# audio = model.generate(text=text, speaker="EN-BR")          # British English
# audio = model.generate(text=text, speaker="EN-AU")          # Australian English

# --- Multilingual Voices ---
# audio = model.generate(text="Guten Tag! Willkommen bei VibeVoice.", speaker="DE")     # German
# audio = model.generate(text="Bonjour! Bienvenue à VibeVoice.", speaker="FR")          # French
# audio = model.generate(text="Ciao! Benvenuto a VibeVoice.", speaker="IT")             # Italian
# audio = model.generate(text="¡Hola! Bienvenido a VibeVoice.", speaker="ES")           # Spanish
# audio = model.generate(text="Olá! Bem-vindo ao VibeVoice.", speaker="PT")             # Portuguese
# audio = model.generate(text="Hallo! Welkom bij VibeVoice.", speaker="NL")             # Dutch
# audio = model.generate(text="Cześć! Witamy w VibeVoice.", speaker="PL")               # Polish
# audio = model.generate(text="こんにちは！VibeVoice へようこそ。", speaker="JP")        # Japanese
# audio = model.generate(text="안녕하세요! VibeVoice에 오신 것을 환영합니다.", speaker="KR")  # Korean

# =============================================================================
# STEP 5: Save Audio to WAV File
# =============================================================================
# VibeVoice outputs audio at 24kHz sample rate
# We use soundfile to save as a standard WAV file

output_filename = "output.wav"
sample_rate = 24000  # VibeVoice default sample rate

print(f"Saving audio to {output_filename}...")
sf.write(output_filename, audio, sample_rate)

# =============================================================================
# STEP 6: Confirmation
# =============================================================================
# Report success and provide file info

file_size = os.path.getsize(output_filename)
duration_seconds = len(audio) / sample_rate

print(f"\n✅ Audio generated successfully!")
print(f"   File: {output_filename}")
print(f"   Size: {file_size / 1024:.1f} KB")
print(f"   Duration: {duration_seconds:.2f} seconds")
print(f"   Sample Rate: {sample_rate} Hz")

# =============================================================================
# ADVANCED: Streaming Generation (Optional)
# =============================================================================
# For longer texts, you can use streaming to start playback before generation
# is complete. This is useful for real-time applications.

# def generate_with_streaming():
#     """Example of streaming audio generation"""
#     long_text = """
#     VibeVoice is a state-of-the-art text-to-speech model developed by Microsoft.
#     It provides natural-sounding speech synthesis with low latency, making it
#     ideal for real-time applications like voice assistants and accessibility tools.
#     """
#     
#     print("Streaming generation...")
#     for audio_chunk in model.generate_stream(text=long_text, speaker="EN-Default"):
#         # Process each chunk as it's generated
#         # You could play this directly or save incrementally
#         print(f"  Received chunk: {len(audio_chunk)} samples")
#
# Uncomment to try streaming:
# generate_with_streaming()
