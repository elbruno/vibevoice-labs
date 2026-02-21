"""
REST API Routes - Health check, voice listing, and HTTP conversation endpoint.
"""

import base64
import logging
import time
import uuid
from typing import Optional

from fastapi import APIRouter, File, Form, UploadFile

from app.models.schemas import VoicesResponse, HealthResponse
from app.services.tts_service import TTSService
from app.services.stt_service import STTService
from app.services.chat_service import ChatService
from app.services.ready_state import ready_state

logger = logging.getLogger(__name__)

router = APIRouter()

# ---------------------------------------------------------------------------
# Per-session conversation history store
# Each session_id maps to a ChatService instance that retains history.
# ---------------------------------------------------------------------------
_sessions: dict[str, ChatService] = {}


@router.get("/health", response_model=HealthResponse)
async def health_check():
    """Health check endpoint for Aspire orchestration."""
    try:
        model_loaded = TTSService.is_model_loaded()
        stt_available = STTService.is_available()
        chat_available = ChatService.is_available()
        
        # Service is healthy if TTS model is loaded (minimum requirement)
        status = "healthy" if model_loaded else "unhealthy"
        
        logger.info(f"Health check: status={status}, tts={model_loaded}, stt={stt_available}, chat={chat_available}")
        
        return HealthResponse(
            status=status,
            model_loaded=model_loaded,
            stt_available=stt_available,
            chat_available=chat_available,
        )
    except Exception as e:
        logger.error(f"Health check failed: {e}", exc_info=True)
        return HealthResponse(
            status="unhealthy",
            model_loaded=False,
            stt_available=False,
            chat_available=False,
        )


@router.get("/ready")
async def check_ready():
    """
    Readiness check endpoint.
    Returns whether backend is ready to accept requests.
    
    Unlike /health which checks if services CAN work,
    /ready checks if services ARE LOADED and ready NOW.
    
    Use this endpoint to:
    - Wait for startup to complete before connecting
    - Show loading progress to users
    - Determine when to send first request
    """
    state_dict = ready_state.to_dict()
    logger.debug(f"Ready check: {state_dict['ready']}, state: {state_dict['state']}")
    return state_dict


@router.post("/warmup")
async def warmup():
    """
    Warmup endpoint - Pre-loads models and tests services.
    Call this after startup to ensure everything is ready.
    Returns timing information for diagnostics.
    """
    logger.info("Warmup request received")
    results = {
        "status": "ok",
        "services": {},
        "total_time_ms": 0
    }
    
    start_time = time.time()
    
    # Test TTS
    tts_start = time.time()
    try:
        if TTSService.is_model_loaded():
            # Generate a short test audio to warm up the model
            test_text = "Hello, testing voice synthesis."
            _ = TTSService.generate_audio(test_text, voice_id="en-carter")
            tts_time = (time.time() - tts_start) * 1000
            results["services"]["tts"] = {
                "status": "ready",
                "time_ms": round(tts_time, 2)
            }
            logger.info(f"TTS warmup completed in {tts_time:.2f}ms")
        else:
            results["services"]["tts"] = {"status": "not_loaded"}
            logger.warning("TTS warmup skipped - model not loaded")
    except Exception as e:
        results["services"]["tts"] = {"status": "error", "error": str(e)}
        logger.error(f"TTS warmup failed: {e}")
    
    # Test STT  
    stt_start = time.time()
    try:
        if STTService.is_available():
            stt_time = (time.time() - stt_start) * 1000
            results["services"]["stt"] = {
                "status": "ready",
                "time_ms": round(stt_time, 2)
            }
            logger.info(f"STT warmup completed in {stt_time:.2f}ms")
        else:
            results["services"]["stt"] = {"status": "not_available"}
            logger.info("STT warmup skipped - not available")
    except Exception as e:
        results["services"]["stt"] = {"status": "error", "error": str(e)}
        logger.error(f"STT warmup failed: {e}")
    
    # Test Chat (Ollama)
    chat_start = time.time()
    try:
        if ChatService.is_available():
            # Test with a simple message
            test_chat = ChatService()
            test_response = test_chat.chat("Hello")
            chat_time = (time.time() - chat_start) * 1000
            results["services"]["chat"] = {
                "status": "ready",
                "time_ms": round(chat_time, 2),
                "test_response_length": len(test_response)
            }
            logger.info(f"Chat warmup completed in {chat_time:.2f}ms")
        else:
            results["services"]["chat"] = {"status": "not_available"}
            logger.warning("Chat warmup skipped - Ollama not available")
    except Exception as e:
        results["services"]["chat"] = {"status": "error", "error": str(e)}
        logger.error(f"Chat warmup failed: {e}")
    
    total_time = (time.time() - start_time) * 1000
    results["total_time_ms"] = round(total_time, 2)
    
    logger.info(f"Warmup completed in {total_time:.2f}ms")
    return results


@router.get("/voices", response_model=VoicesResponse)
async def list_voices():
    """List all available TTS voices."""
    voices = TTSService.get_voices()
    return VoicesResponse(voices=voices)


@router.post("/conversation")
async def conversation(
    audio: UploadFile = File(...),
    voice_id: str = Form("en-emma"),
    session_id: Optional[str] = Form(None),
):
    """
    HTTP conversation endpoint — replaces the WebSocket approach.

    Accepts a browser audio recording (audio/webm or any ffmpeg-supported
    format), runs it through the STT → Chat → TTS pipeline, and returns a
    JSON payload so the client can display text and play back audio without
    needing a persistent WebSocket connection.

    Request (multipart/form-data):
      - audio      : audio file from the browser's MediaRecorder
      - voice_id   : TTS voice to use (default: en-emma)
      - session_id : opaque string that ties multiple turns together so the
                     chat service can maintain conversation history

    Response JSON:
      - session_id    : echo back (or newly minted) session identifier
      - transcript    : STT result of the user's speech
      - response_text : AI chat response
      - audio_base64  : WAV audio for the AI response, base64-encoded
    """
    # Resolve or create the session
    if not session_id:
        session_id = str(uuid.uuid4())
    if session_id not in _sessions:
        _sessions[session_id] = ChatService()
        logger.info(f"New session created: {session_id}")
    chat_service = _sessions[session_id]

    # Step 1: STT
    audio_bytes = await audio.read()
    content_type = audio.content_type or "audio/webm"
    try:
        transcript = STTService.transcribe_upload(audio_bytes, content_type)
        if not transcript.strip():
            return {"error": "Could not transcribe audio", "session_id": session_id}
    except Exception as e:
        logger.error(f"STT failed: {e}")
        return {"error": f"Transcription failed: {e}", "session_id": session_id}

    # Step 2: Chat
    try:
        response_text = chat_service.chat(transcript)
    except Exception as e:
        logger.error(f"Chat failed: {e}")
        return {"error": f"Chat failed: {e}", "session_id": session_id}

    # Step 3: TTS
    try:
        wav_bytes = TTSService.generate_audio(response_text, voice_id=voice_id)
        audio_b64 = base64.b64encode(wav_bytes).decode("utf-8")
    except Exception as e:
        logger.error(f"TTS failed: {e}")
        return {"error": f"Speech generation failed: {e}", "session_id": session_id}

    return {
        "session_id": session_id,
        "transcript": transcript,
        "response_text": response_text,
        "audio_base64": audio_b64,
    }


@router.delete("/conversation/{session_id}")
async def reset_conversation(session_id: str):
    """Reset (delete) conversation history for the given session."""
    if session_id in _sessions:
        del _sessions[session_id]
        logger.info(f"Session deleted: {session_id}")
    return {"status": "ok", "session_id": session_id}
