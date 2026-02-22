# Scenario 4: Full C# Text-to-Speech with Aspire

A full-stack C# application demonstrating VibeVoice TTS with a **C# WebAPI backend** using the `ElBruno.VibeVoiceTTS` library, a Blazor frontend, and .NET Aspire orchestration. **Zero Python dependency at runtime.**

**Pattern:** 📝 Type text → C# WebAPI (ElBruno.VibeVoiceTTS) → 🔊 Audio response

## Architecture

```
Browser (Blazor)
  ↕ HTTP (POST /api/tts)
Aspire AppHost
  ├── backend (C# WebAPI)
  │   └── TTS: ElBruno.VibeVoiceTTS (ONNX Runtime)
  │       └── Models: auto-downloaded from HuggingFace
  └── frontend (Blazor Server)
      ├── Text input + voice selection
      ├── Audio playback via Web Audio API
      └── Chat-style conversation UI
```

## Prerequisites

| Requirement | Details |
|---|---|
| .NET 10 SDK | [Download](https://dotnet.microsoft.com/download/dotnet/10.0) |
| Aspire workload | `dotnet workload install aspire` |

> **No Python required!** The C# backend uses `ElBruno.VibeVoiceTTS` with ONNX Runtime for native inference.

## Quick Start

1. **Run with Aspire:**
   ```bash
   cd src/scenario-04-meai/VoiceLabs.ConversationHost
   dotnet run
   ```

2. Open the Aspire dashboard → click the **frontend** endpoint → start generating speech!

> **First run:** The backend will automatically download ONNX model files (~2.3 GB) from HuggingFace. Subsequent runs use the local cache.

## API Endpoints

| Method | Endpoint | Description |
|--------|----------|-------------|
| `POST` | `/api/tts` | Generate speech from text (returns WAV audio) |
| `GET` | `/api/voices` | List available voice presets |
| `GET` | `/api/health` | Health check |
| `GET` | `/api/ready` | Model readiness check |

### POST /api/tts

```json
{
  "text": "Hello, world!",
  "voice": "Carter"
}
```

**Response:** `audio/wav` binary file

## Project Structure

```
scenario-04-meai/
├── VoiceLabs.slnx                    # Solution file
├── VoiceLabs.ConversationHost/       # Aspire AppHost
│   └── AppHost.cs                    # Orchestrates backend + frontend
├── VoiceLabs.Api/                    # C# WebAPI backend
│   ├── Program.cs                    # Minimal API with TTS endpoints
│   └── VoiceLabs.Api.csproj          # References ElBruno.VibeVoiceTTS
├── VoiceLabs.ConversationWeb/        # Blazor frontend
│   ├── Program.cs                    # Aspire service defaults + HttpClient
│   └── Components/Pages/Home.razor   # Conversation UI
├── VoiceLabs.ServiceDefaults/        # Aspire shared config
├── backend/                          # Legacy Python backend (reference only)
└── README.md                         # You are here
```
