"""
VibeVoice TTS - Multilingual Demo Script
=========================================
This script demonstrates how to use the VibeVoice-Realtime-0.5B model
for text-to-speech synthesis in multiple languages, including Spanish.

Model: microsoft/VibeVoice-Realtime-0.5B
- 0.5 billion parameters
- ~200ms first audible latency (real-time capable)
- Supports 10 languages: English, German, French, Italian, Japanese,
  Korean, Dutch, Polish, Portuguese, and Spanish

Prerequisites:
  pip install "vibevoice[streamingtts] @ git+https://github.com/microsoft/VibeVoice.git"
"""

# =============================================================================
# STEP 1: Import Required Libraries
# =============================================================================
from vibevoice.modular.modeling_vibevoice_streaming_inference import VibeVoiceStreamingForConditionalGenerationInference
from vibevoice.processor.vibevoice_streaming_processor import VibeVoiceStreamingProcessor
import torch
import copy
import os
import glob

# =============================================================================
# STEP 2: Language and Voice Configuration
# =============================================================================
# VibeVoice supports multiple languages with different voice presets.
# Each language has specific voice files available.

LANGUAGE_CONFIG = {
    "en": {
        "name": "English",
        "voices": ["Carter_man", "Davis_man", "Emma_woman", "Frank_man", "Grace_woman", "Mike_man"],
        "sample_text": "Hello! This is a demonstration of VibeVoice text-to-speech in English."
    },
    "sp": {
        "name": "Spanish",
        "voices": ["Spk1_man"],
        "sample_text": "¡Hola! Esta es una demostración de VibeVoice texto a voz en español. Estamos en Jueves en Quack."
    },
    "de": {
        "name": "German",
        "voices": ["Spk0_man", "Spk1_woman"],
        "sample_text": "Hallo! Dies ist eine Demonstration von VibeVoice Text-zu-Sprache auf Deutsch."
    },
    "fr": {
        "name": "French",
        "voices": ["Spk0_man", "Spk1_woman"],
        "sample_text": "Bonjour! Ceci est une démonstration de VibeVoice synthèse vocale en français."
    },
    "it": {
        "name": "Italian",
        "voices": ["Spk0_woman", "Spk1_man"],
        "sample_text": "Ciao! Questa è una dimostrazione di VibeVoice sintesi vocale in italiano."
    },
    "pt": {
        "name": "Portuguese",
        "voices": ["Spk0_woman", "Spk1_man"],
        "sample_text": "Olá! Esta é uma demonstração do VibeVoice texto para fala em português."
    },
    "jp": {
        "name": "Japanese",
        "voices": ["Spk0_man", "Spk1_woman"],
        "sample_text": "こんにちは！これはVibeVoiceのテキスト音声変換のデモです。"
    },
    "kr": {
        "name": "Korean",
        "voices": ["Spk0_woman", "Spk1_man"],
        "sample_text": "안녕하세요! VibeVoice 텍스트 음성 변환 데모입니다."
    },
    "nl": {
        "name": "Dutch",
        "voices": ["Spk0_man", "Spk1_woman"],
        "sample_text": "Hallo! Dit is een demonstratie van VibeVoice tekst-naar-spraak in het Nederlands."
    },
    "pl": {
        "name": "Polish",
        "voices": ["Spk0_man", "Spk1_woman"],
        "sample_text": "Cześć! To jest demonstracja VibeVoice tekstu na mowę po polsku."
    },
}

# =============================================================================
# STEP 3: Select Language and Voice
# =============================================================================
# Change LANGUAGE to any supported language code (en, sp, de, fr, it, pt, jp, kr, nl, pl)
# Change VOICE_INDEX to select different voices (0 or 1 for most languages)

LANGUAGE = "sp"           # Spanish (change to "en" for English, "fr" for French, etc.)
VOICE_INDEX = 0           # 0 = first voice, 1 = second voice

# Custom text (optional - leave empty to use sample text for the language)
CUSTOM_TEXT = ""

# =============================================================================
# STEP 4: Download Voice Presets
# =============================================================================
VOICES_DIR = os.path.join(os.path.dirname(os.path.abspath(__file__)), "voices")

def download_voices():
    """Download voice presets for all supported languages from the VibeVoice GitHub repo."""
    print("Checking voice presets...")
    os.makedirs(VOICES_DIR, exist_ok=True)
    
    import urllib.request
    
    base_url = "https://raw.githubusercontent.com/microsoft/VibeVoice/main/demo/voices/streaming_model"
    
    # Build list of all voice files
    voices = []
    for lang_code, config in LANGUAGE_CONFIG.items():
        for voice in config["voices"]:
            voices.append(f"{lang_code}-{voice}.pt")
    
    # Download missing files
    downloaded = 0
    for voice_file in voices:
        dest = os.path.join(VOICES_DIR, voice_file)
        if not os.path.exists(dest):
            url = f"{base_url}/{voice_file}"
            print(f"  Downloading {voice_file}...")
            try:
                urllib.request.urlretrieve(url, dest)
                downloaded += 1
            except Exception as e:
                print(f"  Warning: Could not download {voice_file}: {e}")
    
    if downloaded > 0:
        print(f"  Downloaded {downloaded} voice preset(s)")
    else:
        print("  All voice presets already available")

download_voices()

# =============================================================================
# STEP 5: Load the VibeVoice Model and Processor
# =============================================================================
MODEL_NAME = "microsoft/VibeVoice-Realtime-0.5B"

print(f"\nLoading VibeVoice-Realtime-0.5B model...")
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
# STEP 6: Load the Selected Voice
# =============================================================================
lang_config = LANGUAGE_CONFIG.get(LANGUAGE)
if not lang_config:
    raise ValueError(f"Unsupported language: {LANGUAGE}. Supported: {list(LANGUAGE_CONFIG.keys())}")

if VOICE_INDEX >= len(lang_config["voices"]):
    raise ValueError(f"Voice index {VOICE_INDEX} out of range. Available voices for {lang_config['name']}: {lang_config['voices']}")

voice_name = lang_config["voices"][VOICE_INDEX]
voice_filename = f"{LANGUAGE}-{voice_name}.pt"
voice_path = os.path.join(VOICES_DIR, voice_filename)

if not os.path.exists(voice_path):
    raise FileNotFoundError(f"Voice preset not found: {voice_path}")

print(f"\nUsing voice: {voice_name} ({lang_config['name']})")

# Load the pre-computed voice outputs
all_prefilled_outputs = torch.load(voice_path, map_location=device, weights_only=False)

# =============================================================================
# STEP 7: Define the Text to Synthesize
# =============================================================================
text = CUSTOM_TEXT if CUSTOM_TEXT else lang_config["sample_text"]

print(f"\nLanguage: {lang_config['name']}")
print(f"Text: {text}")

# =============================================================================
# STEP 8: Generate Audio
# =============================================================================
print(f"\nGenerating audio...")

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
# STEP 9: Save Audio to WAV File
# =============================================================================
output_filename = f"output_{LANGUAGE}.wav"

print(f"Saving audio to {output_filename}...")
audio = output.speech_outputs[0]
processor.save_audio(audio, output_path=output_filename)

# =============================================================================
# STEP 10: Confirmation
# =============================================================================
file_size = os.path.getsize(output_filename)
sample_rate = 24000
audio_samples = audio.shape[-1] if len(audio.shape) > 0 else len(audio)
audio_duration = audio_samples / sample_rate

print(f"\n✅ Audio generated successfully!")
print(f"   File:     {output_filename}")
print(f"   Size:     {file_size / 1024:.1f} KB")
print(f"   Duration: {audio_duration:.2f}s")
print(f"   Language: {lang_config['name']}")
print(f"   Voice:    {voice_name}")


# =============================================================================
# BONUS: Generate Audio in Multiple Languages
# =============================================================================
def generate_all_languages():
    """
    Generate sample audio in all supported languages.
    Uncomment the call to this function at the bottom to run.
    """
    print("\n" + "="*60)
    print("Generating audio in all supported languages...")
    print("="*60)
    
    for lang_code, config in LANGUAGE_CONFIG.items():
        voice_name = config["voices"][0]  # Use first voice
        voice_filename = f"{lang_code}-{voice_name}.pt"
        voice_path = os.path.join(VOICES_DIR, voice_filename)
        
        if not os.path.exists(voice_path):
            print(f"\n⚠️ Skipping {config['name']}: voice preset not found")
            continue
        
        print(f"\n🎤 {config['name']}...")
        
        # Load voice
        prefilled = torch.load(voice_path, map_location=device, weights_only=False)
        
        # Process text
        inputs = processor.process_input_with_cached_prompt(
            text=config["sample_text"],
            cached_prompt=prefilled,
            padding=True,
            return_tensors="pt",
            return_attention_mask=True,
        )
        
        for k, v in inputs.items():
            if torch.is_tensor(v):
                inputs[k] = v.to(device)
        
        # Generate
        out = model.generate(
            **inputs,
            tokenizer=processor.tokenizer,
            cfg_scale=1.5,
            generation_config={"do_sample": False},
            all_prefilled_outputs=copy.deepcopy(prefilled),
        )
        
        # Save
        out_file = f"output_{lang_code}.wav"
        processor.save_audio(out.speech_outputs[0], output_path=out_file)
        print(f"   ✅ Saved: {out_file}")
    
    print("\n" + "="*60)
    print("Done! Audio files generated for all languages.")
    print("="*60)


# Uncomment the line below to generate audio in ALL supported languages:
# generate_all_languages()
