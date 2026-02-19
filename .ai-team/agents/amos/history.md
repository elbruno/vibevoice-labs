# Amos — History

## Project Learnings (from init)
- Project: VibeVoice Labs — showcase for VibeVoice TTS
- User: Bruno Capuano (bcapuano@gmail.com)
- Test the app thoroughly and generate user manuals
- Scenario 1: Simple Python script — verify TTS output
- Scenario 2: Full stack — test API endpoints, UI flows, audio generation

## Learnings

### 2026-02-19: Test scaffolding created (proactive)
- Created Python pytest tests in `src/scenario-02-fullstack/python-api/tests/`
  - `conftest.py` — fixtures for async FastAPI test client
  - `test_api.py` — 12 tests covering health, voices, and TTS endpoints
  - `requirements-test.txt` — pytest, httpx, pytest-asyncio, anyio
- Created C# xUnit tests in `src/scenario-02-fullstack/VoiceLabs.Web.Tests/`
  - `TtsServiceTests.cs` — 8 tests mocking HTTP client for TtsService
  - `VoiceLabs.Web.Tests.csproj` — .NET 10, xUnit, Moq
- Created `docs/USER_MANUAL.md` with section structure for both scenarios
- Tests are based on API contract from Holden's design review
- **Note:** Tests will need adjustment once Naomi (backend) and Alex (frontend) complete implementations
- Python test imports are flexible to handle different module structures
- C# tests use placeholder models — will need ProjectReference once VoiceLabs.Web exists

📌 Team update (2026-02-19): Holden designed complete API contract with 14 voice options, WAV format, 1000-char limit, clear directory structure for parallel development — decided by Holden

📌 Team update (2026-02-19): Alex implemented Blazor frontend with glassmorphism UI, Aspire orchestration, service discovery via `http://backend`, JSON serialization with `[JsonPropertyName]` for snake_case compatibility — decided by Alex

### 2026-02-19: Full test run — fixes applied
- **Fixed:** Added `VoiceLabs.Web.Tests` project to `VoiceLabs.slnx` (was missing)
- **Fixed:** Enabled `ProjectReference` to `VoiceLabs.Web` in test `.csproj` (was commented out)
- **C# tests:** 8/8 passed (xUnit, mocked HTTP handlers for voices, TTS, health endpoints)
- **Python tests:** 12/12 collected, all skipped — backend `main.py` not yet implemented (Naomi's work pending)
- **Solution build:** All 4 projects build clean (0 errors, 0 warnings)
- **Note:** C# tests still use internal placeholder models instead of actual `VoiceLabs.Web.Services` types — should be refactored to use real models now that ProjectReference is active
- **Note:** Python tests will activate automatically once backend FastAPI app exists at `python-api/main.py` or `python-api/app/main.py`
