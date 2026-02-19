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

### 2026-02-19: Comprehensive project documentation created
- Created complete README.md overhaul with badges, feature list, project structure, and quick start guides
- Created docs/ARCHITECTURE.md with ASCII system diagrams, component descriptions, and data flow documentation
- Created docs/GETTING_STARTED.md with step-by-step setup for both scenarios and troubleshooting guide
- Created docs/API_REFERENCE.md with full endpoint documentation, voice reference table, and code examples
- Completed docs/USER_MANUAL.md replacing all TODO placeholders with actual usage instructions

**Documentation structure:**
- Voice registry contains 14 voices: 5 English (US, GB, AU), 7 European (DE, FR, IT, ES, PT, NL, PL), 2 Asian (JP, KR)
- API contract: /api/health, /api/voices, /api/tts with WAV output at 24kHz
- Aspire configuration uses `AddUvicornApp` for Python backend integration
- Frontend uses base64 data URLs for audio playback (no server-side file storage)
