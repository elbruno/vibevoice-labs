"""
VibeVoice Labs - Conversation Backend
======================================
Real-time voice conversation system: STT → Chat → TTS over WebSocket.

Endpoints:
- WS   /ws/conversation  - WebSocket for voice conversation
- GET  /api/voices       - List available TTS voices
- GET  /api/health       - Health check for Aspire

Run with: uvicorn main:app --host 0.0.0.0 --port 8000
"""

import os
import logging

from fastapi import FastAPI
from fastapi.middleware.cors import CORSMiddleware

from app.api.routes import router
from app.api.test_routes import router as test_router
from app.api.websocket_handler import handle_conversation
from app.services.tts_service import TTSService
from app.services.stt_service import STTService

logging.basicConfig(level=logging.INFO)
logger = logging.getLogger(__name__)

app = FastAPI(
    title="VibeVoice Conversation API",
    description="Real-time voice conversation: STT → Chat → TTS",
    version="1.0.0",
)

# CORS middleware for frontend integration
app.add_middleware(
    CORSMiddleware,
    allow_origins=["*"],
    allow_credentials=True,
    allow_methods=["*"],
    allow_headers=["*"],
)

# REST API routes
app.include_router(router, prefix="/api")
app.include_router(test_router, prefix="/api/test")


# WebSocket endpoint
@app.websocket("/ws/conversation")
async def websocket_conversation(websocket):
    await handle_conversation(websocket)


@app.on_event("startup")
async def startup_event():
    """Initialize services on startup."""
    logger.info("Initializing TTS service...")
    TTSService.initialize()

    logger.info("Initializing STT service...")
    try:
        STTService.initialize()
    except Exception as e:
        logger.warning(f"STT initialization failed (will not be available): {e}")


@app.get("/")
async def root():
    return {
        "message": "VibeVoice Conversation API is running",
        "status": "online",
        "endpoints": {
            "docs": "/docs",
            "health": "/api/health",
            "voices": "/api/voices",
            "test_ping": "/api/test/ping",
            "test_echo": "/api/test/echo",
            "test_headers": "/api/test/headers",
            "websocket": "/ws/conversation"
        }
    }


if __name__ == "__main__":
    import uvicorn
    port = int(os.environ.get("PORT", 8000))
    uvicorn.run(app, host="0.0.0.0", port=port)
