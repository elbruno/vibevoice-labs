"""
VibeVoice Labs - FastAPI Backend
================================
A REST API for text-to-speech using VibeVoice-Realtime-0.5B.

Endpoints:
- GET  /api/voices  - List available voices
- POST /api/tts     - Generate speech from text
- GET  /api/health  - Health check for Aspire

Run with: uvicorn main:app --host 0.0.0.0 --port 5100
"""

import os
from fastapi import FastAPI
from fastapi.middleware.cors import CORSMiddleware

from app.api.routes import router
from app.services.tts_service import TTSService

# Create FastAPI app
app = FastAPI(
    title="VibeVoice Labs API",
    description="Text-to-speech API powered by VibeVoice-Realtime-0.5B",
    version="1.0.0",
)

# Enable CORS for Blazor frontend
app.add_middleware(
    CORSMiddleware,
    allow_origins=["*"],  # In production, specify exact origins
    allow_credentials=True,
    allow_methods=["*"],
    allow_headers=["*"],
)

# Include API routes
app.include_router(router, prefix="/api")

# Initialize TTS service on startup
@app.on_event("startup")
async def startup_event():
    """Load the TTS model when the server starts."""
    TTSService.initialize()

# Health check at root for convenience
@app.get("/")
async def root():
    """Root endpoint - redirects to health check."""
    return {"message": "VibeVoice Labs API", "docs": "/docs"}


if __name__ == "__main__":
    import uvicorn
    port = int(os.environ.get("PORT", 5100))
    uvicorn.run(app, host="0.0.0.0", port=port)
