# Architecture

This document describes the system architecture of VibeVoice Labs across all 7 scenarios.

## Scenario Architectures at a Glance

| Scenario | Architecture | Components | Use Case |
|----------|--------------|-----------|----------|
| 1 | **Standalone Python** | Python script + VibeVoice TTS | Learning, local batch generation |
| 2 | **Aspire Orchestration** | Blazor frontend + FastAPI backend | Full-stack web application |
| 3 | **CSnakes Embedded Python** | C# console + embedded CPython via CSnakes | Running VibeVoice directly from .NET process |
| 4 | **Real-Time Conversation** | Blazor frontend + Python FastAPI backend + OpenAI | AI-driven voice conversation with streaming |
| 5 | **Batch Processing** | Python CLI + direct TTS model | Bulk text-to-audio conversion with YAML overrides |
| 6 | **Streaming Real-Time** | Python streaming processor | Low-latency audio playback (~300ms to first chunk) |
| 7 | **MAUI Cross-Platform** | MAUI UI + HTTP TTS client | Mobile/desktop app across Windows, Android, iOS, macOS |

---

## Detailed Architecture: Scenario 2 (Full-Stack)

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
// Uses the shared virtual environment at the repository root (.venv via junction link)
var backend = builder.AddUvicornApp("backend", "../backend", "main:app");

builder.AddProject<Projects.VoiceLabs_Web>("frontend")
    .WithReference(backend)
    .WaitFor(backend);
```

> **Note:** Aspire finds the `.venv` in the backend directory automatically. To share the root venv, a junction link must be created from `backend/.venv` to the root `.venv` (see [Getting Started](GETTING_STARTED.md#step-2-create-virtual-environment-link-first-time-only)).

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

---

## Detailed Architecture: Scenario 3 (CSnakes Embedded Python)

```
┌─────────────────────────────────────────────────────────────┐
│                  .NET Process (C#)                          │
│                                                             │
│  ┌────────────────────────────────────────────────────┐    │
│  │            Console Application                     │    │
│  │  • Voice selector                                  │    │
│  │  • Text input (hardcoded or interactive)          │    │
│  │  • Output path configuration                      │    │
│  │                                                    │    │
│  │  ┌─────────────────────────────────────────────┐  │    │
│  │  │  CSnakes Runtime (Embedded CPython)         │  │    │
│  │  │  • Hosts Python 3.11+ interpreter           │  │    │
│  │  │  • Auto-creates virtual environment         │  │    │
│  │  │  • Installs requirements.txt packages       │  │    │
│  │  │  • Executes vibevoice_tts.py functions     │  │    │
│  │  │                                             │  │    │
│  │  │  ┌──────────────────────────────────────┐  │  │    │
│  │  │  │  vibevoice_tts.py Module             │  │  │    │
│  │  │  │  • synthesize_speech() function      │  │  │    │
│  │  │  │  • Voice preset management           │  │  │    │
│  │  │  │  • VibeVoice model inference         │  │  │    │
│  │  │  │  • WAV file output                   │  │  │    │
│  │  │  │                                      │  │  │    │
│  │  │  │  ┌───────────────────────────────┐  │  │  │    │
│  │  │  │  │ VibeVoice-Realtime-0.5B       │  │  │  │    │
│  │  │  │  │ • Voice presets (.pt files)   │  │  │  │    │
│  │  │  │  │ • Audio generation            │  │  │  │    │
│  │  │  │  │ • Speaker embeddings          │  │  │  │    │
│  │  │  │  └───────────────────────────────┘  │  │  │    │
│  │  │  │                                      │  │  │    │
│  │  │  │  ┌───────────────────────────────┐  │  │  │    │
│  │  │  │  │ PyTorch (CPU or CUDA)         │  │  │  │    │
│  │  │  │  │ • Tensor operations           │  │  │  │    │
│  │  │  │  │ • GPU acceleration (optional) │  │  │  │    │
│  │  │  │  └───────────────────────────────┘  │  │  │    │
│  │  │  └──────────────────────────────────────┘  │  │    │
│  │  │                                             │  │    │
│  │  └─────────────────────────────────────────────┘  │    │
│  │                                                    │    │
│  └────────────────────────────────────────────────────┘    │
│                           │                                 │
│                           ▼                                 │
│  ┌────────────────────────────────────────────────────┐    │
│  │  File System (output.wav)                          │    │
│  └────────────────────────────────────────────────────┘    │
└─────────────────────────────────────────────────────────────┘
```

## Component Descriptions

### 1. C# Console Application

A simple .NET console application that orchestrates the TTS workflow.

**Responsibilities:**
- Accept voice and text parameters
- Configure CSnakes Python runtime
- Call Python synthesize_speech() function
- Display progress and results

**Key Pattern:**
```csharp
var module = env.VibevoiceTts();
var result = module.SynthesizeSpeech(text, voice, outputPath);
```

### 2. CSnakes Runtime

CSnakes is a Python-in-memory library for .NET that embeds a CPython interpreter.

**Features:**
- **No subprocess** — Python runs in-process, zero IPC overhead
- **Auto-bootstrap** — Automatically downloads Python and installs dependencies
- **Type-safe interop** — Generates C# wrappers from Python type annotations
- **Virtual environment** — Isolates dependencies per project
- **GPU support** — Passes through CUDA for hardware acceleration

**Configuration:**
```csharp
builder.Services
    .WithPython()
    .WithHome(pythonHome)
    .FromRedistributable();
```

### 3. VibeVoice TTS Module (vibevoice_tts.py)

A Python module providing the `synthesize_speech()` function.

**Responsibilities:**
- Download voice presets (.pt files) on first run
- Load VibeVoice-Realtime-0.5B model and processor
- Handle voice preset matching
- Generate audio using cached prompts (speaker embeddings)
- Save output as WAV file with soundfile

**Supported Voices:**
- Carter, Davis, Emma, Frank, Grace, Mike (English)

**Type Annotations:**
All public functions have Python type hints for CSnakes code generation:
```python
def synthesize_speech(text: str, voice: str, output_path: str) -> str:
    """Returns status message"""
```

### 4. Voice Presets

Pre-computed speaker embeddings downloaded from VibeVoice GitHub repository.

**Storage:**
```
scenario-03-csharp-simple/voices/
├── en-Carter_man.pt
├── en-Davis_man.pt
├── en-Emma_woman.pt
├── en-Frank_man.pt
├── en-Grace_woman.pt
└── en-Mike_man.pt
```

**Mechanism:**
Voice presets are cached PyTorch tensors containing speaker-specific embeddings that condition the TTS model for consistent voice quality.

## Data Flow

```
┌──────────┐    ┌────────────────┐    ┌──────────────┐    ┌──────────┐
│   User   │    │  C# Console    │    │  CSnakes     │    │VibeVoice │
│  Input   │    │  Application   │    │  Runtime     │    │  Model   │
└────┬─────┘    └────┬───────────┘    └──────┬───────┘    └────┬─────┘
     │               │                       │                 │
     │ Voice + Text  │                       │                 │
     ├──────────────►│                       │                 │
     │               │                       │                 │
     │               │ Call synthesize_      │                 │
     │               │ speech(text, voice)   │                 │
     │               ├──────────────────────►│                 │
     │               │                       │                 │
     │               │                       │ Load model      │
     │               │                       ├────────────────►│
     │               │                       │                 │
     │               │                       │ Process text +  │
     │               │                       │ voice preset    │
     │               │                       ├────────────────►│
     │               │                       │                 │
     │               │                       │◄────────────────┤
     │               │                       │ Audio tensor    │
     │               │                       │                 │
     │               │◄──────────────────────┤                 │
     │               │ WAV bytes + status    │                 │
     │               │                       │                 │
     │◄──────────────┤                       │                 │
     │ Success / Error                       │                 │
     │               │                       │                 │
```

### Sequence Details

1. **Initialize CSnakes** — Console creates Python runtime, installs dependencies
2. **Download Voices** — First run downloads voice preset files (~20 MB total)
3. **Load Model** — VibeVoice-Realtime-0.5B loaded into memory (~2 GB)
4. **Process Input** — Text and voice preset converted to model tensors
5. **Generate Audio** — Model inference produces audio tensor
6. **Save WAV** — Audio written to file using soundfile library
7. **Return Result** — Status message returned to C# caller

## Technology Choices & Rationale

### Why CSnakes?

- **In-Process Execution** — No subprocess overhead or IPC delays
- **Type Safety** — Automatic C# wrapper generation from Python type hints
- **Deployment** — Single .NET executable, no separate Python installation required
- **Development** — Pure C# project can call Python functions directly

### Why Embedded Instead of HTTP?

- **Latency** — Direct function calls faster than network round-trips
- **Resource Efficiency** — Shared process memory, no serialization overhead
- **Simplicity** — No server setup, ports, or service orchestration needed

### Trade-offs

| Aspect | Embedded (CSnakes) | HTTP (Scenario 2/7) |
|--------|-------------------|-------------------|
| **Latency** | Lowest (~direct calls) | Higher (~50-200ms) |
| **Deployment** | Single .exe | Separate backend + frontend |
| **Scalability** | Single process | Horizontal (multiple backends) |
| **UI** | Console only | Web / MAUI / Mobile |
| **Resource** | All in one memory space | Isolated processes |

---

## Detailed Architecture: Scenario 4 (Real-Time Conversation)

```
┌────────────────────────────────────────────────────────────────┐
│                  .NET Aspire AppHost                           │
│              (Service Orchestration + Discovery)               │
│  ┌──────────────────────────────────────────────────────────┐  │
│  │         Service Discovery & Health Checks                │  │
│  │          http://conversation-backend                     │  │
│  └──────────────────────────────────────────────────────────┘  │
│                  │                              │              │
│         ┌────────┴────────────┐        ┌────────┴───────────┐  │
│         ▼                     ▼        ▼                    ▼  │
│  ┌──────────────────┐  ┌──────────────────────────┐           │
│  │ Blazor Frontend  │  │  Python FastAPI Backend  │           │
│  │  (.NET 10)       │  │  (Conversation Service)  │           │
│  │                  │  │                          │           │
│  │ • Mic capture    │  │  ┌────────────────────┐  │           │
│  │   (16kHz PCM)    │  │  │ STT Service        │  │           │
│  │ • Push-to-talk   │  │  │ (Parakeet/Whisper) │  │           │
│  │   button UI      │  │  │                    │  │           │
│  │ • Chat bubbles   │  │  │ Text → Transcript  │  │           │
│  │ • Audio playback │  │  └────────────────────┘  │           │
│  │   (Web Audio API)│  │            ↓              │           │
│  │                  │  │  ┌────────────────────┐  │           │
│  │ Web Socket       │  │  │ Chat Service       │  │           │
│  │ /ws/conversation │  │  │ (OpenAI gpt-4o-   │  │           │
│  │                  │  │  │  mini)            │  │           │
│  │ • Binary frames  │  │  │                    │  │           │
│  │   (audio chunks) │  │  │ Transcript → Reply │  │           │
│  │ • Text messages  │  │  └────────────────────┘  │           │
│  │   (protocol)     │  │            ↓              │           │
│  │                  │  │  ┌────────────────────┐  │           │
│  │ HTML5 <audio>    │  │  │ TTS Service        │  │           │
│  │ data URL playback│  │  │ (VibeVoice-0.5B)   │  │           │
│  │                  │  │  │                    │  │           │
│  │                  │  │  │ Reply → Audio      │  │           │
│  │                  │  │  └────────────────────┘  │           │
│  └──────────────────┘  └──────────────────────────┘           │
│         │                       │                              │
│         └───────WebSocket───────┘                              │
│             (Audio + Protocol)                                 │
│                                                                │
└────────────────────────────────────────────────────────────────┘
```

## Component Descriptions

### 1. Blazor Frontend (VoiceLabs.ConversationWeb)

A .NET 10 Blazor Server application providing the real-time voice conversation UI.

**Responsibilities:**
- Capture microphone audio via Web Audio API
- Establish WebSocket connection to backend
- Send audio chunks to backend in real-time
- Display chat bubbles for transcripts and AI responses
- Play audio responses via HTML5 <audio> element

**Key Features:**
- **Push-to-talk** — Hold button to record, release to send
- **Streaming Protocol** — Binary frames for audio, text messages for control
- **Chat Bubbles** — User utterances vs. AI responses visually distinct

### 2. Aspire AppHost (VoiceLabs.ConversationHost)

Orchestration layer managing Python backend and .NET frontend lifecycle.

**Configuration:**
- Launches Python FastAPI backend via `AddUvicornApp`
- Registers Blazor frontend as .NET project
- Configures service discovery for `http://conversation-backend` DNS name
- Provides health checks and observability dashboard

### 3. Python FastAPI Backend (backend/)

Core service handling speech-to-text, AI conversation, and text-to-speech.

**Architecture:**
```
backend/
├── main.py                   # FastAPI app + WebSocket handler
├── app/
│   ├── services/
│   │   ├── stt_service.py    # Speech-to-text (Parakeet)
│   │   ├── chat_service.py   # AI responses (OpenAI)
│   │   └── tts_service.py    # Text-to-speech (VibeVoice)
│   ├── api/
│   │   ├── routes.py         # REST endpoints (/health, /voices)
│   │   └── websocket_handler.py  # WebSocket conversation loop
│   └── models/
│       └── schemas.py        # Pydantic models
```

### 3a. STT Service (Speech-to-Text)

Converts audio to text using NVIDIA Parakeet or faster-whisper.

**Flow:**
1. Receive PCM audio chunks from WebSocket
2. Accumulate frames until end-of-speech signal
3. Run inference on accumulated audio
4. Return transcript back to frontend

**Models:**
- **Primary:** NVIDIA Parakeet (faster, on-device)
- **Fallback:** OpenAI Whisper (via faster-whisper, more accurate)

### 3b. Chat Service

Generates conversational AI responses using OpenAI's API.

**Configuration:**
```python
self.client = OpenAI(api_key=os.getenv("OPENAI_API_KEY"))
self.model = "gpt-4o-mini"  # Configurable
```

**Flow:**
1. Receive transcript from STT
2. Send to OpenAI with system prompt (conversational assistant)
3. Stream response back to WebSocket client
4. Return complete response to TTS service

### 3c. TTS Service

Generates speech audio from text using VibeVoice.

**Flow:**
1. Receive text from Chat service
2. Select voice (configurable, default: Carter)
3. Synthesize audio using VibeVoice-Realtime-0.5B
4. Return WAV bytes to WebSocket client

## Data Flow

```
┌──────────┐    ┌──────────┐    ┌──────────┐    ┌──────────┐
│  User    │    │ Frontend │    │ Backend  │    │ External │
│  Speech  │    │ (Blazor) │    │(FastAPI) │    │ Services │
└────┬─────┘    └────┬─────┘    └────┬─────┘    └────┬─────┘
     │               │               │               │
     │ Speak into    │               │               │
     │ microphone    │               │               │
     ├──────────────►│               │               │
     │               │ WebSocket     │               │
     │               │ PCM audio     │               │
     │               ├──────────────►│               │
     │               │               │               │
     │               │               │ Transcribe   │
     │               │               │ (Parakeet)   │
     │               │               │               │
     │               │               │ Generate     │
     │               │               ├──────────────┤
     │               │               │ AI response  │
     │               │               │              │ OpenAI
     │               │               │◄──────────────┤ API
     │               │               │               │
     │               │               │ Synthesize   │
     │               │               │ audio        │
     │               │               │               │
     │               │ WebSocket     │               │
     │               │ WAV bytes     │               │
     │               │◄──────────────┤               │
     │               │               │               │
     │◄──────────────┤               │               │
     │ Hear response │               │               │
     │ (Web Audio)   │               │               │
     │               │               │               │
```

### WebSocket Protocol

**Client → Server (Binary frames):**
- PCM audio chunks (16-bit, 16kHz, mono)
- Text message: `{"type": "end_of_speech"}`

**Server → Client (Text messages):**
- `{"type": "transcript", "text": "..."}`
- `{"type": "response", "text": "..."}`
- `{"type": "audio_complete"}`

**Server → Client (Binary frames):**
- WAV audio bytes for playback

## Technology Choices & Rationale

### Why WebSocket?

- **Bidirectional** — Real-time communication without polling
- **Low Latency** — Binary frame overhead minimal
- **Persistent Connection** — Single connection for entire conversation
- **Streaming** — Audio and text messages multiplexed on same channel

### Why Parakeet + OpenAI + VibeVoice Stack?

| Component | Why |
|-----------|-----|
| **Parakeet STT** | Fast on-device transcription, no API calls needed |
| **OpenAI GPT-4o-mini** | Conversational, context-aware, cost-effective |
| **VibeVoice TTS** | Real-time capable (~300ms), multiple English voices |

### Trade-offs vs. Scenario 2

| Aspect | Scenario 2 | Scenario 4 |
|--------|-----------|-----------|
| **Complexity** | Simple request/response | Stateful WebSocket streaming |
| **Use Case** | One-shot TTS | Interactive conversation |
| **AI** | None (user provides text) | OpenAI generates responses |
| **Latency** | ~1-2 seconds | ~300-500ms (streaming) |
| **Cost** | Free (VibeVoice only) | Per-token charges (OpenAI API) |

---

## Detailed Architecture: Scenario 5 (Batch Processing)

```
┌────────────────────────────────────────────────────────────┐
│           Python CLI (batch_tts.py)                        │
│                                                            │
│  ┌──────────────────────────────────────────────────────┐  │
│  │  Command-Line Interface                              │  │
│  │  • --input-dir (text files to process)               │  │
│  │  • --output-dir (where WAV files go)                 │  │
│  │  • --voice (default voice)                           │  │
│  │  • --parallel (number of threads)                    │  │
│  │                                                      │  │
│  │  ┌────────────────────────────────────────────────┐  │  │
│  │  │  File Scanner                                  │  │  │
│  │  │  • Recursive directory scan for .txt files    │  │  │
│  │  │  • Parse YAML front-matter (voice override)   │  │  │
│  │  │                                                │  │  │
│  │  │  ┌─────────────────────────────────────────┐  │  │  │
│  │  │  │  ThreadPoolExecutor                     │  │  │  │
│  │  │  │  (Parallel TTS Generation)              │  │  │  │
│  │  │  │                                          │  │  │  │
│  │  │  │  ┌──────────────────────────────────┐  │  │  │  │
│  │  │  │  │ TTS Worker Thread (x4 default)   │  │  │  │  │
│  │  │  │  │                                   │  │  │  │  │
│  │  │  │  │ ┌──────────────────────────────┐ │  │  │  │  │
│  │  │  │  │ │ VibeVoice Model Instance    │ │  │  │  │  │
│  │  │  │  │ │ (shared across threads)     │ │  │  │  │  │
│  │  │  │  │ │                              │ │  │  │  │  │
│  │  │  │  │ │ • Processor                 │ │  │  │  │  │
│  │  │  │  │ │ • Model inference           │ │  │  │  │  │
│  │  │  │  │ │ • Voice preset cache        │ │  │  │  │  │
│  │  │  │  │ │ • GPU allocation            │ │  │  │  │  │
│  │  │  │  │ └──────────────────────────────┘ │  │  │  │  │
│  │  │  │  │                                   │  │  │  │  │
│  │  │  │  │ for each text file:              │  │  │  │  │
│  │  │  │  │ 1. Load text content             │  │  │  │  │
│  │  │  │  │ 2. Generate audio (VibeVoice)    │  │  │  │  │
│  │  │  │  │ 3. Save WAV file                 │  │  │  │  │
│  │  │  │  │ 4. Update progress bar           │  │  │  │  │
│  │  │  │  └──────────────────────────────────┘  │  │  │  │
│  │  │  │                                          │  │  │  │
│  │  │  └─────────────────────────────────────────┘  │  │  │
│  │  │           │                                   │  │  │
│  │  └───────────┼───────────────────────────────────┘  │  │
│  │              │                                       │  │
│  │              ▼                                       │  │
│  │  ┌──────────────────────────────────────────────┐  │  │
│  │  │  Summary Report                              │  │  │
│  │  │  • Total files processed                     │  │  │
│  │  │  • Total duration                            │  │  │
│  │  │  • Timing per file                           │  │  │
│  │  │  • Success / failure summary                 │  │  │
│  │  └──────────────────────────────────────────────┘  │  │
│  └──────────────────────────────────────────────────────┘  │
│                                                            │
└────────────────────────────────────────────────────────────┘
          │                              │
          ▼                              ▼
    ┌──────────────┐          ┌────────────────┐
    │  Input Dir   │          │  Output Dir    │
    │  (text .txt) │          │  (audio .wav)  │
    └──────────────┘          └────────────────┘
```

## Component Descriptions

### 1. CLI Interface (Click Framework)

Command-line tool using Python Click for argument parsing.

**Options:**
```bash
--input-dir    Path to directory with .txt files (default: ./sample-texts)
--output-dir   Path to save .wav files (default: ./output)
--voice        Default voice for all files (default: carter)
--parallel     Number of parallel workers (default: 1)
```

### 2. File Scanner

Recursively scans input directory for `.txt` files.

**Features:**
- **YAML Front-Matter** — Each file can specify voice override:
  ```
  ---
  voice: fr-woman
  ---
  Bonjour le monde!
  ```
- **Fallback Logic** — Uses front-matter voice if present, else CLI `--voice`
- **Error Handling** — Skips malformed files, logs errors

### 3. ThreadPoolExecutor

Parallel processing pool for multi-threaded TTS generation.

**Configuration:**
- Default: 1 worker (sequential)
- User-configurable: `--parallel 4` for 4 concurrent workers
- Model instance shared across all threads (PyTorch-safe)

**Benefits:**
- CPU workers process multiple files while GPU generates audio
- I/O (disk reads/writes) doesn't block TTS inference
- Linear speedup with parallel workers (up to GPU memory limit)

### 4. TTS Worker

Each thread in the pool executes the same TTS generation workflow.

**Workflow:**
1. Dequeue work item (text file + voice)
2. Load text content from file
3. Call VibeVoice model to generate audio
4. Write WAV file to output directory
5. Update progress bar
6. Return result (success/failure)

**Thread Safety:**
- VibeVoice model inference is thread-safe with PyTorch
- File I/O uses thread-safe paths (each worker has unique output filename)
- Progress bar uses thread-safe counter

### 5. Voice Preset Cache

Loaded voice presets (speaker embeddings) held in memory across all workers.

**Presets:**
```python
VOICE_PRESETS = {
    "carter": "en-Carter_man.pt",
    "emma": "en-Emma_woman.pt",
    "fr-woman": "fr-Spk1_woman.pt",
    # ... etc
}
```

**Caching Strategy:**
- Load each unique voice preset once at startup
- Store in-memory as PyTorch tensors
- All threads reference same tensor (no duplication)
- Reduces disk I/O and model load overhead

### 6. Summary Report

Final console output summarizing batch job results.

**Information:**
- Total files processed
- Total wall-clock time
- Average time per file
- Success count
- Failure count (if any)
- Timing metrics (tqdm progress)

## Data Flow

```
┌──────────────┐    ┌─────────────┐    ┌──────────────┐
│ Input Files  │    │   Worker    │    │ Output Files │
│  (.txt)      │    │  Threads    │    │   (.wav)     │
└────┬─────────┘    └─────┬───────┘    └──────┬───────┘
     │                    │                    │
     │ Scan directory     │                    │
     ├───────────────────►│                    │
     │                    │                    │
     │ Dequeue job        │                    │
     ├───────────────────►│                    │
     │                    │                    │
     │                    │ Load text          │
     │                    │ Load voice preset  │
     │                    │ Generate audio    │
     │                    │ Save WAV           │
     │                    ├───────────────────►│
     │                    │                    │
     │                    │◄───────────────────┤
     │                    │ Result (success)   │
     │                    │                    │
     │◄───────────────────┤                    │
     │ Report stats       │                    │
     │                    │                    │
```

### Sequence Details

1. **Scan Input** — Recursively find all .txt files in input directory
2. **Parse Metadata** — Extract YAML front-matter or use CLI default voice
3. **Submit Jobs** — Enqueue (text, voice) pairs to ThreadPoolExecutor
4. **Parallel Generation** — Multiple workers generate audio concurrently
5. **Progress Tracking** — tqdm progress bar updates in real-time
6. **Result Aggregation** — Collect success/failure for each file
7. **Report Summary** — Print timing, counts, and success rate

## Technology Choices & Rationale

### Why Batch Processing?

- **Cost** — Single model load amortized across many files
- **Throughput** — Process 100+ files in time of Scenario 1 (one-shot TTS)
- **Parallelism** — Thread pool uses idle time efficiently
- **Flexibility** — Per-file voice overrides via YAML front-matter

### Why ThreadPoolExecutor?

- **Lightweight** — No multiprocessing serialization overhead
- **Shared Model** — Single PyTorch model in memory across threads
- **GIL Aware** — Releases GIL during inference (GPU/heavy compute)

### YAML Front-Matter Pattern

Inspired by Jekyll/Hugo static site generators:
```
---
voice: fr-woman
style: formal
---
Votre contenu en français.
```

Advantages:
- Self-documenting (metadata lives with content)
- Separates data from code
- Easy to parse (PyYAML)
- Familiar to developers (common in docs/blogs)

---

## Detailed Architecture: Scenario 6 (Streaming Real-Time)

```
┌──────────────────────────────────────────────────────────────┐
│         Python Streaming Demo (stream_tts.py)                │
│                                                              │
│  ┌───────────────────────────────────────────────────────┐   │
│  │  Console Output                                       │   │
│  │  • Model load progress                               │   │
│  │  • Streaming progress bar (chunks arriving)          │   │
│  │  • Performance metrics (time-to-first-chunk, etc.)   │   │
│  │                                                      │   │
│  │  ┌──────────────────────────────────────────────┐   │   │
│  │  │  VibeVoice Streaming Processor               │   │   │
│  │  │  • Input: text + voice preset                │   │   │
│  │  │  • Output: audio chunks via generator        │   │   │
│  │  │                                              │   │   │
│  │  │  ┌────────────────────────────────────────┐ │   │   │
│  │  │  │ VibeVoice-Realtime-0.5B Model          │ │   │   │
│  │  │  │ • Streaming generation (not batch)     │ │   │   │
│  │  │  │ • generate_stream() returns chunks     │ │   │   │
│  │  │  │ • Time-to-first-chunk: ~300ms         │ │   │   │
│  │  │  │ • Speaker embeddings (voice preset)   │ │   │   │
│  │  │  │ • Device: CPU or CUDA                 │ │   │   │
│  │  │  │                                        │ │   │   │
│  │  │  │  ┌──────────────────────────────────┐ │ │   │   │
│  │  │  │  │ PyTorch Runtime                 │ │ │   │   │
│  │  │  │  │ • Tensor operations             │ │ │   │   │
│  │  │  │  │ • Flash Attention 2 (GPU opt.)  │ │ │   │   │
│  │  │  │  │ • SDPA fallback (CPU)           │ │ │   │   │
│  │  │  │  └──────────────────────────────────┘ │ │   │   │
│  │  │  └────────────────────────────────────────┘ │   │   │
│  │  │           │                                  │   │   │
│  │  │           ▼                                  │   │   │
│  │  │  ┌────────────────────────────────────────┐ │   │   │
│  │  │  │ Audio Streaming Loop                   │ │   │   │
│  │  │  │                                        │ │   │   │
│  │  │  │ 1. Receive audio chunk from model    │ │   │   │
│  │  │  │ 2. Convert to numpy array            │ │   │   │
│  │  │  │ 3. Play via sounddevice (real-time)  │ │   │   │
│  │  │  │ 4. Accumulate in buffer              │ │   │   │
│  │  │  │ 5. Update progress bar                │ │   │   │
│  │  │  └────────────────────────────────────────┘ │   │   │
│  │  │                                              │   │   │
│  │  │  ┌────────────────────────────────────────┐ │   │   │
│  │  │  │ File Saving (soundfile)                │ │   │   │
│  │  │  │ • Save full audio buffer to WAV       │ │   │   │
│  │  │  │ • 24kHz sample rate                   │ │   │   │
│  │  │  │ • Mono or stereo                      │ │   │   │
│  │  │  └────────────────────────────────────────┘ │   │   │
│  │  └──────────────────────────────────────────────┘   │   │
│  │                      │                               │   │
│  │                      ▼                               │   │
│  │  ┌──────────────────────────────────────────────┐   │   │
│  │  │ Performance Summary                          │   │   │
│  │  │ • Time to first chunk (ms)                  │   │   │
│  │  │ • Total generation time (s)                │   │   │
│  │  │ • Real-time factor (generated/played)      │   │   │
│  │  │ • Latency metrics                          │   │   │
│  │  └──────────────────────────────────────────────┘   │   │
│  └───────────────────────────────────────────────────┘   │
│                      │                                   │
│                      ▼                                   │
│  ┌──────────────────────────────────────────────────┐   │
│  │ Audio Output                                     │   │
│  │ • Real-time playback (speakers/headphones)      │   │
│  │ • File output (stream_output.wav)               │   │
│  └──────────────────────────────────────────────────┘   │
└──────────────────────────────────────────────────────────────┘
```

## Component Descriptions

### 1. VibeVoice Streaming Processor

Core TTS engine using VibeVoice's streaming/chunked generation.

**Key Methods:**
- `process_input_with_cached_prompt()` — Prepare text + voice embedding
- `generate_stream()` — Generator yielding audio chunks as they become available

**Streaming Advantages:**
- Latency to first audio output: **~300 ms** (vs. 1-2s for batch generation)
- Memory efficient: Process one chunk at a time
- User perceives real-time response (begins hearing immediately)

**Voice Presets:**
Cached speaker embeddings loaded once, reused for streaming:
```python
all_prefilled_outputs = torch.load(voice_path, device=device)
```

### 2. Audio Streaming Loop

Receives audio chunks from VibeVoice generator and plays them immediately.

**Responsibilities:**
- **Pull from Generator** — Iterate over `generate_stream()` output
- **Playback** — Send chunk to sounddevice for speaker output
- **Buffering** — Accumulate chunks for final file save
- **Progress** — Update tqdm progress bar for each chunk
- **Timing** — Track time-to-first-chunk and total generation time

**Error Handling:**
- If sounddevice unavailable (no audio hardware), silently fall back to file-only mode
- If PortAudio library missing on Linux, print helpful installation message

### 3. Audio Playback (sounddevice)

Bridges Python tensors to speaker hardware.

**Configuration:**
```python
sd.play(audio_chunk, samplerate=24000)
```

**Platform Support:**
- **Windows:** Direct sound integration
- **macOS:** CoreAudio
- **Linux:** PulseAudio or ALSA (requires libportaudio2)

**Fallback Logic:**
```python
try:
    import sounddevice
except (ImportError, OSError):
    PLAYBACK_AVAILABLE = False  # Fall back to file-only
```

### 4. File Saving (soundfile)

Accumulates streamed chunks and writes final WAV file.

**Specification:**
- **Sample Rate:** 24,000 Hz
- **Format:** WAV (PCM)
- **Bit Depth:** 32-bit float or 16-bit int (configurable)
- **Channels:** Mono (1 channel)

**Storage:**
```
stream_output.wav  (~5 MB for typical demo text)
```

## Data Flow

```
┌──────────┐    ┌────────────┐    ┌──────────────┐
│  Input   │    │ VibeVoice  │    │   Playback   │
│  Text    │    │ Streaming  │    │   & File     │
└────┬─────┘    └────┬───────┘    └──────┬───────┘
     │               │                   │
     │ Select voice  │                   │
     ├──────────────►│                   │
     │               │                   │
     │               │ Load model        │
     │               │ Load voice preset │
     │               │                   │
     │               │ Process input     │
     │               │ (text + embeddings)
     │               │                   │
     │               ├──────────────────►│
     │               │ Audio chunk 1     │
     │               │ (~24k samples)    │
     │               │                   │
     │               ├──────────────────►│ Play (300ms)
     │               │ Audio chunk 2     │ Buffer + display
     │               │                   │
     │               ├──────────────────►│ Play (300ms)
     │               │ Audio chunk 3     │ Buffer + display
     │               │                   │
     │               │ ...               │
     │               │                   │
     │               │ EOF (no more      │
     │               │ chunks)           │
     │               │                   │
     │               │                   ├───────►│
     │               │                   │ Save file
     │               │                   │ (stream_output.wav)
     │               │                   │
```

### Key Performance Metrics

| Metric | Value | Significance |
|--------|-------|--------------|
| **Time to First Chunk** | ~300 ms | User hears response immediately |
| **Total Generation Time** | 1-3s (varies by text) | How long model runs |
| **Real-Time Factor (RTF)** | >1.0 (often 2-3x) | Audio generated faster than playback |
| **Chunk Size** | ~2-4 KB each | Frequent updates, smooth streaming |

**Real-Time Factor Example:**
- Text: "Hello, welcome to VibeVoice Labs." (4 seconds of audio)
- Generation: 2 seconds wall-clock time
- **RTF = 4 / 2 = 2×** (generated in half the playback time)

## Technology Choices & Rationale

### Why Streaming?

- **Latency** — Perceived responsiveness of ~300ms instead of full generation time
- **Interactivity** — User can interrupt long generation (future enhancement)
- **Scalability** — Chunks can be sent over network (future streaming server)
- **UX** — Audio begins playing before full generation complete

### Why VibeVoice Streaming Model?

- **Real-Time Capable** — Specifically designed for low-latency synthesis
- **Lightweight** — 0.5B parameters fit in consumer GPU memory
- **Quality** — Competitive with larger models despite smaller size
- **Open Source** — Community maintained, reproducible

### Trade-offs

| Aspect | Streaming (Scenario 6) | Batch (Scenario 5) |
|--------|----------------------|-------------------|
| **User Experience** | Immediate audio feedback | Simpler (single file output) |
| **Latency** | ~300ms to first audio | 1-2s per file (blocking) |
| **Complexity** | Generator + async handling | Sequential loop |
| **Use Case** | Interactive / real-time | Offline / batch jobs |

---

## Detailed Architecture: Scenario 7 (.NET MAUI Cross-Platform)

```
┌─────────────────────────────────────────────────────────────┐
│           .NET MAUI Application                             │
│      (Single Codebase, Multiple Platforms)                  │
│                                                             │
│  ┌─────────────────────────────────────────────────────┐   │
│  │ MauiProgram.cs                                      │   │
│  │ • Dependency injection setup                        │   │
│  │ • HttpClient configuration                          │   │
│  │ • Service registration                              │   │
│  │                                                     │   │
│  │ ┌─────────────────────────────────────────────┐   │   │
│  │ │  MainPage.xaml / MainPage.xaml.cs           │   │   │
│  │ │                                             │   │   │
│  │ │  UI (XAML)                                 │   │   │
│  │ │  ├── Header / Title                       │   │   │
│  │ │  ├── Text Input Entry                     │   │   │
│  │ │  │   (multiline, 1000 char limit)         │   │   │
│  │ │  ├── Voice Picker (Picker control)        │   │   │
│  │ │  │   (populated from /api/voices)         │   │   │
│  │ │  ├── Generate Button                      │   │   │
│  │ │  ├── Audio Player Control                 │   │   │
│  │ │  │   (using Plugin.Maui.Audio)            │   │   │
│  │ │  └── Status / Error messages              │   │   │
│  │ │                                             │   │   │
│  │ │  Event Handlers (C#)                      │   │   │
│  │ │  ├── OnGenerateClicked()                 │   │   │
│  │ │  ├── OnPlayClicked()                     │   │   │
│  │ │  ├── OnDownloadClicked()                 │   │   │
│  │ │  └── UpdateUI(response)                  │   │   │
│  │ │                                             │   │   │
│  │ └─────────────────────────────────────────────┘   │   │
│  │           │                                       │   │
│  │           ▼                                       │   │
│  │ ┌─────────────────────────────────────────────┐   │   │
│  │ │  TtsService (Services/TtsService.cs)        │   │   │
│  │ │                                             │   │   │
│  │ │  HttpClient Wrapper                        │   │   │
│  │ │  ├── GetVoicesAsync()                     │   │   │
│  │ │  │   GET /api/voices → List<Voice>       │   │   │
│  │ │  │                                         │   │   │
│  │ │  ├── GenerateTtsAsync(text, voice_id)    │   │   │
│  │ │  │   POST /api/tts → byte[] (WAV)        │   │   │
│  │ │  │                                         │   │   │
│  │ │  └── Error handling & timeouts            │   │   │
│  │ │                                             │   │   │
│  │ └─────────────────────────────────────────────┘   │   │
│  │                                                     │   │
│  └─────────────────────────────────────────────────────┘   │
│              │                                              │
│              ▼                                              │
│  ┌─────────────────────────────────────────────────────┐   │
│  │  Platform-Specific Renderers (Auto-generated)       │   │
│  │  ├── Windows (WinUI 3 native controls)             │   │
│  │  ├── Android (AndroidX native controls)            │   │
│  │  ├── iOS (UIKit native controls)                   │   │
│  │  └── macOS (AppKit native controls)                │   │
│  └─────────────────────────────────────────────────────┘   │
│              │                                              │
└──────────────┼──────────────────────────────────────────────┘
               │
        ┌──────┴──────┬──────────┬──────────┐
        ▼             ▼          ▼          ▼
    ┌────────┐  ┌─────────┐  ┌─────┐  ┌──────┐
    │Windows │  │ Android │  │ iOS │  │macOS │
    │(WinUI) │  │(AndroidX)  │(UIKit)  │(AppKit)
    └────────┘  └─────────┘  └─────┘  └──────┘
        │             │          │          │
        ▼             ▼          ▼          ▼
    ┌─────────────────────────────────────────┐
    │  Python Backend (FastAPI)               │
    │  http://localhost:5100 (or configured)  │
    │                                         │
    │  • VibeVoice TTS                        │
    │  • Voice registry                       │
    │  • Health checks                        │
    └─────────────────────────────────────────┘
```

## Component Descriptions

### 1. MAUI Application Shell

Cross-platform app hosting the TTS interface.

**Architecture:**
- **Single C# / XAML codebase** — Compiles to Windows, Android, iOS, macOS
- **Platform renderers** — MAUI automatically maps controls to native UI
- **Native performance** — Direct access to platform APIs

**Target Frameworks:**
```csharp
net10.0-windows10.0.19041.0
net10.0-android34
net10.0-ios17
net10.0-maccatalyst14
```

### 2. MainPage (UI Layer)

User interface for text input, voice selection, and audio playback.

**XAML Components:**
```xaml
<Entry x:Name="textInput" Placeholder="Enter text to synthesize" />
<Picker x:Name="voicePicker" Title="Select Voice" />
<Button Text="Generate" Clicked="OnGenerateClicked" />
<Button Text="Play" Clicked="OnPlayClicked" />
<Label x:Name="statusLabel" Text="Ready" />
```

**Event Handlers (C#):**
- `OnGenerateClicked()` — Calls TtsService.GenerateTtsAsync()
- `OnPlayClicked()` — Uses Plugin.Maui.Audio to play WAV bytes
- `OnVoiceChanged()` — Updates voice selection for next generation

### 3. TtsService

HttpClient wrapper for the Python backend API.

**Responsibilities:**
```csharp
public async Task<List<Voice>> GetVoicesAsync()
    // GET /api/voices
    
public async Task<byte[]> GenerateTtsAsync(string text, string voiceId)
    // POST /api/tts
    // Returns WAV bytes
```

**Configuration:**
```csharp
var backendUrl = "http://localhost:5100";  // Desktop/Windows
// For Android emulator:
var backendUrl = "http://10.0.2.2:5100";   // Host machine
```

### 4. Audio Playback Plugin

Cross-platform audio playback via `Plugin.Maui.Audio`.

**Capabilities:**
- Play WAV bytes from memory (no file storage needed)
- Control playback (play, pause, stop)
- Manage volume and playback state
- Native audio APIs per platform:
  - **Windows:** NAudio or Windows.Media.Audio
  - **Android:** MediaPlayer (AndroidX)
  - **iOS:** AVAudioPlayer
  - **macOS:** AVAudioPlayer

### 5. Dependency Injection Setup

MAUI's DI container registration in MauiProgram.cs.

**Configuration:**
```csharp
builder
    .UseMauiApp<App>()
    .AddMauiControlsHostingHandler()
    .ConfigureServices(services =>
    {
        services.AddSingleton<TtsService>();
        services.AddSingleton<MainPage>();
        
        var httpClientHandler = new HttpClientHandler();
        services.AddSingleton(new HttpClient(httpClientHandler)
        {
            Timeout = TimeSpan.FromSeconds(30),
            BaseAddress = new Uri(backendUrl)
        });
    });
```

## Data Flow

```
┌──────────┐    ┌────────────────┐    ┌──────────┐
│   User   │    │ MAUI Frontend  │    │ Backend  │
│ Interaction   │ (All Platforms)│    │ FastAPI  │
└────┬─────┘    └────┬───────────┘    └────┬─────┘
     │               │                      │
     │ Type text     │                      │
     ├──────────────►│                      │
     │ Select voice  │                      │
     ├──────────────►│                      │
     │ Tap Generate  │                      │
     ├──────────────►│                      │
     │               │ GET /api/voices      │
     │               ├─────────────────────►│
     │               │                      │
     │               │ [Voice list JSON]    │
     │               │◄─────────────────────┤
     │               │                      │
     │               │ POST /api/tts        │
     │               │ {text, voice_id}     │
     │               ├─────────────────────►│
     │               │                      │
     │               │ [WAV bytes (audio)]  │
     │               │◄─────────────────────┤
     │               │                      │
     │ Hear audio    │ Play (Plugin.Audio)  │
     │◄──────────────┤                      │
     │               │                      │
```

### Sequence Details

1. **App Launch** — MainPage loads, TtsService initializes HttpClient
2. **Fetch Voices** — Call GetVoicesAsync(), populate voice picker dropdown
3. **User Input** — User enters text and selects voice
4. **Generate** — Call GenerateTtsAsync(), show progress indicator
5. **Receive Audio** — WAV bytes returned from backend
6. **Play Audio** — Plugin.Maui.Audio plays bytes via native platform API
7. **Playback** — User hears audio on speakers/headphones

## Technology Choices & Rationale

### Why .NET MAUI?

- **Single Codebase** — One C# / XAML project for all platforms
- **Native Performance** — Compiles to native code (WinUI, AndroidX, UIKit, AppKit)
- **Modern Tooling** — Latest .NET runtime (10.0) with async/await
- **Consistency** — Same API across platforms, native look & feel

### Why HTTP Client Instead of Direct Backend Integration?

- **Separation of Concerns** — Thin client (MAUI) + thick backend (Python)
- **Scalability** — Multiple clients can share one backend service
- **Portability** — Backend can run anywhere (on-prem, cloud, Docker)
- **Technology Freedom** — Backend language independent from MAUI

### Why Plugin.Maui.Audio?

- **Cross-Platform** — Single NuGet, works on all MAUI platforms
- **Simple API** — Just `player.Play(stream)` for playback
- **Native Quality** — Uses platform-native audio APIs internally
- **Low Latency** — Minimal overhead for in-memory WAV playback

### Trade-offs vs. Other Scenarios

| Aspect | Scenario 2 (Web) | Scenario 7 (Mobile) |
|--------|-----------------|-------------------|
| **Platform Coverage** | Windows + Mac (browsers) | Windows, Android, iOS, macOS (native) |
| **Distribution** | Via browser / URL | App Store / Play Store |
| **Offline** | Requires backend online | Thin client only (backend still needed) |
| **Development** | Blazor + CSS | MAUI + XAML |
| **Install** | No install (web) | Install required |

---
