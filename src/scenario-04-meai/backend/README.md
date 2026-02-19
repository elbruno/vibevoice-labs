# Scenario 4 — Conversation Backend

Real-time voice conversation backend: **Speech-to-Text → AI Chat → Text-to-Speech** over WebSocket.

## Architecture

```
Client (mic audio) → WebSocket → STT (Parakeet/Whisper) → OpenAI Chat → TTS (VibeVoice) → WebSocket → Client (speaker)
```

## Setup

```bash
cd src/scenario-04-meai/backend
python -m venv .venv
.venv\Scripts\Activate.ps1   # Windows
pip install -r requirements.txt
```

Set your OpenAI API key:
```bash
set OPENAI_API_KEY=sk-...
```

## Run

```bash
uvicorn main:app --host 0.0.0.0 --port 8000
```

Or with Aspire (port set via `PORT` env var).

## Endpoints

| Endpoint | Method | Description |
|---|---|---|
| `/api/health` | GET | Service health + model status |
| `/api/voices` | GET | Available TTS voices |
| `/ws/conversation` | WebSocket | Voice conversation |

## WebSocket Protocol

### Client → Server

| Frame Type | Content | Description |
|---|---|---|
| Binary | Raw PCM audio | 16kHz, 16-bit, mono |
| Text (JSON) | `{"type": "end_of_speech"}` | Signals end of utterance |
| Text (JSON) | `{"type": "reset"}` | Clears conversation history |

### Server → Client

| Frame Type | Content | Description |
|---|---|---|
| Text (JSON) | `{"type": "transcript", "text": "..."}` | STT transcription |
| Text (JSON) | `{"type": "response", "text": "..."}` | AI chat response |
| Binary | WAV audio data | 24kHz TTS output |
| Text (JSON) | `{"type": "audio_complete"}` | All audio chunks sent |
| Text (JSON) | `{"type": "error", "error": "..."}` | Error occurred |

### Conversation Flow

1. Client streams audio as binary frames
2. Client sends `end_of_speech` when done talking
3. Server transcribes → sends `transcript`
4. Server generates AI response → sends `response`
5. Server generates TTS audio → sends binary WAV
6. Server sends `audio_complete`
7. Client can start next turn

## STT Backends

The service auto-selects whichever is installed:

1. **NVIDIA Parakeet** (`nemo_toolkit[asr]`) — best accuracy
2. **faster-whisper** — lighter weight fallback

Set whisper model size via `WHISPER_MODEL_SIZE` env var (default: `base.en`).

## Voices

Same voice registry as Scenario 2: `en-carter`, `en-davis`, `en-emma`, `en-frank`, `en-grace`, `en-mike`.
