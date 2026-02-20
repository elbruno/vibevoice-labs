"""
REST API Routes - Health check and voice listing.
"""

import logging
import time
from fastapi import APIRouter

from app.models.schemas import VoicesResponse, HealthResponse
from app.services.tts_service import TTSService
from app.services.stt_service import STTService
from app.services.chat_service import ChatService

logger = logging.getLogger(__name__)

router = APIRouter()


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
