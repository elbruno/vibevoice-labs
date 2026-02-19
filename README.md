# 🎙️ VibeVoice Labs

> Showcase project demonstrating Microsoft's VibeVoice TTS with Python + Blazor + .NET Aspire

[![.NET 10](https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
[![Python 3.11+](https://img.shields.io/badge/Python-3.11+-3776AB?logo=python&logoColor=white)](https://python.org)
[![Aspire](https://img.shields.io/badge/Aspire-9.2-purple)](https://learn.microsoft.com/en-us/dotnet/aspire/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

<!-- 
![VoiceLabs Demo](docs/images/demo.gif)
-->

## ✨ Features

- 🔊 **Natural Text-to-Speech** powered by VibeVoice-Realtime-0.5B (~200ms latency)
- 🌍 **6 English Voice Presets** (Carter, Davis, Emma, Frank, Grace, Mike) + multilingual experimental voices
- 🎨 **Modern Blazor UI** with glassmorphism design
- 🚀 **.NET Aspire Orchestration** for seamless service discovery
- 📥 **Audio Download** as WAV files

## 📦 Scenarios

This project includes seven ways to explore VibeVoice across Python and .NET:

### Scenario 1: Simple Python Script
A minimal, step-by-step Python script perfect for learning VibeVoice basics. **Beginner level.**

```
src/scenario-01-simple/
├── main.py           # Step-by-step TTS demo with comments
├── requirements.txt  # Python dependencies
└── README.md         # Quick start guide
```

### Scenario 2: Full-Stack Application
A complete web application with Blazor frontend, FastAPI backend, and Aspire orchestration. **Intermediate level.**

```
src/scenario-02-fullstack/
├── backend/                  # Python FastAPI + VibeVoice
├── VoiceLabs.AppHost/        # Aspire orchestration
├── VoiceLabs.ServiceDefaults/
├── VoiceLabs.Web/            # Blazor .NET 10 frontend
└── VoiceLabs.slnx            # Solution file
```

### Scenario 3: Simple C# Console App
A .NET 10 console app that runs VibeVoice TTS using **CSnakes** to embed the Python model directly in the .NET process. No subprocess calls or HTTP backends. **Beginner level.**

```
src/scenario-03-csharp-simple/
├── Program.cs              # C# host using CSnakes
├── vibevoice_tts.py        # Python TTS module (embedded via CSnakes)
├── requirements.txt        # Python dependencies
├── .csproj                 # Project file with CSnakes NuGet
└── README.md               # Quick start guide
```

### Scenario 4: Real-Time Voice Conversation
A full-stack real-time voice conversation app. Speak into your mic, AI responds with voice — all orchestrated by Aspire. Uses Parakeet (STT) + OpenAI (AI brain) + VibeVoice (TTS). **Advanced level.**

```
src/scenario-04-meai/
├── VoiceLabs.ConversationHost/       # Aspire AppHost
├── backend/                          # Python FastAPI (STT + TTS + AI)
├── VoiceLabs.ConversationWeb/        # Blazor frontend (mic + audio)
├── VoiceLabs.ServiceDefaults/
└── VoiceLabs.slnx                    # Solution file
```

### Scenario 5: Batch TTS Processing
A Python CLI that converts a folder of .txt files to .wav. Uses VibeVoice directly, supports YAML front-matter for per-file voice, parallel processing. **Intermediate level.**

```
src/scenario-05-batch-processing/
├── batch_tts.py     # Batch processing CLI
├── requirements.txt # Python dependencies
└── README.md        # Quick start guide
```

### Scenario 6: Real-Time Streaming
A Python script demonstrating chunked audio playback for low-latency TTS applications. **Intermediate level.**

```
src/scenario-06-streaming-realtime/
├── stream_tts.py    # Real-time streaming implementation
├── requirements.txt # Python dependencies
└── README.md        # Quick start guide
```

### Scenario 7: MAUI Cross-Platform App
A .NET 10 MAUI app for Windows/macOS/Android/iOS with voice selection and audio playback. **Advanced level.**

```
src/scenario-07-maui-mobile/
├── MauiProgram.cs           # MAUI app setup
├── VoiceLabs.Mobile.csproj  # Project file
├── Pages/                   # MAUI pages
└── README.md                # Quick start guide
```

## 🛠️ Prerequisites

| Requirement | Version | Installation |
|------------|---------|--------------|
| Python | 3.11+ | [python.org](https://python.org) |
| Git | Latest | [git-scm.com](https://git-scm.com/) (required for pip install from GitHub) |
| .NET SDK | 10.0+ | [dotnet.microsoft.com](https://dotnet.microsoft.com/download/dotnet/10.0) |
| Aspire Workload | - | `dotnet workload install aspire` |
| GPU (optional) | CUDA 12.1+ | Recommended for faster inference |

## 🚀 Quick Start

### One-Time Setup (All Python Scenarios)

Create a single virtual environment at the repo root:

```bash
# From the repo root
python -m venv .venv

# Activate (Windows PowerShell)
.venv\Scripts\Activate.ps1

# Activate (Windows CMD)
.venv\Scripts\activate.bat

# Activate (Linux/macOS)
source .venv/bin/activate

# Install all Python dependencies
pip install -r requirements.txt
```

> **Note:** First installation downloads the VibeVoice model (~1-2 GB). Voice presets (~4 MB each) are auto-downloaded on first run.

### Scenario 1 — Simple Python Script

```bash
cd src/scenario-01-simple
python main.py
```

**Output:** `output.wav` containing synthesized speech.

### Scenario 2 — Full-Stack Application

```bash
cd src/scenario-02-fullstack

# Run with Aspire (starts both backend and frontend)
cd VoiceLabs.AppHost
dotnet run
```

Open the Aspire dashboard to access:
- **Frontend:** Blazor TTS interface
- **Backend:** FastAPI at `http://localhost:5100`

### Scenario 3 — Simple C# Console App

```bash
cd src/scenario-03-csharp-simple
dotnet run
```

CSnakes auto-downloads Python and installs dependencies on first run.

### Scenario 4 — Real-Time Voice Conversation

```bash
cd src/scenario-04-meai

# Install Python dependencies
cd backend && pip install -r requirements.txt && cd ..

# Set OpenAI API key
$env:OPENAI_API_KEY = "sk-..."

# Run with Aspire
cd VoiceLabs.ConversationHost
dotnet run
```

Open the Aspire dashboard → click the frontend endpoint → push-to-talk to start a conversation!

### Scenario 5 — Batch TTS Processing

```bash
cd src/scenario-05-batch-processing
python batch_tts.py --input-dir ./sample-texts --output-dir ./output --voice carter
```

### Scenario 6 — Real-Time Streaming

```bash
cd src/scenario-06-streaming-realtime
python stream_tts.py
```

### Scenario 7 — MAUI Mobile App

```bash
cd src/scenario-07-maui-mobile
dotnet run -f net10.0-windows  # Or your target platform
```

## 📁 Project Structure

```
vibevoice-labs/
├── README.md                      # You are here
├── LICENSE                        # MIT License
├── docs/
│   ├── ARCHITECTURE.md           # System design & diagrams
│   ├── GETTING_STARTED.md        # Detailed setup guide
│   ├── API_REFERENCE.md          # Backend API documentation
│   └── USER_MANUAL.md            # End-user documentation
│
└── src/
    ├── scenario-01-simple/                 # Minimal Python TTS script
    │   ├── main.py
    │   ├── requirements.txt
    │   └── README.md
    │
    ├── scenario-02-fullstack/              # Full-stack application
    │   ├── backend/                        # FastAPI + VibeVoice
    │   ├── VoiceLabs.AppHost/              # Aspire orchestration
    │   ├── VoiceLabs.ServiceDefaults/
    │   ├── VoiceLabs.Web/                  # Blazor frontend
    │   └── python-api/tests/               # pytest tests
    │
    ├── scenario-03-csharp-simple/          # Simple C# console app
    │   ├── Program.cs
    │   ├── VoiceLabs.ConsoleApp.csproj
    │   └── README.md
    │
    ├── scenario-04-meai/                   # Real-time voice conversation
    │   ├── VoiceLabs.ConversationHost/     # Aspire AppHost
    │   ├── backend/                        # Python FastAPI (STT + TTS + AI)
    │   ├── VoiceLabs.ConversationWeb/      # Blazor frontend
    │   └── VoiceLabs.slnx
    │
    ├── scenario-05-batch-processing/       # Batch TTS CLI
    │   ├── batch_tts.py
    │   ├── requirements.txt
    │   └── README.md
    │
    ├── scenario-06-streaming-realtime/     # Real-time streaming
    │   ├── streaming_tts.py
    │   ├── requirements.txt
    │   └── README.md
    │
    └── scenario-07-maui-mobile/            # MAUI cross-platform app
        ├── MauiProgram.cs
        ├── VoiceLabs.Mobile.csproj
        ├── Pages/
        └── README.md
```

## 🔧 Tech Stack

| Layer | Technology | Purpose |
|-------|------------|---------|
| **TTS Engine** | [VibeVoice-Realtime-0.5B](https://huggingface.co/microsoft/VibeVoice-Realtime-0.5B) | Text-to-speech synthesis |
| **TTS Package** | [VibeVoice](https://github.com/microsoft/VibeVoice) (installed from Git) | Streaming TTS inference |
| **Backend** | [FastAPI](https://fastapi.tiangolo.com/) + [Python](https://python.org) | REST API for TTS |
| **Frontend** | [Blazor](https://dotnet.microsoft.com/apps/aspnet/web-apps/blazor) + [.NET 10](https://dotnet.microsoft.com/) | Interactive web UI |
| **Orchestration** | [.NET Aspire](https://learn.microsoft.com/en-us/dotnet/aspire/) | Service discovery & health checks |
| **Audio** | [SoundFile](https://pysoundfile.readthedocs.io/) | WAV file I/O |

## 📚 Documentation

- [**Getting Started**](docs/GETTING_STARTED.md) — Detailed setup instructions
- [**Architecture**](docs/ARCHITECTURE.md) — System design and data flow
- [**API Reference**](docs/API_REFERENCE.md) — Backend REST API documentation
- [**User Manual**](docs/USER_MANUAL.md) — End-user guide

## 🤝 Contributing

Contributions are welcome! Please:

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/amazing-feature`)
3. Commit your changes (`git commit -m 'Add amazing feature'`)
4. Push to the branch (`git push origin feature/amazing-feature`)
5. Open a Pull Request

## 📄 License

This project is licensed under the MIT License — see the [LICENSE](LICENSE) file for details.

## 🙏 Credits

- **[VibeVoice](https://github.com/microsoft/VibeVoice)** — Text-to-speech model by Microsoft
- **[.NET Aspire](https://learn.microsoft.com/en-us/dotnet/aspire/)** — Cloud-native orchestration by Microsoft
- **Bruno Capuano** — Project creator

---

<p align="center">
  Made with ❤️ by <a href="https://github.com/elbruno">Bruno Capuano</a>
</p>