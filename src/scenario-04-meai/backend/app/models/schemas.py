"""
Pydantic models for API request/response schemas.
"""

from typing import List, Optional
from pydantic import BaseModel, Field


class Voice(BaseModel):
    """Represents an available TTS voice."""
    id: str = Field(..., description="Unique voice identifier")
    name: str = Field(..., description="Human-readable voice name")
    language: str = Field(..., description="Language code (e.g., en)")
    style: str = Field(..., description="Voice style (e.g., male, female)")


class VoicesResponse(BaseModel):
    """Response model for /api/voices endpoint."""
    voices: List[Voice]


class HealthResponse(BaseModel):
    """Response model for /api/health endpoint."""
    status: str = Field(..., description="Service status (healthy/unhealthy)")
    model_loaded: bool = Field(..., description="Whether the TTS model is loaded")
    stt_available: bool = Field(default=False, description="Whether STT is available")
    chat_available: bool = Field(default=False, description="Whether chat service is available")


class ErrorResponse(BaseModel):
    """Standard error response model."""
    error: str = Field(..., description="Error message")
    code: str = Field(..., description="Error code for programmatic handling")


class WebSocketMessage(BaseModel):
    """Base WebSocket message from server to client."""
    type: str = Field(..., description="Message type")


class TranscriptMessage(WebSocketMessage):
    """STT transcription result."""
    type: str = "transcript"
    text: str = Field(..., description="Transcribed user speech")


class ResponseMessage(WebSocketMessage):
    """AI chat response."""
    type: str = "response"
    text: str = Field(..., description="AI-generated response text")


class AudioCompleteMessage(WebSocketMessage):
    """Signals that all audio chunks have been sent."""
    type: str = "audio_complete"


class ErrorMessage(WebSocketMessage):
    """Error during conversation turn."""
    type: str = "error"
    error: str = Field(..., description="Error description")
