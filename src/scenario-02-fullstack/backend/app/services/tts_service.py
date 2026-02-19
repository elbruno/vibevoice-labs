"""
TTS Service - Handles VibeVoice model loading and audio generation.
"""

import io
import os
import copy
import glob
import logging
from typing import List, Optional

import numpy as np
import soundfile as sf
import torch

from app.models.schemas import Voice

logger = logging.getLogger(__name__)

# Voice registry mapping API voice IDs to voice preset filenames
VOICES_REGISTRY: List[Voice] = [
    Voice(id="en-carter", name="Carter", language="en", style="male"),
    Voice(id="en-davis", name="Davis", language="en", style="male"),
    Voice(id="en-emma", name="Emma", language="en", style="female"),
    Voice(id="en-frank", name="Frank", language="en", style="male"),
    Voice(id="en-grace", name="Grace", language="en", style="female"),
    Voice(id="en-mike", name="Mike", language="en", style="male"),
]

VOICE_ID_TO_PRESET = {
    "en-carter": "en-Carter_man.pt",
    "en-davis": "en-Davis_man.pt",
    "en-emma": "en-Emma_woman.pt",
    "en-frank": "en-Frank_man.pt",
    "en-grace": "en-Grace_woman.pt",
    "en-mike": "en-Mike_man.pt",
}

VOICES_DIR = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "voices")


def download_voices():
    """Download English voice presets from the VibeVoice GitHub repo if not present."""
    voices_dir = os.path.abspath(VOICES_DIR)
    if os.path.exists(voices_dir) and glob.glob(os.path.join(voices_dir, "*.pt")):
        return
    os.makedirs(voices_dir, exist_ok=True)
    import urllib.request
    base_url = "https://raw.githubusercontent.com/microsoft/VibeVoice/main/demo/voices/streaming_model"
    for filename in set(VOICE_ID_TO_PRESET.values()):
        dest = os.path.join(voices_dir, filename)
        if not os.path.exists(dest):
            logger.info(f"Downloading voice preset: {filename}")
            urllib.request.urlretrieve(f"{base_url}/{filename}", dest)


class TTSService:
    """Singleton service for text-to-speech generation using VibeVoice."""

    _model = None
    _processor = None
    _device = "cpu"
    _initialized = False
    _voice_cache: dict = {}

    @classmethod
    def initialize(cls) -> None:
        """Load the VibeVoice model. Called on app startup."""
        if cls._initialized:
            return

        try:
            logger.info("Downloading voice presets (if needed)...")
            download_voices()

            logger.info("Loading VibeVoice-Realtime-0.5B model...")
            from vibevoice.modular.modeling_vibevoice_streaming_inference import VibeVoiceStreamingForConditionalGenerationInference
            from vibevoice.processor.vibevoice_streaming_processor import VibeVoiceStreamingProcessor

            MODEL_NAME = "microsoft/VibeVoice-Realtime-0.5B"
            cls._processor = VibeVoiceStreamingProcessor.from_pretrained(MODEL_NAME)

            cls._device = "cuda" if torch.cuda.is_available() else "cpu"
            dtype = torch.bfloat16 if cls._device == "cuda" else torch.float32
            attn_impl = "flash_attention_2" if cls._device == "cuda" else "sdpa"

            try:
                cls._model = VibeVoiceStreamingForConditionalGenerationInference.from_pretrained(
                    MODEL_NAME, torch_dtype=dtype, attn_implementation=attn_impl,
                    device_map=cls._device if cls._device == "cuda" else "cpu",
                )
            except Exception:
                cls._model = VibeVoiceStreamingForConditionalGenerationInference.from_pretrained(
                    MODEL_NAME, torch_dtype=dtype, attn_implementation="sdpa", device_map="cpu",
                )
                cls._device = "cpu"

            cls._model.eval()
            cls._model.set_ddpm_inference_steps(num_steps=5)
            cls._initialized = True
            logger.info(f"VibeVoice model loaded successfully on {cls._device}")
        except Exception as e:
            logger.error(f"Failed to load VibeVoice model: {e}")
            raise

    @classmethod
    def _load_voice_preset(cls, voice_id: str):
        """Load and cache a voice preset file."""
        if voice_id in cls._voice_cache:
            return cls._voice_cache[voice_id]

        preset_file = VOICE_ID_TO_PRESET.get(voice_id, "en-Carter_man.pt")
        voices_dir = os.path.abspath(VOICES_DIR)
        path = os.path.join(voices_dir, preset_file)

        if not os.path.exists(path):
            logger.warning(f"Voice preset not found: {path}, using default")
            path = os.path.join(voices_dir, "en-Carter_man.pt")

        prefilled = torch.load(path, map_location=cls._device, weights_only=False)
        cls._voice_cache[voice_id] = prefilled
        return prefilled

    @classmethod
    def is_model_loaded(cls) -> bool:
        return cls._initialized and cls._model is not None

    @classmethod
    def get_voices(cls) -> List[Voice]:
        return VOICES_REGISTRY

    @classmethod
    def get_voice_by_id(cls, voice_id: str) -> Optional[Voice]:
        for voice in VOICES_REGISTRY:
            if voice.id == voice_id:
                return voice
        return None

    @classmethod
    def generate_audio(cls, text: str, voice_id: str) -> bytes:
        """Generate audio from text using the specified voice."""
        if not cls.is_model_loaded():
            raise RuntimeError("TTS model is not loaded")

        prefilled = cls._load_voice_preset(voice_id)

        logger.info(f"Generating audio: text='{text[:50]}...', voice={voice_id}")

        inputs = cls._processor.process_input_with_cached_prompt(
            text=text,
            cached_prompt=prefilled,
            padding=True,
            return_tensors="pt",
            return_attention_mask=True,
        )
        for k, v in inputs.items():
            if torch.is_tensor(v):
                inputs[k] = v.to(cls._device)

        output = cls._model.generate(
            **inputs,
            tokenizer=cls._processor.tokenizer,
            cfg_scale=1.5,
            generation_config={"do_sample": False},
            all_prefilled_outputs=copy.deepcopy(prefilled),
        )

        audio = output.speech_outputs[0]
        wav_bytes = cls._audio_to_wav_bytes(audio)
        logger.info(f"Audio generated: {len(wav_bytes)} bytes")
        return wav_bytes

    @staticmethod
    def _audio_to_wav_bytes(audio, sample_rate: int = 24000) -> bytes:
        """Convert audio tensor/array to WAV bytes."""
        if hasattr(audio, "cpu"):
            audio = audio.cpu().numpy()
        buffer = io.BytesIO()
        sf.write(buffer, audio, sample_rate, format="WAV")
        buffer.seek(0)
        return buffer.read()
