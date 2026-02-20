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
from contextlib import asynccontextmanager

from fastapi import FastAPI, WebSocket
from fastapi.middleware.cors import CORSMiddleware

from app.api.routes import router
from app.api.test_routes import router as test_router
from app.api.websocket_handler import handle_conversation
from app.services.tts_service import TTSService
from app.services.stt_service import STTService

logging.basicConfig(level=logging.INFO)
logger = logging.getLogger(__name__)


@asynccontextmanager
async def lifespan(app: FastAPI):
    """Application lifespan manager - handles startup and shutdown."""
    logger.info("Starting application...")
    
    # Startup: Initialize services
    logger.info("Initializing TTS service...")
    try:
        TTSService.initialize()
        logger.info("TTS service initialized successfully")
    except Exception as e:
        logger.error(f"TTS initialization failed: {e}", exc_info=True)
        # Don't raise - let the app start but health check will report unhealthy

    logger.info("Initializing STT service...")
    try:
        STTService.initialize()
        logger.info("STT service initialized successfully")
    except Exception as e:
        logger.warning(f"STT initialization failed (will not be available): {e}", exc_info=True)
    
    logger.info("Application startup complete")
    
    yield  # Application runs
    
    # Shutdown: cleanup if needed
    logger.info("Shutting down application...")


app = FastAPI(
    title="VibeVoice Conversation API",
    description="Real-time voice conversation: STT → Chat → TTS",
    version="1.0.0",
    lifespan=lifespan,
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
async def websocket_conversation(websocket: WebSocket):
    """Main WebSocket endpoint for voice conversation."""
    await handle_conversation(websocket)


@app.websocket("/ws/test")
async def websocket_test(websocket: WebSocket):
    """Simple WebSocket test endpoint - echoes back messages without loading services."""
    await websocket.accept()
    logger.info(f"Test WebSocket connected from {websocket.client.host if websocket.client else 'unknown'}")
    
    try:
        await websocket.send_text('{"type": "connected", "message": "WebSocket test endpoint ready"}')
        
        while True:
            message = await websocket.receive_text()
            logger.info(f"Test WebSocket received: {message[:100]}")
            # Echo back
            await websocket.send_text(f'{{"type": "echo", "data": {message}}}')
    except Exception as e:
        logger.info(f"Test WebSocket disconnected: {e}")


@app.get("/")
async def root():
    return {
        "message": "VibeVoice Conversation API is running",
        "status": "online",
        "endpoints": {
            "docs": "/docs",
            "health": "/api/health",
            "warmup": "/api/warmup",
            "voices": "/api/voices",
            "test_ping": "/api/test/ping",
            "test_echo": "/api/test/echo",
            "test_headers": "/api/test/headers",
            "websocket": "/ws/conversation",
            "websocket_test": "/ws/test"
        }
    }


if __name__ == "__main__":
    import uvicorn
    port = int(os.environ.get("PORT", 8000))
    uvicorn.run(app, host="0.0.0.0", port=port)
