# Scenario 3 — C# Console Simple Demo

A simple C# console app that calls the VibeVoice Python backend via HTTP. This mirrors **Scenario 1** (Python simple script) but written in C#.

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- Python backend running (from Scenario 2)

## Quick Start

### 1. Start the Python backend

```bash
cd src/scenario-02-fullstack/backend
pip install -r requirements.txt
uvicorn main:app --port 5100
```

### 2. Run the console app

```bash
cd src/scenario-03-csharp-simple
dotnet run
```

The app will generate an `output.wav` file in the current directory.

## Configuration

| Variable | Default | Description |
|---|---|---|
| `VIBEVOICE_BACKEND_URL` | `http://localhost:5100` | Backend API base URL |

```bash
# Example: custom backend URL
set VIBEVOICE_BACKEND_URL=http://localhost:8000
dotnet run
```

## What Each Step Does

| Step | Description |
|---|---|
| **1** | Setup `HttpClient` with configurable backend URL |
| **2** | Check backend health via `GET /api/health` |
| **3** | List available voices via `GET /api/voices` |
| **4** | Define the text and voice to synthesize |
| **5** | Generate audio via `POST /api/tts` |
| **6** | Save the WAV response to `output.wav` |
| **7** | Print confirmation with file size info |

## Trying Different Voices

Edit `Program.cs` and uncomment any of the alternative voice lines to try different languages and styles. The file includes commented examples for English variants, German, French, Italian, Spanish, Portuguese, Dutch, Japanese, and Korean.
