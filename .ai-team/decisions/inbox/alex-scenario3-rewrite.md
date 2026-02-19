# Decision: Scenario 3 Rewritten to Direct Model Invocation

**Date:** 2026-02-19  
**By:** Alex (Frontend Dev)  
**Requested by:** Bruno Capuano

## What Changed

Scenario 3 (`src/scenario-03-csharp-simple/`) was rewritten from an HTTP client that called the FastAPI backend to a direct model invocation using `System.Diagnostics.Process` to run a Python helper script.

## Architecture

```
Before:  C# → HttpClient → FastAPI Backend → VibeVoice Model → WAV
After:   C# → Process → tts_helper.py → VibeVoice Model → WAV (no backend needed)
```

## Why Process Instead of pythonnet

- **Simpler:** No Python runtime configuration in C#, no NuGet dependencies
- **Portable:** Works with any Python venv, any OS
- **Debuggable:** Python script can be tested independently
- **Reliable:** No DLL loading issues or version conflicts

## Impact on Other Scenarios

- Scenario 3 **no longer requires** the Scenario 2 backend to be running
- Scenario 3 now requires Python + VibeVoice installed locally (own `requirements.txt`)
- Voice ID format uses Capitalized names (Carter, Emma) matching Scenario 1 convention

## Files Changed

- `src/scenario-03-csharp-simple/Program.cs` — Complete rewrite
- `src/scenario-03-csharp-simple/tts_helper.py` — New Python TTS engine
- `src/scenario-03-csharp-simple/requirements.txt` — New Python deps
- `src/scenario-03-csharp-simple/README.md` — Updated docs
- Root README, GETTING_STARTED, USER_MANUAL, ARCHITECTURE — All updated
