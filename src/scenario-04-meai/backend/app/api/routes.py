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
    model_loaded = TTSService.is_model_loaded()
    return HealthResponse(
        status="healthy" if model_loaded else "unhealthy",
        model_loaded=model_loaded,
        stt_available=STTService.is_available(),
        chat_available=ChatService.is_available(),
    )


@router.get("/voices", response_model=VoicesResponse)
async def list_voices():
    """List all available TTS voices."""
    voices = TTSService.get_voices()
    return VoicesResponse(voices=voices)
