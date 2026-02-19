# Scenario 4: Real-Time Voice Conversation with AI

A full-stack real-time voice conversation app orchestrated by **.NET Aspire**. Speak into your microphone, get AI responses spoken back — all in the browser.

**Pattern:** 🎙️ Speak → STT (Parakeet) → AI Brain (OpenAI) → TTS (VibeVoice) → 🔊 Hear response

## Architecture

```
Browser (Blazor)
  ↕ WebSocket (audio + JSON messages)
Aspire AppHost
  ├── conversation-backend (Python FastAPI)
  │   ├── STT: NVIDIA Parakeet / faster-whisper
  │   ├── AI: OpenAI gpt-4o-mini
  │   └── TTS: VibeVoice-Realtime-0.5B
  └── frontend (Blazor Server)
      ├── Push-to-talk microphone capture
      ├── Audio playback via Web Audio API
      └── Chat bubble conversation UI
```

## Prerequisites

| Requirement | Details |
|---|---|
| .NET 10 SDK | [Download](https://dotnet.microsoft.com/download/dotnet/10.0) |
| Aspire workload | `dotnet workload install aspire` |
| Python 3.11+ | [python.org](https://python.org) |
| OpenAI API key | Set as `OPENAI_API_KEY` environment variable |
| GPU (optional) | CUDA 12.1+ recommended for STT + TTS models |

## Quick Start

1. **Install Python dependencies:**
   ```bash
   cd src/scenario-04-meai/backend
   pip install -r requirements.txt
   ```

2. **Set your OpenAI API key:**
   ```bash
   # Windows PowerShell
   $env:OPENAI_API_KEY = "sk-..."

   # Linux / macOS
   export OPENAI_API_KEY="sk-..."
   ```

3. **Run with Aspire:**
   ```bash
   cd src/scenario-04-meai/VoiceLabs.ConversationHost
   dotnet run
   ```

4. Open the Aspire dashboard → click the **frontend** endpoint → start talking!

## How It Works

| Step | Component | What Happens |
|------|-----------|-------------|
| 1 | Frontend | User holds push-to-talk button, mic captures 16kHz PCM audio |
| 2 | WebSocket | Audio chunks sent as binary frames to backend |
| 3 | Backend STT | Parakeet (or faster-whisper) transcribes audio to text |
| 4 | Backend AI | OpenAI gpt-4o-mini generates a conversational response |
| 5 | Backend TTS | VibeVoice synthesizes response as 24kHz WAV audio |
| 6 | WebSocket | Audio sent back as binary frames |
| 7 | Frontend | Web Audio API plays the response, chat bubbles update |

## WebSocket Protocol

The frontend and backend communicate over WebSocket at `/ws/conversation`:

| Direction | Type | Content |
|-----------|------|---------|
| Client → Server | Binary | PCM audio chunks (16kHz, 16-bit, mono) |
| Client → Server | Text | `{"type": "end_of_speech"}` signals recording complete |
| Server → Client | Text | `{"type": "transcript", "text": "..."}` |
| Server → Client | Text | `{"type": "response", "text": "..."}` |
| Server → Client | Binary | WAV audio chunks |
| Server → Client | Text | `{"type": "audio_complete"}` |

## Configuration

### Change the AI Model

Edit `backend/app/services/chat_service.py`:
```python
self.model = "gpt-4o"  # or any OpenAI-compatible model
```

### Change the Voice

Select a voice in the frontend dropdown, or set the default in the backend.
Available voices: Carter, Davis, Emma, Frank, Grace, Mike.

### STT Model

The backend tries NVIDIA Parakeet first, then falls back to faster-whisper. Configure in `backend/app/services/stt_service.py`.

## Project Structure

```
scenario-04-meai/
├── VoiceLabs.slnx                    # Solution file
├── VoiceLabs.ConversationHost/       # Aspire AppHost
│   └── AppHost.cs                    # Orchestrates backend + frontend
├── backend/                          # Python FastAPI conversation service
│   ├── main.py                       # FastAPI app + WebSocket endpoint
│   ├── app/
│   │   ├── services/
│   │   │   ├── stt_service.py        # Speech-to-text (Parakeet)
│   │   │   ├── chat_service.py       # AI brain (OpenAI)
│   │   │   └── tts_service.py        # Text-to-speech (VibeVoice)
│   │   ├── api/
│   │   │   ├── routes.py             # REST endpoints
│   │   │   └── websocket_handler.py  # WebSocket conversation loop
│   │   └── models/
│   │       └── schemas.py            # Pydantic models
│   └── requirements.txt
├── VoiceLabs.ConversationWeb/        # Blazor frontend
│   ├── Program.cs                    # Aspire service defaults + HttpClient
│   ├── Components/Pages/Home.razor   # Conversation UI
│   └── wwwroot/js/audio.js           # Mic capture + audio playback
├── VoiceLabs.ServiceDefaults/        # Aspire shared config
├── Program.cs.bak                    # Old console app (reference)
└── README.md                         # You are here
```
