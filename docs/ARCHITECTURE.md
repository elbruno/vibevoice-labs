# Architecture

This document describes the system architecture of VibeVoice Labs.

## System Overview

```
┌─────────────────────────────────────────────────────────────────────┐
│                        .NET Aspire AppHost                          │
│                    (Service Orchestration)                          │
│  ┌─────────────────────────────────────────────────────────────┐   │
│  │                    Service Discovery                         │   │
│  │              http://backend → localhost:5100                 │   │
│  └─────────────────────────────────────────────────────────────┘   │
│                               │                                     │
│         ┌─────────────────────┴─────────────────────┐              │
│         ▼                                           ▼              │
│  ┌─────────────────┐                      ┌─────────────────┐      │
│  │  Blazor Web UI  │      HTTP/REST       │  FastAPI        │      │
│  │  (.NET 10)      │◄────────────────────►│  Backend        │      │
│  │                 │                      │  (Python)       │      │
│  │  - Text Input   │    GET /api/voices   │                 │      │
│  │  - Voice Select │    POST /api/tts     │  ┌───────────┐  │      │
│  │  - Audio Player │    GET /api/health   │  │VibeVoice  │  │      │
│  │  - Download     │                      │  │TTS Model  │  │      │
│  └─────────────────┘                      │  │(0.5B)     │  │      │
│         │                                 │  └───────────┘  │      │
│         ▼                                 └─────────────────┘      │
│  ┌─────────────────┐                              │                │
│  │   Browser       │                              ▼                │
│  │   <audio>       │◄──────────────────── WAV Audio Bytes          │
│  └─────────────────┘                                               │
└─────────────────────────────────────────────────────────────────────┘
```

## Component Descriptions

### 1. Aspire AppHost (`VoiceLabs.AppHost`)

The orchestration layer that manages service lifecycle and discovery.

**Responsibilities:**
- Launch and monitor the Python backend via `AddUvicornApp`
- Launch the Blazor frontend as a .NET project
- Configure service endpoints and environment variables
- Provide health checks and observability dashboard

**Key Configuration:**
```csharp
var backend = builder.AddUvicornApp("backend", "../backend", "main:app")
    .WithHttpEndpoint(port: 5100, env: "PORT")
    .WithExternalHttpEndpoints();

builder.AddProject<Projects.VoiceLabs_Web>("frontend")
    .WithReference(backend)
    .WaitFor(backend);
```

### 2. Blazor Frontend (`VoiceLabs.Web`)

A .NET 10 Blazor Server application providing the user interface.

**Features:**
- Text input with 1000 character limit
- Dynamic voice selection dropdown (populated from API)
- Sample text presets (collapsible section)
- HTML5 audio player for playback
- WAV file download capability

**Technology Choices:**
- **Blazor Server** — Real-time interactivity without WASM download
- **Glassmorphism UI** — Modern, visually appealing design
- **HTTP resilience** — Automatic retries via Aspire ServiceDefaults

### 3. FastAPI Backend (`backend/`)

A Python REST API that wraps the VibeVoice TTS model.

**Architecture:**
```
backend/
├── main.py              # FastAPI app entry point
├── app/
│   ├── api/
│   │   └── routes.py    # API endpoint definitions
│   ├── models/
│   │   └── schemas.py   # Pydantic request/response models
│   └── services/
│       └── tts_service.py  # VibeVoice wrapper (singleton)
```

**Design Patterns:**
- **Singleton TTS Service** — Model loaded once at startup
- **Voice Registry** — Centralized voice metadata with ID mappings
- **Pydantic Validation** — Type-safe request/response handling

### 4. VibeVoice TTS Model

Microsoft's VibeVoice-Realtime-0.5B model for text-to-speech synthesis.

**Specifications:**
- **Parameters:** 0.5 billion
- **Latency:** ~200ms to first audio (real-time capable)
- **Sample Rate:** 24kHz
- **Voice Presets:** Pre-computed .pt files for each speaker (Carter, Davis, Emma, Frank, Grace, Mike)
- **Package:** Installed from GitHub: `vibevoice[streamingtts] @ git+https://github.com/microsoft/VibeVoice.git`
- **Key Classes:** `VibeVoiceStreamingForConditionalGenerationInference` + `VibeVoiceStreamingProcessor`
- **Inference:** Uses `processor.process_input_with_cached_prompt()` with voice preset files

## Data Flow

### TTS Generation Flow

```
┌──────────┐    ┌──────────┐    ┌──────────┐    ┌──────────┐
│  User    │    │  Blazor  │    │  FastAPI │    │VibeVoice │
│  Input   │    │  Frontend│    │  Backend │    │  Model   │
└────┬─────┘    └────┬─────┘    └────┬─────┘    └────┬─────┘
     │               │               │               │
     │ Enter text    │               │               │
     │ Select voice  │               │               │
     ├──────────────►│               │               │
     │               │               │               │
     │               │ POST /api/tts │               │
     │               │ {text, voice} │               │
     │               ├──────────────►│               │
     │               │               │               │
     │               │               │ generate()    │
     │               │               ├──────────────►│
     │               │               │               │
     │               │               │◄──────────────┤
     │               │               │  numpy array  │
     │               │               │               │
     │               │◄──────────────┤               │
     │               │  WAV bytes    │               │
     │               │               │               │
     │◄──────────────┤               │               │
     │ Audio player  │               │               │
     │ (base64 data) │               │               │
     │               │               │               │
```

### Sequence Details

1. **User Input** — User enters text and selects a voice
2. **API Request** — Blazor sends POST to `/api/tts` with JSON payload
3. **Voice Mapping** — Backend maps voice ID to VibeVoice speaker code
4. **TTS Generation** — VibeVoice model generates audio samples
5. **WAV Encoding** — Audio converted to WAV bytes using soundfile
6. **Response** — WAV bytes returned with `audio/wav` content type
7. **Playback** — Frontend converts to base64 data URL for `<audio>` element

## API Contract

### Endpoints

| Method | Path | Description |
|--------|------|-------------|
| `GET` | `/api/health` | Health check for Aspire |
| `GET` | `/api/voices` | List available TTS voices |
| `POST` | `/api/tts` | Generate speech from text |

### Request/Response Models

**Voice Object:**
```json
{
  "id": "en-carter",
  "name": "Carter",
  "language": "en",
  "style": "male"
}
```

**TTS Request:**
```json
{
  "text": "Hello, world!",
  "voice_id": "en-carter",
  "output_format": "wav"
}
```

**Error Response:**
```json
{
  "error": "Text is required",
  "code": "VALIDATION_ERROR"
}
```

## Technology Choices & Rationale

### Why .NET Aspire?

- **Service Discovery** — Automatic DNS resolution between services
- **Observability** — Built-in dashboard with logs, traces, metrics
- **Health Checks** — Automatic health monitoring and restarts
- **Python Support** — `AddUvicornApp` for seamless Python integration

### Why FastAPI?

- **Performance** — Async support for efficient I/O
- **OpenAPI** — Automatic API documentation at `/docs`
- **Pydantic** — Type validation with clear error messages
- **Ecosystem** — Easy integration with ML/AI libraries

### Why Blazor Server?

- **No WASM** — Faster initial load without WebAssembly download
- **Real-time** — SignalR connection for instant UI updates
- **C# Everywhere** — Consistent language across frontend logic

### Why WAV Format?

- **Simplicity** — No encoding complexity
- **Quality** — Lossless audio preservation
- **Compatibility** — Universal browser support
- **Streaming** — Easy to chunk for future streaming support

## Deployment Considerations

### Development
```bash
cd VoiceLabs.AppHost
dotnet run
```
Aspire manages all services automatically.

### Production

For production deployment, consider:

1. **Model Caching** — Pre-download VibeVoice model to container
2. **GPU Support** — Deploy backend to GPU-enabled infrastructure
3. **Load Balancing** — Multiple backend instances with shared model cache
4. **CORS Configuration** — Restrict origins in production
5. **Rate Limiting** — Prevent abuse of TTS endpoint

## Related Documentation

- [Getting Started](GETTING_STARTED.md) — Setup instructions
- [API Reference](API_REFERENCE.md) — Detailed API documentation
- [User Manual](USER_MANUAL.md) — End-user guide
