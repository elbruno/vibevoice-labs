"""
TTS Service - Handles VibeVoice model loading and audio generation.
"""

import io
import logging
from typing import List, Optional

import numpy as np
import soundfile as sf

from app.models.schemas import Voice

logger = logging.getLogger(__name__)

# Voice registry with metadata
VOICES_REGISTRY: List[Voice] = [
    # English voices (11 styles available)
    Voice(id="en-US-Aria", name="Aria", language="en-US", style="general"),
    Voice(id="en-US-Guy", name="Guy", language="en-US", style="general"),
    Voice(id="en-US-Jenny", name="Jenny", language="en-US", style="conversational"),
    Voice(id="en-GB-Sonia", name="Sonia", language="en-GB", style="general"),
    Voice(id="en-AU-Natasha", name="Natasha", language="en-AU", style="general"),
    
    # Multilingual voices
    Voice(id="de-DE-Katja", name="Katja", language="de-DE", style="general"),
    Voice(id="fr-FR-Denise", name="Denise", language="fr-FR", style="general"),
    Voice(id="it-IT-Elsa", name="Elsa", language="it-IT", style="general"),
    Voice(id="es-ES-Elvira", name="Elvira", language="es-ES", style="general"),
    Voice(id="pt-BR-Francisca", name="Francisca", language="pt-BR", style="general"),
    Voice(id="nl-NL-Colette", name="Colette", language="nl-NL", style="general"),
    Voice(id="pl-PL-Paulina", name="Paulina", language="pl-PL", style="general"),
    Voice(id="ja-JP-Nanami", name="Nanami", language="ja-JP", style="general"),
    Voice(id="ko-KR-SunHi", name="SunHi", language="ko-KR", style="general"),
]

# Mapping from API voice IDs to VibeVoice speaker codes
VOICE_ID_TO_SPEAKER = {
    "en-US-Aria": "EN-Default",
    "en-US-Guy": "EN-US",
    "en-US-Jenny": "EN-US",
    "en-GB-Sonia": "EN-BR",
    "en-AU-Natasha": "EN-AU",
    "de-DE-Katja": "DE",
    "fr-FR-Denise": "FR",
    "it-IT-Elsa": "IT",
    "es-ES-Elvira": "ES",
    "pt-BR-Francisca": "PT",
    "nl-NL-Colette": "NL",
    "pl-PL-Paulina": "PL",
    "ja-JP-Nanami": "JP",
    "ko-KR-SunHi": "KR",
}


class TTSService:
    """
    Singleton service for text-to-speech generation using VibeVoice.
    """
    
    _model = None
    _initialized = False
    
    @classmethod
    def initialize(cls) -> None:
        """Load the VibeVoice model. Called on app startup."""
        if cls._initialized:
            return
            
        try:
            logger.info("Loading VibeVoice-Realtime-0.5B model...")
            from vibevoice_realtime import VibeVoiceRealtime
            cls._model = VibeVoiceRealtime.from_pretrained("microsoft/VibeVoice-Realtime-0.5B")
            cls._initialized = True
            logger.info("VibeVoice model loaded successfully")
        except Exception as e:
            logger.error(f"Failed to load VibeVoice model: {e}")
            # Don't mark as initialized so we can retry
            raise
    
    @classmethod
    def is_model_loaded(cls) -> bool:
        """Check if the TTS model is loaded and ready."""
        return cls._initialized and cls._model is not None
    
    @classmethod
    def get_voices(cls) -> List[Voice]:
        """Return list of available voices with metadata."""
        return VOICES_REGISTRY
    
    @classmethod
    def get_voice_by_id(cls, voice_id: str) -> Optional[Voice]:
        """Look up a voice by its ID."""
        for voice in VOICES_REGISTRY:
            if voice.id == voice_id:
                return voice
        return None
    
    @classmethod
    def generate_audio(cls, text: str, voice_id: str) -> bytes:
        """
        Generate audio from text using the specified voice.
        
        Args:
            text: The text to synthesize
            voice_id: Voice ID from the voices registry
            
        Returns:
            WAV audio as bytes
            
        Raises:
            ValueError: If voice_id is invalid
            RuntimeError: If model is not loaded
        """
        if not cls.is_model_loaded():
            raise RuntimeError("TTS model is not loaded")
        
        # Map voice ID to VibeVoice speaker code
        speaker = VOICE_ID_TO_SPEAKER.get(voice_id)
        if speaker is None:
            # Default to EN-Default for unknown voices
            logger.warning(f"Unknown voice_id '{voice_id}', using EN-Default")
            speaker = "EN-Default"
        
        logger.info(f"Generating audio: text='{text[:50]}...', voice={voice_id}, speaker={speaker}")
        
        # Generate audio using VibeVoice
        audio = cls._model.generate(text=text, speaker=speaker)
        
        # Convert to WAV bytes
        wav_bytes = cls._audio_to_wav_bytes(audio)
        
        logger.info(f"Audio generated: {len(wav_bytes)} bytes")
        return wav_bytes
    
    @staticmethod
    def _audio_to_wav_bytes(audio: np.ndarray, sample_rate: int = 24000) -> bytes:
        """
        Convert numpy audio array to WAV bytes.
        
        Args:
            audio: Audio samples as numpy array
            sample_rate: Sample rate in Hz (VibeVoice uses 24kHz)
            
        Returns:
            WAV file as bytes
        """
        buffer = io.BytesIO()
        sf.write(buffer, audio, sample_rate, format="WAV")
        buffer.seek(0)
        return buffer.read()
