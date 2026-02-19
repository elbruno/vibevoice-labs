"""
VibeVoice TTS - Simple Demo Script
==================================
This script demonstrates how to use the VibeVoice-Realtime-0.5B model
for text-to-speech synthesis. Follow along with the step-by-step comments
to understand how the TTS pipeline works.

Model: microsoft/VibeVoice-Realtime-0.5B
- 0.5 billion parameters
- ~200ms first audible latency (real-time capable)
- Supports streaming text input and ~10 minute long-form generation

Prerequisites:
  pip install "vibevoice[streamingtts] @ git+https://github.com/microsoft/VibeVoice.git"
"""

# =============================================================================
# STEP 1: Import Required Libraries
# =============================================================================
# VibeVoiceStreamingForConditionalGenerationInference: The streaming TTS model
# VibeVoiceStreamingProcessor: Handles text tokenization and audio processing
# torch: Required for model inference
# copy: For deep-copying prefilled voice outputs

from vibevoice.modular.modeling_vibevoice_streaming_inference import VibeVoiceStreamingForConditionalGenerationInference
from vibevoice.processor.vibevoice_streaming_processor import VibeVoiceStreamingProcessor
import torch
import copy
import os
import glob

# =============================================================================
# STEP 2: Download Voice Presets (first run only)
# =============================================================================
# VibeVoice uses pre-computed voice preset files (.pt) that define each
# speaker's voice characteristics. We download them from the VibeVoice repo.

VOICES_DIR = os.path.join(os.path.dirname(os.path.abspath(__file__)), "voices")

def download_voices():
    """Download voice presets from the VibeVoice GitHub repo if not present."""
    if os.path.exists(VOICES_DIR) and glob.glob(os.path.join(VOICES_DIR, "*.pt")):
        return  # Already downloaded
    
    print("Downloading voice presets (first run only)...")
    os.makedirs(VOICES_DIR, exist_ok=True)
    
    from huggingface_hub import hf_hub_url
    import urllib.request
    
    # Voice files are in the GitHub repo, not HuggingFace
    base_url = "https://raw.githubusercontent.com/microsoft/VibeVoice/main/demo/voices/streaming_model"
    voices = [
        "en-Carter_man.pt",
        "en-Davis_man.pt",
        "en-Emma_woman.pt",
        "en-Frank_man.pt",
        "en-Grace_woman.pt",
        "en-Mike_man.pt",
    ]
    
    for voice_file in voices:
        dest = os.path.join(VOICES_DIR, voice_file)
        if not os.path.exists(dest):
            url = f"{base_url}/{voice_file}"
            print(f"  Downloading {voice_file}...")
            urllib.request.urlretrieve(url, dest)
    
    print(f"  Done! Downloaded {len(voices)} voice presets to {VOICES_DIR}")

download_voices()

# =============================================================================
# STEP 3: Load the VibeVoice Model and Processor
# =============================================================================
# The model is downloaded from HuggingFace on first run (~1GB download)
# Subsequent runs use the cached model
# Note: GPU with CUDA recommended; CPU works but is slower

MODEL_NAME = "microsoft/VibeVoice-Realtime-0.5B"

print("Loading VibeVoice-Realtime-0.5B model...")
processor = VibeVoiceStreamingProcessor.from_pretrained(MODEL_NAME)

# Select device and dtype
device = "cuda" if torch.cuda.is_available() else "cpu"
dtype = torch.bfloat16 if device == "cuda" else torch.float32
attn_impl = "flash_attention_2" if device == "cuda" else "sdpa"

try:
    model = VibeVoiceStreamingForConditionalGenerationInference.from_pretrained(
        MODEL_NAME, torch_dtype=dtype, attn_implementation=attn_impl,
        device_map=device if device == "cuda" else "cpu",
    )
except Exception:
    # Fallback if flash_attention_2 is not available
    model = VibeVoiceStreamingForConditionalGenerationInference.from_pretrained(
        MODEL_NAME, torch_dtype=dtype, attn_implementation="sdpa",
        device_map="cpu",
    )
    device = "cpu"

model.eval()
model.set_ddpm_inference_steps(num_steps=5)
print(f"Model loaded successfully on {device}!")

# =============================================================================
# STEP 4: Select a Voice Preset
# =============================================================================
# Each voice is a .pt file containing pre-computed voice characteristics.
# Available English voices: Carter, Davis, Emma, Frank, Grace, Mike
#
# To change voices, uncomment one of the lines below:

SPEAKER_NAME = "Carter"   # Male, clear American English (default)
# SPEAKER_NAME = "Davis"   # Male voice
# SPEAKER_NAME = "Emma"    # Female voice
# SPEAKER_NAME = "Frank"   # Male voice
# SPEAKER_NAME = "Grace"   # Female voice
# SPEAKER_NAME = "Mike"    # Male voice

# Find the voice preset file
voice_files = glob.glob(os.path.join(VOICES_DIR, f"*{SPEAKER_NAME.lower()}*.pt"), recursive=False)
if not voice_files:
    # Case-insensitive search
    voice_files = [f for f in glob.glob(os.path.join(VOICES_DIR, "*.pt"))
                   if SPEAKER_NAME.lower() in os.path.basename(f).lower()]

if not voice_files:
    raise FileNotFoundError(
        f"No voice preset found for '{SPEAKER_NAME}'. "
        f"Available files: {os.listdir(VOICES_DIR)}"
    )

voice_path = voice_files[0]
print(f"Using voice: {SPEAKER_NAME} ({os.path.basename(voice_path)})")

# Load the pre-computed voice outputs
all_prefilled_outputs = torch.load(voice_path, map_location=device, weights_only=False)

# =============================================================================
# STEP 5: Define the Text to Synthesize
# =============================================================================
# Write your text as a plain script. The voice is determined by the preset
# file selected above, not by text annotations.

text = "Hello! Welcome to VibeVoice Labs. This is a demonstration of the VibeVoice text-to-speech system. The model can generate natural sounding speech in real time."

# =============================================================================
# STEP 6: Generate Audio
# =============================================================================
# 1. Process the text with the cached voice prompt
# 2. Generate audio with the model
# 3. Extract speech waveforms from the output

print(f"Generating audio for: '{text[:80]}...'")

inputs = processor.process_input_with_cached_prompt(
    text=text,
    cached_prompt=all_prefilled_outputs,
    padding=True,
    return_tensors="pt",
    return_attention_mask=True,
)

# Move tensors to device
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

# =============================================================================
# STEP 7: Save Audio to WAV File
# =============================================================================
# Use the processor's save_audio method to write the generated speech

output_filename = "output.wav"

print(f"Saving audio to {output_filename}...")
audio = output.speech_outputs[0]
processor.save_audio(audio, output_path=output_filename)

# =============================================================================
# STEP 8: Confirmation
# =============================================================================
# Report success and provide file info

file_size = os.path.getsize(output_filename)
sample_rate = 24000
audio_samples = audio.shape[-1] if len(audio.shape) > 0 else len(audio)
audio_duration = audio_samples / sample_rate

print(f"\n✅ Audio generated successfully!")
print(f"   File:     {output_filename}")
print(f"   Size:     {file_size / 1024:.1f} KB")
print(f"   Duration: {audio_duration:.2f}s")
print(f"   Speaker:  {SPEAKER_NAME}")
