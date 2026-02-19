# Holden — History

## Project Learnings (from init)
- Project: VibeVoice Labs — showcase for VibeVoice TTS
- User: Bruno Capuano (bcapuano@gmail.com)
- Scenario 1: Minimal Python script with step-by-step TTS demo
- Scenario 2: Full stack — Python FastAPI backend + Blazor .NET 10 frontend + Aspire orchestration
- VibeVoice-Realtime-0.5B is the recommended model for real-time TTS (~300ms latency)
- Aspire Python integration uses `AddUvicornApp` for FastAPI apps

## Learnings

### 2026-02-19: Project structure and API contract finalized
- Designed directory layout with clear separation between Scenario 1 and Scenario 2
- Defined complete API contract: /api/health, /api/voices, /api/tts endpoints
- Established voice registry with 14 language/voice combinations
- Set constraints: WAV format, 1000-char text limit, snake_case JSON properties
- Specified clear boundaries for Naomi (backend) and Alex (frontend) parallel work

📌 Team update (2026-02-19): Alex implemented Blazor frontend with glassmorphism UI, Aspire orchestration, service discovery via `http://backend`, JSON serialization with `[JsonPropertyName]` for snake_case compatibility — decided by Alex
