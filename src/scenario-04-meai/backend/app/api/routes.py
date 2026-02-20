"""
REST API Routes - Health check and voice listing.
"""

import logging
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


@router.get("/voices", response_model=VoicesResponse)
async def list_voices():
    """List all available TTS voices."""
    voices = TTSService.get_voices()
    return VoicesResponse(voices=voices)
