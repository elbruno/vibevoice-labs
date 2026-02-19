# Naomi — History

## Project Learnings (from init)
- Project: VibeVoice Labs — showcase for VibeVoice TTS
- User: Bruno Capuano (bcapuano@gmail.com)
- Scenario 1: Minimal Python script with step-by-step TTS demo
- Scenario 2: Full stack — Python FastAPI backend + Blazor .NET 10 frontend + Aspire orchestration
- VibeVoice-Realtime-0.5B is the recommended model for real-time TTS
- Aspire uses `AddUvicornApp` for FastAPI apps with `WithHttpEndpoint(port, env: "PORT")`
- Virtual environments auto-detected from requirements.txt or pyproject.toml
- Can use `WithUv()` for faster package management

## Learnings

### 2026-02-19: Backend Implementation Complete
- Created Scenario 1: Simple Python script (`src/scenario-01-simple/main.py`)
  - Step-by-step commented code showing VibeVoice TTS usage
  - Demonstrates model loading, text synthesis, and WAV export
  - Includes commented examples for all 14 voice/language options
- Created Scenario 2: FastAPI backend (`src/scenario-02-fullstack/backend/`)
  - Modular structure: routes.py, tts_service.py, schemas.py
  - Three endpoints: GET /api/health, GET /api/voices, POST /api/tts
  - CORS enabled for Blazor frontend integration
  - Singleton TTS service with lazy model loading
- VibeVoice outputs audio at 24kHz sample rate
- Voice ID mapping: API uses friendly IDs (en-US-Aria), internally maps to VibeVoice speaker codes (EN-Default)
- FastAPI reads PORT from environment variable for Aspire integration

📌 Team update (2026-02-19): Holden designed complete API contract with 14 voice options, WAV format, 1000-char limit, clear directory structure for parallel development — decided by Holden

📌 Team update (2026-02-19): Alex implemented Blazor frontend with glassmorphism UI, Aspire orchestration, service discovery via `http://backend`, JSON serialization with `[JsonPropertyName]` for snake_case compatibility — decided by Alex

### 2026-02-19: Scenario 5 — Batch TTS Processing CLI
- Created `src/scenario-05-batch-processing/batch_tts.py` — Click-based CLI with tqdm progress
- YAML front-matter parsing via regex + pyyaml for per-file voice overrides
- ThreadPoolExecutor for optional parallel processing (--parallel flag)
- Error handling per file — single failures don't stop the batch
- Summary report shows succeeded/failed counts, audio duration, total time
- 5 sample text files (English, French w/ front-matter, Spanish w/ front-matter, story, technical)
- Same step-by-step teaching style as scenario-01 with section headers and comments

### 2026-02-19: Scenario 6 — Real-Time Streaming TTS Demo
- Created `src/scenario-06-streaming-realtime/stream_tts.py` — streaming demo using `generate_stream()`
  - Plays audio chunks via sounddevice in real-time as they arrive
  - Measures time-to-first-chunk and total generation time (real-time factor)
  - Terminal progress bar showing chunks arriving
  - Graceful fallback: if sounddevice unavailable, saves to WAV file only
  - Commented alternatives for all voice/language options
- `generate_stream()` yields numpy arrays per chunk; `sd.play(chunk, blocking=True)` queues them naturally
- `sounddevice` may fail on headless servers — always provide a file-save fallback
- Performance summary (first-chunk latency, RTF) is valuable for demonstrating VibeVoice's streaming advantage

### 2026-02-19: Python Scenarios Verification and Fixes
- **Scenario 1:** ✅ All files compile, imports work, README updated with correct voice list (Carter, Davis, Emma, Frank, Grace, Mike)
- **Scenario 2:** ✅ Backend complete with modular structure; fixed default voice_id from "en-US-Aria" to "en-carter" to match actual voice registry; updated README examples
- **Scenario 5:** ✅ Batch processing tool complete with YAML front-matter support; fixed README voice codes to match VOICE_PRESETS keys; updated sample files (hello-french.txt, hello-spanish.txt) to use correct voice codes (fr-woman, es-woman)
- **Scenario 6:** ✅ Streaming demo complete with chunked playback; all syntax valid
- All Python files compile successfully via `python -m py_compile`

### 2026-02-19: Aspire Frontend-to-Backend HTTP Fix
- **Problem:** Blazor frontend couldn't reach Python FastAPI backend via Aspire service discovery
- **Root cause 1:** `VoiceLabs.Web/Program.cs` used `https://backend` but uvicorn only serves HTTP — Aspire resolves scheme to named endpoint, so HTTPS lookup fails
- **Root cause 2:** `AppHost.cs` didn't declare `.WithHttpEndpoint()` on the uvicorn app, so Aspire had no endpoint metadata for service discovery
- **Fix:** Changed `https://backend` → `http://backend` in Program.cs; added `.WithHttpEndpoint(targetPort: 8000, env: "PORT")` in AppHost.cs
- **Lesson:** When using `AddUvicornApp`, always declare the endpoint explicitly with `WithHttpEndpoint` and ensure the frontend uses `http://` (not `https://`) since Python uvicorn typically serves plain HTTP behind Aspire's reverse proxy
- All import resolution verified for scenario-02 backend (`from app.api.routes import router` etc.)
- **Critical fix:** Voice ID consistency across all scenarios — backend uses kebab-case (en-carter, en-emma), batch tool uses lowercase (carter, emma, fr-woman, es-woman)
- requirements.txt files verified complete for all scenarios

### 2026-02-19: Scenario 4 — Conversation Backend Created
- Created `src/scenario-04-meai/backend/` — real-time voice conversation system
- **Architecture:** WebSocket-based STT → Chat → TTS pipeline
- **Services:**
  - `tts_service.py` — copied from scenario-02, same VibeVoice model loading pattern
  - `stt_service.py` — dual-backend: NVIDIA Parakeet (primary) or faster-whisper (fallback), auto-selected via try/except
  - `chat_service.py` — OpenAI gpt-4o-mini with conversation history, system prompt tuned for short spoken responses
- **WebSocket protocol** (`/ws/conversation`):
  - Client sends binary PCM audio frames (16kHz, 16-bit, mono) + `end_of_speech` JSON signal
  - Server responds with `transcript` → `response` → binary WAV → `audio_complete`
  - Supports `reset` to clear conversation history
  - 30-second max audio buffer per turn
- **REST endpoints:** `/api/health` (includes stt/chat availability), `/api/voices`
- Port configurable via PORT env var (default 8000) for Aspire integration
- All Python files compile successfully via `py_compile`
- WebSocket protocol contract written to `.ai-team/decisions/inbox/naomi-websocket-protocol.md`
