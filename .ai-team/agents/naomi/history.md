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
