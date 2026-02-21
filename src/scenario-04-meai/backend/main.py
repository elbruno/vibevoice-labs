"""
VibeVoice Labs - Conversation Backend
======================================
Real-time voice conversation system: STT → Chat → TTS over WebSocket.

Endpoints:
- WS   /ws/conversation  - WebSocket for voice conversation
- GET  /api/voices       - List available TTS voices
- GET  /api/health       - Health check for Aspire
- GET  /api/ready        - Readiness check with progress

Run with: uvicorn main:app --host 0.0.0.0 --port 8000
"""

import os
import logging
import time
from contextlib import asynccontextmanager

from fastapi import FastAPI, WebSocket
from fastapi.middleware.cors import CORSMiddleware

from app.api.routes import router
from app.api.test_routes import router as test_router
from app.api.websocket_handler import handle_conversation
from app.services.tts_service import TTSService
from app.services.stt_service import STTService
from app.services.chat_service import ChatService
from app.services.ready_state import ready_state, State

logging.basicConfig(level=logging.INFO)
logger = logging.getLogger(__name__)


@asynccontextmanager
async def lifespan(app: FastAPI):
    """Application lifespan manager - handles startup and shutdown with auto-warmup."""
    logger.info("=" * 60)
    logger.info("Starting VibeVoice Backend")
    logger.info("=" * 60)
    
    # Set initial state
    ready_state.set_state(State.INITIALIZING, progress=0)
    
    # Phase 1: Load TTS Model
    logger.info("Phase 1: Loading TTS model...")
    ready_state.set_state(State.LOADING_MODELS, progress=10)
    ready_state.mark_service_loading("tts")
    
    tts_start = time.time()
    try:
        TTSService.initialize()
        tts_time = (time.time() - tts_start) * 1000
        ready_state.mark_service_ready("tts", warmup_time_ms=tts_time)
        logger.info(f"✓ TTS model loaded in {tts_time:.2f}ms")
        ready_state.set_state(State.LOADING_MODELS, progress=40)
    except Exception as e:
        logger.error(f"✗ TTS initialization failed: {e}", exc_info=True)
        ready_state.mark_service_error("tts", str(e))
        ready_state.add_error(f"TTS initialization failed: {e}")
    
    # Phase 2: Load STT Model (optional)
    logger.info("Phase 2: Loading STT model (optional)...")
    ready_state.mark_service_loading("stt")
    
    stt_start = time.time()
    try:
        STTService.initialize()
        stt_time = (time.time() - stt_start) * 1000
        if STTService.is_available():
            ready_state.mark_service_ready("stt", warmup_time_ms=stt_time)
            logger.info(f"✓ STT model loaded in {stt_time:.2f}ms")
        else:
            ready_state.mark_service_error("stt", "not_installed")
            logger.info("ℹ STT not available (optional)")
        ready_state.set_state(State.LOADING_MODELS, progress=60)
    except Exception as e:
        logger.warning(f"STT initialization failed (optional): {e}")
        ready_state.mark_service_error("stt", str(e))
    
    # Phase 3: Check Chat Service (Ollama)
    logger.info("Phase 3: Checking Chat service (Ollama)...")
    ready_state.mark_service_loading("chat")
    ready_state.set_state(State.LOADING_MODELS, progress=70)
    
    chat_start = time.time()
    try:
        if ChatService.is_available():
            chat_time = (time.time() - chat_start) * 1000
            ready_state.mark_service_ready(
                "chat", 
                warmup_time_ms=chat_time,
                model=os.environ.get("OLLAMA_MODEL", "llama3.2")
            )
            logger.info(f"✓ Chat service ready in {chat_time:.2f}ms")
        else:
            ready_state.mark_service_error("chat", "Ollama not available")
            ready_state.add_error("Chat service unavailable: Ollama not responding or model not installed")
            logger.error("✗ Chat service not available - Ollama not responding")
        ready_state.set_state(State.LOADING_MODELS, progress=80)
    except Exception as e:
        logger.error(f"✗ Chat service check failed: {e}", exc_info=True)
        ready_state.mark_service_error("chat", str(e))
        ready_state.add_error(f"Chat service check failed: {e}")
    
    # Phase 4: Warmup services
    logger.info("Phase 4: Warming up services...")
    ready_state.set_state(State.WARMING_UP, progress=85)
    
    # Warmup TTS with a short test
    if TTSService.is_model_loaded():
        try:
            logger.info("Warming up TTS...")
            warmup_start = time.time()
            audio_bytes = TTSService.generate_audio("Hello", voice_id="en-carter")
            warmup_time = (time.time() - warmup_start) * 1000
            logger.info(f"✓ TTS warmup completed in {warmup_time:.2f}ms ({len(audio_bytes)} bytes)")
            ready_state.set_state(State.WARMING_UP, progress=90)
        except Exception as e:
            logger.warning(f"TTS warmup failed: {e}")
            # TTS warmup failure is non-critical
    
    # Warmup Chat with a test message
    if ChatService.is_available():
        try:
            logger.info("Warming up Chat...")
            warmup_start = time.time()
            test_chat = ChatService()
            _ = test_chat.chat("Hello")
            warmup_time = (time.time() - warmup_start) * 1000
            logger.info(f"✓ Chat warmup completed in {warmup_time:.2f}ms")
            ready_state.set_state(State.WARMING_UP, progress=95)
        except Exception as e:
            logger.warning(f"Chat warmup failed: {e}")
    
    # Final state determination
    if ready_state.is_ready():
        ready_state.set_state(State.READY, progress=100)
        logger.info("=" * 60)
        logger.info("✓ Backend READY - All critical services loaded and warmed up")
        logger.info("=" * 60)
    else:
        ready_state.set_state(State.ERROR, progress=100)
        logger.error("=" * 60)
        logger.error("✗ Backend initialization completed with ERRORS")
        logger.error("  Check logs and /api/ready for details")
        logger.error("=" * 60)
    
    yield  # Application runs
    
    # Shutdown: cleanup if needed
    logger.info("Shutting down application...")
    logger.info("=" * 60)


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
        "ready": ready_state.is_ready(),
        "endpoints": {
            "docs": "/docs",
            "health": "/api/health",
            "ready": "/api/ready",
            "warmup": "/api/warmup",
            "voices": "/api/voices",
            "conversation": "/api/conversation",
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
