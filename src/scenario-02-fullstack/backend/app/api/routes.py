"""
API Routes - Endpoints for VibeVoice TTS service.
"""

import logging
from fastapi import APIRouter, HTTPException, Response

from app.models.schemas import (
    VoicesResponse,
    TTSRequest,
    ErrorResponse,
    HealthResponse,
)
from app.services.tts_service import TTSService

logger = logging.getLogger(__name__)

router = APIRouter()


@router.get("/health", response_model=HealthResponse)
async def health_check():
    """
    Health check endpoint for Aspire orchestration.
    
    Returns the service status and whether the TTS model is loaded.
    """
    model_loaded = TTSService.is_model_loaded()
    status = "healthy" if model_loaded else "unhealthy"
    
    return HealthResponse(status=status, model_loaded=model_loaded)


@router.get("/voices", response_model=VoicesResponse)
async def list_voices():
    """
    List all available TTS voices.
    
    Returns voice metadata including ID, name, language, and style.
    """
    voices = TTSService.get_voices()
    return VoicesResponse(voices=voices)


@router.post(
    "/tts",
    responses={
        200: {"content": {"audio/wav": {}}},
        400: {"model": ErrorResponse, "description": "Validation error"},
        500: {"model": ErrorResponse, "description": "Server error"},
    },
)
async def generate_speech(request: TTSRequest):
    """
    Generate speech from text using the specified voice.
    
    - **text**: Text to synthesize (1-1000 characters)
    - **voice_id**: Voice ID from /api/voices (default: en-US-Aria)
    - **output_format**: Audio format, currently only 'wav' supported
    
    Returns WAV audio bytes.
    """
    # Validate text
    if not request.text or not request.text.strip():
        raise HTTPException(
            status_code=400,
            detail={"error": "Text is required", "code": "VALIDATION_ERROR"}
        )
    
    # Validate output format
    if request.output_format != "wav":
        raise HTTPException(
            status_code=400,
            detail={"error": "Only 'wav' format is supported", "code": "UNSUPPORTED_FORMAT"}
        )
    
    # Check if voice exists (log warning but don't fail)
    voice = TTSService.get_voice_by_id(request.voice_id)
    if voice is None:
        logger.warning(f"Voice '{request.voice_id}' not found, using default")
    
    try:
        # Generate audio
        audio_bytes = TTSService.generate_audio(
            text=request.text.strip(),
            voice_id=request.voice_id
        )
        
        return Response(
            content=audio_bytes,
            media_type="audio/wav",
            headers={
                "Content-Disposition": "attachment; filename=speech.wav"
            }
        )
        
    except RuntimeError as e:
        logger.error(f"TTS generation failed: {e}")
        raise HTTPException(
            status_code=500,
            detail={"error": "TTS model not available", "code": "MODEL_ERROR"}
        )
    except Exception as e:
        logger.error(f"Unexpected error during TTS generation: {e}")
        raise HTTPException(
            status_code=500,
            detail={"error": "Audio generation failed", "code": "GENERATION_ERROR"}
        )
