# Decisions

This file contains all team decisions. Append-only.

---

### 2026-02-19: Project initialized
**By:** Squad (Coordinator)
**What:** VibeVoice Labs project created with Python + Blazor + Aspire stack
**Why:** User requested a showcase project for VibeVoice TTS demonstrations

### 2026-02-19: Tech stack confirmed
**By:** Squad (Coordinator)
**What:** 
- Scenario 1: Minimal Python script with VibeVoice-Realtime-0.5B
- Scenario 2: FastAPI backend + Blazor .NET 10 frontend + Aspire orchestration
**Why:** Matches user requirements for multi-language showcase with modern tooling

### 2026-02-19: Project structure and API contract
**By:** Holden
**What:** Defined directory layout, API contract, key interfaces, and dependencies for both scenarios
**Why:** Establishes clear boundaries for parallel implementation by Naomi (backend) and Alex (frontend)

---

## Directory Layout

```
vibevoice-labs/
├── README.md                          # Project overview
├── docs/
│   └── user-manual.md                 # User documentation
│
├── src/
│   ├── scenario-01-simple/            # Scenario 1: Minimal Python script
│   │   ├── main.py                    # Step-by-step TTS demo
│   │   ├── requirements.txt
│   │   └── README.md                  # How to run the script
│   │
│   └── scenario-02-fullstack/         # Scenario 2: Full stack app
│       ├── VoiceLabs.sln              # Solution file
│       │
│       ├── VoiceLabs.AppHost/         # Aspire orchestration
│       │   ├── Program.cs
│       │   └── VoiceLabs.AppHost.csproj
│       │
│       ├── VoiceLabs.ServiceDefaults/ # Shared Aspire defaults
│       │   ├── Extensions.cs
│       │   └── VoiceLabs.ServiceDefaults.csproj
│       │
│       ├── VoiceLabs.Web/             # Blazor .NET 10 frontend
│       │   ├── Program.cs
│       │   ├── Components/
│       │   │   ├── Layout/
│       │   │   └── Pages/
│       │   │       └── Home.razor     # Main TTS interface
│       │   ├── wwwroot/
│       │   └── VoiceLabs.Web.csproj
│       │
│       └── backend/                   # Python FastAPI backend
│           ├── main.py                # FastAPI app entry point
│           ├── requirements.txt
│           ├── app/
│           │   ├── __init__.py
│           │   ├── api/
│           │   │   └── routes.py      # API endpoints
│           │   ├── services/
│           │   │   └── tts_service.py # VibeVoice TTS logic
│           │   └── models/
│           │       └── schemas.py     # Pydantic models
│           └── README.md
│
└── tests/
    ├── backend/                       # Python tests
    │   └── test_tts.py
    └── frontend/                      # Blazor tests
        └── VoiceLabs.Web.Tests/
```

---

## API Contract

### Base URL
- Development: `http://localhost:5100` (configured via Aspire)

### Endpoints

#### `GET /api/voices`
Returns available voice options.

**Response:**
```json
{
  "voices": [
    {
      "id": "en-US-Aria",
      "name": "Aria",
      "language": "en-US",
      "style": "general"
    },
    {
      "id": "de-DE-Katja",
      "name": "Katja", 
      "language": "de-DE",
      "style": "general"
    }
    // ... more voices
  ]
}
```

#### `POST /api/tts`
Generate speech from text.

**Request:**
```json
{
  "text": "Hello, world!",
  "voice_id": "en-US-Aria",
  "output_format": "wav"
}
```

**Response:**
- Content-Type: `audio/wav`
- Body: Binary audio data

**Error Response:**
```json
{
  "error": "Text is required",
  "code": "VALIDATION_ERROR"
}
```

#### `GET /api/health`
Health check endpoint.

**Response:**
```json
{
  "status": "healthy",
  "model_loaded": true
}
```

---

## Key Interfaces

### What Naomi (Backend) provides:
1. **TTS Service** — Wraps VibeVoice-Realtime-0.5B model
2. **Voice Registry** — List of supported voices with metadata
3. **Audio Generation** — Returns WAV audio from text + voice selection
4. **Health endpoint** — For Aspire orchestration

### What Alex (Frontend) provides:
1. **Text Input** — Multi-line text area with character count
2. **Sample Texts** — Collapsible section with preset examples
3. **Voice Selector** — Dropdown populated from `/api/voices`
4. **Audio Player** — HTML5 audio element with playback controls
5. **Download Button** — Save generated audio locally
6. **Collapsible UI** — Modern accordion-style sections

### Shared Contract:
- Audio format: **WAV** (simplest, no encoding complexity)
- Text limit: **1000 characters** (reasonable for TTS demo)
- Error handling: Standard HTTP status codes + JSON error body

---

## Dependencies

### Scenario 1 — Simple Script
**Python:**
```
vibevoice>=0.1.0
torch>=2.0.0
soundfile>=0.12.0
```

### Scenario 2 — Full Stack

**Backend (Python):**
```
fastapi>=0.115.0
uvicorn>=0.32.0
vibevoice>=0.1.0
torch>=2.0.0
soundfile>=0.12.0
pydantic>=2.0.0
python-multipart>=0.0.9
```

**Frontend (Blazor .NET 10):**
```xml
<PackageReference Include="Microsoft.AspNetCore.Components.Web" Version="10.0.0" />
```

**Aspire AppHost:**
```xml
<PackageReference Include="Aspire.AppHost" Version="9.2.0" />
<PackageReference Include="Aspire.Hosting.Python" Version="9.2.0" />
```

**ServiceDefaults:**
```xml
<PackageReference Include="Microsoft.Extensions.ServiceDiscovery" Version="9.2.0" />
<PackageReference Include="Microsoft.Extensions.Http.Resilience" Version="9.2.0" />
```

---

## Aspire Configuration

**AppHost/Program.cs:**
```csharp
var builder = DistributedApplication.CreateBuilder(args);

var backend = builder.AddUvicornApp("backend", "../backend", "main:app")
    .WithHttpEndpoint(port: 5100, env: "PORT");

builder.AddProject<Projects.VoiceLabs_Web>("frontend")
    .WithReference(backend);

builder.Build().Run();
```

---

## Implementation Order

1. **Naomi** → Backend skeleton with `/api/health` and `/api/voices` stubs
2. **Alex** → Frontend with voice selector calling `/api/voices`
3. **Naomi** → Full TTS service with `/api/tts` endpoint
4. **Alex** → Audio playback and download integration
5. **Both** → Integration testing with Aspire orchestration

---

### 2026-02-19: Blazor Frontend Architecture
**By:** Alex (Frontend Dev)
**What:** Implemented complete Blazor .NET 10 frontend with Aspire orchestration

---

## Solution Structure

Created `src/scenario-02-fullstack/` with:
- **VoiceLabs.AppHost/** — Aspire orchestration using `AddUvicornApp` for Python backend
- **VoiceLabs.ServiceDefaults/** — Standard Aspire service defaults with resilience and service discovery
- **VoiceLabs.Web/** — Blazor Server app with interactive TTS interface

## UI Design Decisions

### "Radically Awesome" Styling
- Dark theme with gradient accents (#6366f1 → #8b5cf6 → #ec4899)
- Glassmorphism effect using `backdrop-filter: blur(20px)`
- Animated glow effects and smooth transitions
- Responsive design down to mobile

### Collapsible Sections
- Sample texts are hidden by default to reduce clutter
- Click-to-expand pattern for better UX
- Smooth CSS animations on expand/collapse

### Audio Handling
- HTML5 `<audio>` element with base64 data URLs
- No server-side file storage needed
- Download button triggers browser download via JS interop

## API Integration

### Service Discovery
- Backend accessed via `http://backend` URL scheme
- Aspire handles DNS resolution and health checks
- Typed HttpClient with automatic resilience

### JSON Serialization
- Python API uses snake_case (`voice_id`, `model_loaded`)
- C# models use `[JsonPropertyName]` attributes for compatibility
- Request/response models match API contract from Holden's decision

## Dependencies

```xml
<!-- AppHost -->
<PackageReference Include="Aspire.Hosting.Python" Version="13.1.1" />

<!-- ServiceDefaults (auto-generated) -->
Microsoft.Extensions.ServiceDiscovery
Microsoft.Extensions.Http.Resilience
OpenTelemetry packages
```

---

## Status

**Implemented** — Solution builds successfully, ready for backend integration testing.
