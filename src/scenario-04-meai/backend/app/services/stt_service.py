"""
STT Service - Speech-to-text using NVIDIA Parakeet or faster-whisper fallback.
"""

import io
import os
import tempfile
import logging

logger = logging.getLogger(__name__)

# Try NVIDIA Parakeet first, fall back to faster-whisper
_stt_backend = None

try:
    import nemo.collections.asr as nemo_asr
    _stt_backend = "nemo"
    logger.info("STT backend: NVIDIA NeMo Parakeet")
except ImportError:
    try:
        from faster_whisper import WhisperModel
        _stt_backend = "faster-whisper"
        logger.info("STT backend: faster-whisper")
    except ImportError:
        logger.warning("No STT backend available. Install nemo_toolkit[asr] or faster-whisper.")


class STTService:
    """Speech-to-text service with automatic backend selection."""

    _model = None
    _backend = _stt_backend
    _initialized = False

    @classmethod
    def initialize(cls) -> None:
        """Load the STT model."""
        if cls._initialized:
            return

        if cls._backend == "nemo":
            import nemo.collections.asr as nemo_asr
            cls._model = nemo_asr.models.ASRModel.from_pretrained("nvidia/parakeet-tdt-0.6b-v2")
            cls._initialized = True
            logger.info("NeMo Parakeet STT model loaded")
        elif cls._backend == "faster-whisper":
            from faster_whisper import WhisperModel
            model_size = os.environ.get("WHISPER_MODEL_SIZE", "base.en")
            cls._model = WhisperModel(model_size, device="cpu", compute_type="int8")
            cls._initialized = True
            logger.info(f"faster-whisper model loaded (size: {model_size})")
        else:
            logger.error("No STT backend available")

    @classmethod
    def is_available(cls) -> bool:
        return cls._initialized and cls._model is not None

    @classmethod
    def transcribe(cls, audio_bytes: bytes) -> str:
        """Transcribe audio bytes (16kHz 16-bit PCM) to text."""
        if not cls.is_available():
            raise RuntimeError("STT service not initialized")

        # Write audio to a temp WAV file for model ingestion
        with tempfile.NamedTemporaryFile(suffix=".wav", delete=False) as tmp:
            tmp_path = tmp.name
            import soundfile as sf
            import numpy as np
            # Interpret raw bytes as 16-bit PCM at 16kHz
            audio_array = np.frombuffer(audio_bytes, dtype=np.int16).astype(np.float32) / 32768.0
            sf.write(tmp_path, audio_array, 16000, format="WAV")

        try:
            if cls._backend == "nemo":
                result = cls._model.transcribe([tmp_path])
                # NeMo returns list of transcriptions
                if isinstance(result, list) and len(result) > 0:
                    text = result[0] if isinstance(result[0], str) else str(result[0])
                else:
                    text = str(result)
            elif cls._backend == "faster-whisper":
                segments, _ = cls._model.transcribe(tmp_path, language="en")
                text = " ".join(seg.text for seg in segments).strip()
            else:
                text = ""

            logger.info(f"Transcribed: '{text[:80]}...'")
            return text
        finally:
            os.unlink(tmp_path)
