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

- 🔊 **Natural Text-to-Speech** powered by VibeVoice-Realtime-0.5B (~300ms latency)
- 🌍 **14 Voices** across 10 languages (English, German, French, Spanish, and more)
- 🎨 **Modern Blazor UI** with glassmorphism design
- 🚀 **.NET Aspire Orchestration** for seamless service discovery
- 📥 **Audio Download** as WAV files

## 📦 Scenarios

This project includes two ways to explore VibeVoice:

### Scenario 1: Simple Python Script
A minimal, step-by-step Python script perfect for learning VibeVoice basics.

```
src/scenario-01-simple/
├── main.py           # Step-by-step TTS demo with comments
├── requirements.txt  # Python dependencies
└── README.md         # Quick start guide
```

### Scenario 2: Full-Stack Application
A complete web application with Blazor frontend, FastAPI backend, and Aspire orchestration.

```
src/scenario-02-fullstack/
├── backend/                  # Python FastAPI + VibeVoice
├── VoiceLabs.AppHost/        # Aspire orchestration
├── VoiceLabs.ServiceDefaults/
├── VoiceLabs.Web/            # Blazor .NET 10 frontend
└── VoiceLabs.slnx            # Solution file
```

## 🛠️ Prerequisites

| Requirement | Version | Installation |
|------------|---------|--------------|
| Python | 3.11+ | [python.org](https://python.org) |
| .NET SDK | 10.0+ | [dotnet.microsoft.com](https://dotnet.microsoft.com/download/dotnet/10.0) |
| Aspire Workload | - | `dotnet workload install aspire` |
| GPU (optional) | CUDA 12.1+ | Recommended for faster inference |

## 🚀 Quick Start

### Scenario 1 — Simple Script

```bash
cd src/scenario-01-simple

# Create virtual environment
python -m venv venv
venv\Scripts\activate  # Windows
# source venv/bin/activate  # Linux/Mac

# Install dependencies
pip install -r requirements.txt

# Run the demo
python main.py
```

**Output:** `output.wav` containing synthesized speech.

### Scenario 2 — Full-Stack App

```bash
cd src/scenario-02-fullstack

# Install Python dependencies
cd backend
pip install -r requirements.txt
cd ..

# Run with Aspire
cd VoiceLabs.AppHost
dotnet run
```

Open the Aspire dashboard to access:
- **Frontend:** Blazor TTS interface
- **Backend:** FastAPI at `http://localhost:5100`

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
    ├── scenario-01-simple/       # Minimal Python TTS script
    │   ├── main.py
    │   ├── requirements.txt
    │   └── README.md
    │
    └── scenario-02-fullstack/    # Full-stack application
        ├── backend/              # FastAPI + VibeVoice
        │   ├── main.py
        │   ├── requirements.txt
        │   └── app/
        │       ├── api/routes.py
        │       ├── models/schemas.py
        │       └── services/tts_service.py
        │
        ├── VoiceLabs.AppHost/    # Aspire orchestration
        ├── VoiceLabs.ServiceDefaults/
        ├── VoiceLabs.Web/        # Blazor frontend
        ├── VoiceLabs.Web.Tests/  # xUnit tests
        └── python-api/tests/     # pytest tests
```

## 🔧 Tech Stack

| Layer | Technology | Purpose |
|-------|------------|---------|
| **TTS Engine** | [VibeVoice-Realtime-0.5B](https://huggingface.co/microsoft/VibeVoice-Realtime-0.5B) | Text-to-speech synthesis |
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