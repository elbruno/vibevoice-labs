"""
WebSocket Conversation Handler

Protocol:
  Client → Server:
    - Binary frame: raw 16kHz 16-bit PCM audio data (accumulates)
    - Text frame JSON: {"type": "end_of_speech"} — signals end of user utterance
    - Text frame JSON: {"type": "reset"} — resets conversation history

  Server → Client:
    - Text frame JSON: {"type": "transcript", "text": "..."} — STT result
    - Text frame JSON: {"type": "response", "text": "..."} — AI response text
    - Binary frame: WAV audio chunk (24kHz) from TTS
    - Text frame JSON: {"type": "audio_complete"} — all audio sent
    - Text frame JSON: {"type": "error", "error": "..."} — error occurred
"""

import json
import logging

from fastapi import WebSocket, WebSocketDisconnect

from app.services.stt_service import STTService
from app.services.chat_service import ChatService
from app.services.tts_service import TTSService

logger = logging.getLogger(__name__)

# Max audio buffer: 30 seconds at 16kHz 16-bit mono = ~960KB
MAX_AUDIO_BUFFER = 16000 * 2 * 30


async def handle_conversation(websocket: WebSocket):
    """Handle a single WebSocket conversation session."""
    await websocket.accept()
    logger.info("WebSocket conversation connected")

    audio_buffer = bytearray()

    try:
        chat_service = ChatService()
        while True:
            message = await websocket.receive()

            if "bytes" in message and message["bytes"]:
                # Binary frame: accumulate audio data
                chunk = message["bytes"]
                if len(audio_buffer) + len(chunk) <= MAX_AUDIO_BUFFER:
                    audio_buffer.extend(chunk)
                else:
                    await _send_error(websocket, "Audio buffer full (max 30s)")
                    audio_buffer.clear()

            elif "text" in message and message["text"]:
                # Text frame: control signal
                try:
                    data = json.loads(message["text"])
                except json.JSONDecodeError:
                    await _send_error(websocket, "Invalid JSON")
                    continue

                msg_type = data.get("type", "")

                if msg_type == "end_of_speech":
                    # Process the accumulated audio through the conversation pipeline
                    await _process_turn(websocket, chat_service, bytes(audio_buffer))
                    audio_buffer.clear()

                elif msg_type == "reset":
                    chat_service.reset()
                    audio_buffer.clear()
                    await websocket.send_text(json.dumps({"type": "reset_ack"}))

                else:
                    await _send_error(websocket, f"Unknown message type: {msg_type}")

    except WebSocketDisconnect:
        logger.info("WebSocket conversation disconnected")
    except Exception as e:
        logger.error(f"WebSocket error: {e}")
        try:
            await _send_error(websocket, str(e))
        except Exception:
            pass


async def _process_turn(
    websocket: WebSocket,
    chat_service: ChatService,
    audio_bytes: bytes,
):
    """Run one conversation turn: STT → Chat → TTS."""
    if not audio_bytes:
        await _send_error(websocket, "No audio data received")
        return

    # Step 1: Speech-to-text
    try:
        user_text = STTService.transcribe(audio_bytes)
        if not user_text.strip():
            await _send_error(websocket, "Could not transcribe audio")
            return
        await websocket.send_text(json.dumps({"type": "transcript", "text": user_text}))
    except Exception as e:
        logger.error(f"STT failed: {e}")
        await _send_error(websocket, f"Transcription failed: {e}")
        return

    # Step 2: Chat — get AI response
    try:
        ai_text = chat_service.chat(user_text)
        await websocket.send_text(json.dumps({"type": "response", "text": ai_text}))
    except Exception as e:
        logger.error(f"Chat failed: {e}")
        await _send_error(websocket, f"Chat failed: {e}")
        return

    # Step 3: TTS — generate and send audio
    try:
        wav_bytes = TTSService.generate_audio(ai_text)
        # Send as a single binary frame (WAV includes header)
        await websocket.send_bytes(wav_bytes)
        await websocket.send_text(json.dumps({"type": "audio_complete"}))
    except Exception as e:
        logger.error(f"TTS failed: {e}")
        await _send_error(websocket, f"Speech generation failed: {e}")


async def _send_error(websocket: WebSocket, error: str):
    """Send an error message to the client."""
    await websocket.send_text(json.dumps({"type": "error", "error": error}))
