# VibeVoiceTTS - FastAPI Backend

A REST API for text-to-speech using VibeVoice-Realtime-0.5B.

## Prerequisites

- Python 3.10 or later
- GPU with CUDA recommended (works on CPU but slower)
- **Python environment set up at the repo root** (see [Getting Started](../../../docs/GETTING_STARTED.md#python-environment-setup-one-time))

## Setup

From the repository root, activate the shared virtual environment:

**Windows:**
```powershell
.\.venv\Scripts\Activate.ps1
```

**Linux/macOS:**
```bash
source .venv/bin/activate
```

If you haven't installed dependencies yet:
```bash
pip install -r requirements.txt
```

## Running

### Standalone
```bash
uvicorn main:app --host 0.0.0.0 --port 5100
```

### With Aspire

The AppHost project orchestrates this backend automatically. 

**Important:** Before running with Aspire, ensure you've created the virtual environment link:

**Windows:**
```cmd
cd src\scenario-02-fullstack\backend
mklink /J .venv ..\..\..\.venv
```

**Linux/macOS:**
```bash
cd src/scenario-02-fullstack/backend
ln -s ../../../.venv .venv
```

This creates a link to the shared root venv so Aspire can find it. See the parent solution for Aspire setup.

## API Endpoints

### `GET /api/health`
Health check for Aspire orchestration.

**Response:**
```json
{
  "status": "healthy",
  "model_loaded": true
}
```

### `GET /api/voices`
List available TTS voices.

**Response:**
```json
{
  "voices": [
    {"id": "en-carter", "name": "Carter", "language": "en", "style": "male"},
    {"id": "en-emma", "name": "Emma", "language": "en", "style": "female"}
  ]
}
```

### `POST /api/tts`
Generate speech from text.

**Request:**
```json
{
  "text": "Hello, world!",
  "voice_id": "en-carter",
  "output_format": "wav"
}
```

**Response:**
- Content-Type: `audio/wav`
- Body: Binary WAV audio data

## Project Structure

```
backend/
├── main.py                 # FastAPI app entry point
├── requirements.txt        # Python dependencies
├── app/
│   ├── api/
│   │   └── routes.py       # API endpoint definitions
│   ├── services/
│   │   └── tts_service.py  # VibeVoice TTS integration
│   └── models/
│       └── schemas.py      # Pydantic request/response models
└── README.md
```

## API Documentation

When running, visit:
- Swagger UI: http://localhost:5100/docs
- ReDoc: http://localhost:5100/redoc
