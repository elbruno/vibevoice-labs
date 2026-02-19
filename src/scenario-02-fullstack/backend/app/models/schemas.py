"""
Pydantic models for API request/response schemas.
"""

from typing import List, Optional
from pydantic import BaseModel, Field


class Voice(BaseModel):
    """Represents an available TTS voice."""
    id: str = Field(..., description="Unique voice identifier")
    name: str = Field(..., description="Human-readable voice name")
    language: str = Field(..., description="Language code (e.g., en-US)")
    style: str = Field(..., description="Voice style (e.g., general, conversational)")


class VoicesResponse(BaseModel):
    """Response model for /api/voices endpoint."""
    voices: List[Voice]


class TTSRequest(BaseModel):
    """Request model for /api/tts endpoint."""
    text: str = Field(
        ..., 
        min_length=1, 
        max_length=1000,
        description="Text to synthesize (1-1000 characters)"
    )
    voice_id: str = Field(
        default="en-carter",
        description="Voice ID from /api/voices"
    )
    output_format: str = Field(
        default="wav",
        description="Output audio format (currently only 'wav' supported)"
    )


class ErrorResponse(BaseModel):
    """Standard error response model."""
    error: str = Field(..., description="Error message")
    code: str = Field(..., description="Error code for programmatic handling")


class HealthResponse(BaseModel):
    """Response model for /api/health endpoint."""
    status: str = Field(..., description="Service status (healthy/unhealthy)")
    model_loaded: bool = Field(..., description="Whether the TTS model is loaded")
