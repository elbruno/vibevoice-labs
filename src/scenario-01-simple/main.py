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
# VibeVoiceForConditionalGenerationInference: The TTS model from Microsoft
# VibeVoiceProcessor: Handles text tokenization and audio processing
# torch: Required for model inference

from vibevoice.modular.modeling_vibevoice_inference import VibeVoiceForConditionalGenerationInference
from vibevoice.processor.vibevoice_processor import VibeVoiceProcessor
import torch
import os

# =============================================================================
# STEP 2: Load the VibeVoice Model and Processor
# =============================================================================
# The model is downloaded from HuggingFace on first run (~1GB download)
# Subsequent runs use the cached model
# Note: Requires a GPU with CUDA for best performance, but works on CPU too

MODEL_NAME = "microsoft/VibeVoice-Realtime-0.5B"

print("Loading VibeVoice-Realtime-0.5B model...")
processor = VibeVoiceProcessor.from_pretrained(MODEL_NAME)
model = VibeVoiceForConditionalGenerationInference.from_pretrained(MODEL_NAME, torch_dtype=torch.float16)

device = "cuda" if torch.cuda.is_available() else "cpu"
model = model.to(device)
print(f"Model loaded successfully on {device}!")

# =============================================================================
# STEP 3: Define the Text to Synthesize
# =============================================================================
# VibeVoice expects text in the format: "Speaker <id>: <text>"
# Each line should have a speaker identifier followed by the text.
# For best results, use natural text with proper punctuation.

text = "Speaker 1: Hello! Welcome to VibeVoice Labs. This is a demonstration of the VibeVoice text-to-speech system."

# =============================================================================
# STEP 4: Generate Audio
# =============================================================================
# 1. Process the text through the processor to get model inputs
# 2. Generate audio with the model
# 3. Extract speech waveforms from the output

print(f"Generating audio for: '{text}'")

inputs = processor(text=text, return_tensors="pt").to(device)
output = model.generate(**inputs)

# =============================================================================
# MULTILINGUAL EXAMPLES
# =============================================================================
# VibeVoice supports multiple languages. Change the text content directly:
#
# output = model.generate(**processor(text="Speaker 1: Guten Tag! Willkommen bei VibeVoice.", return_tensors="pt").to(device))       # German
# output = model.generate(**processor(text="Speaker 1: Bonjour! Bienvenue à VibeVoice.", return_tensors="pt").to(device))            # French
# output = model.generate(**processor(text="Speaker 1: Ciao! Benvenuto a VibeVoice.", return_tensors="pt").to(device))               # Italian
# output = model.generate(**processor(text="Speaker 1: ¡Hola! Bienvenido a VibeVoice.", return_tensors="pt").to(device))             # Spanish
# output = model.generate(**processor(text="Speaker 1: Olá! Bem-vindo ao VibeVoice.", return_tensors="pt").to(device))               # Portuguese
# output = model.generate(**processor(text="Speaker 1: こんにちは！VibeVoice へようこそ。", return_tensors="pt").to(device))          # Japanese
# output = model.generate(**processor(text="Speaker 1: 안녕하세요! VibeVoice에 오신 것을 환영합니다.", return_tensors="pt").to(device))  # Korean

# =============================================================================
# STEP 5: Save Audio to WAV File
# =============================================================================
# Use the processor's save_audio method to write the generated speech

output_filename = "output.wav"

print(f"Saving audio to {output_filename}...")
audio = output.speech_outputs[0]
processor.save_audio(audio, output_path=output_filename)

# =============================================================================
# STEP 6: Confirmation
# =============================================================================
# Report success and provide file info

file_size = os.path.getsize(output_filename)

print(f"\n✅ Audio generated successfully!")
print(f"   File: {output_filename}")
print(f"   Size: {file_size / 1024:.1f} KB")
