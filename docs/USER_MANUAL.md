# VibeVoice Labs User Manual

Welcome to **VibeVoice Labs**, a showcase project demonstrating Microsoft's VibeVoice Text-to-Speech (TTS) capabilities using Python and .NET technologies.

---

## Table of Contents

1. [Introduction](#introduction)
2. [Prerequisites](#prerequisites)
3. [All Scenarios Overview](#all-scenarios-overview)
4. [Scenario 1: Simple Python Script](#scenario-1-simple-python-script)
5. [Scenario 2: Full-Stack Application](#scenario-2-full-stack-application)
6. [Scenario 3: Simple C# Console](#scenario-3-simple-c-console)
7. [Scenario 4: Microsoft.Extensions.AI Agent](#scenario-4-microsoftextensionsai-agent)
8. [Scenario 5: Batch Processing](#scenario-5-batch-processing)
9. [Scenario 6: Real-Time Streaming](#scenario-6-real-time-streaming)
10. [Scenario 7: MAUI Cross-Platform](#scenario-7-maui-cross-platform)
11. [Using the Web Interface](#using-the-web-interface)
12. [Available Voices](#available-voices)
13. [Tips & Best Practices](#tips--best-practices)
14. [Troubleshooting](#troubleshooting)
15. [FAQ](#faq)

---

## Introduction

VibeVoice Labs demonstrates how to integrate Microsoft's VibeVoice-Realtime-0.5B model into applications. The project includes **7 scenarios** showcasing different use cases:

- **Scenario 1:** Minimal Python script for learning TTS basics
- **Scenario 2:** Full-stack web application with Blazor UI, FastAPI backend, and .NET Aspire
- **Scenario 3:** Simple C# console app calling the Python backend via HTTP
- **Scenario 4:** AI agent using Microsoft.Extensions.AI (MEAI) to generate and speak responses
- **Scenario 5:** Batch TTS processing CLI for converting folders of text to WAV files
- **Scenario 6:** Real-time streaming demonstration with low-latency audio playback
- **Scenario 7:** Cross-platform MAUI app for Windows, macOS, Android, and iOS

### Key Features

| Feature | Description |
|---------|-------------|
| 🔊 Natural Speech | High-quality TTS with ~200ms latency (VibeVoice-Realtime-0.5B) |
| 🌍 Voice Presets | 6 English voices (Carter, Davis, Emma, Frank, Grace, Mike) |
| 🎨 Modern UI | Glassmorphism design with dark theme (Blazor) |
| 📥 Download | Export audio as WAV files |
| 🤖 AI Integration | Microsoft.Extensions.AI support for AI-driven TTS |
| 📱 Mobile Support | MAUI app for multiple platforms |
| ⚡ Streaming | Real-time chunked audio playback |

---

## All Scenarios Overview

| # | Name | Language | Focus | Difficulty | When to Use |
|---|------|----------|-------|-----------|-----------|
| 1 | Simple Python | Python | Learning basics | Beginner | Want to understand VibeVoice TTS fundamentals |
| 2 | Full-Stack Web | C# + Python | Modern web app | Intermediate | Need a complete web application with UI |
| 3 | C# Console | C# | HTTP client | Beginner | Learning to call a remote API from C# |
| 4 | MEAI Agent | C# | AI + TTS | Intermediate | Build AI agents that speak |
| 5 | Batch Processing | Python | Bulk conversion | Intermediate | Convert many text files to audio |
| 6 | Real-Time Stream | Python | Low-latency play | Intermediate | Understand streaming audio generation |
| 7 | MAUI Mobile | C# (MAUI) | Cross-platform UI | Advanced | Build mobile/desktop apps with TTS |



---

## Prerequisites

### For Scenario 1 (Simple Script)

| Requirement | Version | Link |
|-------------|---------|------|
| Python | 3.11+ | [python.org/downloads](https://python.org/downloads/) |
| pip | Latest | Included with Python |
| Audio device | - | Speakers or headphones |

### For Scenario 2 (Full-Stack)

| Requirement | Version | Link |
|-------------|---------|------|
| Python | 3.11+ | [python.org/downloads](https://python.org/downloads/) |
| .NET SDK | 10.0+ | [dotnet.microsoft.com](https://dotnet.microsoft.com/download/dotnet/10.0) |
| Aspire Workload | Latest | `dotnet workload install aspire` |
| Browser | Modern | Chrome, Edge, Firefox, or Safari |

### Hardware Requirements

| Component | Minimum | Recommended |
|-----------|---------|-------------|
| RAM | 8 GB | 16 GB |
| Storage | 5 GB | 10 GB |
| GPU | None | NVIDIA with CUDA 12.1+ |

---

## Scenario 1: Simple Python Script

A step-by-step learning experience with VibeVoice TTS.

### Quick Start

```bash
# Navigate to scenario directory
cd src/scenario-01-simple

# Create and activate virtual environment
python -m venv venv
venv\Scripts\activate        # Windows
# source venv/bin/activate   # Linux/macOS

# Install dependencies
pip install -r requirements.txt

# Run the demo
python main.py
```

### Expected Output

```
Loading VibeVoice-Realtime-0.5B model...
Model loaded successfully!
Generating audio for: 'Hello! Welcome to VibeVoice Labs...'
Saving audio to output.wav...

✅ Audio generated successfully!
   File: output.wav
   Size: 45.2 KB
   Duration: 3.25 seconds
   Sample Rate: 24000 Hz
```

### What the Script Does

1. **Loads the Model** — Downloads VibeVoice-Realtime-0.5B (~1-2 GB on first run)
2. **Generates Audio** — Converts sample text to speech
3. **Saves WAV File** — Outputs `output.wav` in the current directory

### Customizing the Script

Open `main.py` to:

- **Change the text:** Edit the `text` variable
- **Try different voices:** Change the `SPEAKER_NAME` variable (Carter, Davis, Emma, Frank, Grace, Mike)

---

## Scenario 2: Full-Stack Application

A complete web application with modern UI and API backend.

### Quick Start

```bash
# Navigate to scenario directory
cd src/scenario-02-fullstack

# Install backend dependencies
cd backend
python -m venv venv
venv\Scripts\activate
pip install -r requirements.txt
cd ..

# Run with Aspire
cd VoiceLabs.AppHost
dotnet run
```

### What Happens

1. **Aspire starts** the Python backend and Blazor frontend
2. **Dashboard opens** in your browser with service status
3. **Model loads** in the background (check health endpoint)

### Accessing Services

| Service | URL | Description |
|---------|-----|-------------|
| Aspire Dashboard | Auto-opens | Service health and logs |
| Frontend | Click in dashboard | Blazor TTS interface |
| Backend API | `http://localhost:5100` | FastAPI REST API |
| API Docs | `http://localhost:5100/docs` | Interactive Swagger UI |

---

## Scenario 3: Simple C# Console

A console app that mirrors Scenario 1 but calls the Python backend via HTTP.

### Quick Start

**Terminal 1: Start the Python Backend**

```bash
cd src/scenario-02-fullstack/backend
python -m venv venv
venv\Scripts\activate
pip install -r requirements.txt
uvicorn main:app --port 5100
```

**Terminal 2: Run the Console App**

```bash
cd src/scenario-03-csharp-simple
dotnet run
```

### What Happens

1. **Health Check** — Verifies the backend is running
2. **Voice Listing** — Fetches available voices from the API
3. **Text-to-Speech** — Sends text and generates audio
4. **Save Output** — Stores result as `output.wav`

### Configuration

Set a custom backend URL:

```bash
$env:VIBEVOICE_BACKEND_URL = "http://localhost:8000"
dotnet run
```

---

## Scenario 4: Microsoft.Extensions.AI Agent

An AI agent that generates text responses and speaks them using Microsoft.Extensions.AI (MEAI).

### Prerequisites

- OpenAI API key (or local LLM via Ollama)
- Python backend running (from Scenario 2)

### Quick Start

```bash
# Set your OpenAI API key
$env:OPENAI_API_KEY = "sk-..."

# Terminal 1: Start the Python backend
cd src/scenario-02-fullstack/backend
python -m venv venv
venv\Scripts\activate
pip install -r requirements.txt
uvicorn main:app --port 5100

# Terminal 2: Run the agent
cd src/scenario-04-meai
dotnet run
```

### How It Works

1. **Initialize MEAI IChatClient** with your LLM (OpenAI gpt-4o-mini by default)
2. **Create SpeechPlugin** that wraps the VibeVoice HTTP API
3. **Ask a question** — AI generates a text response
4. **Speak the response** — Send generated text to VibeVoice API
5. **Play audio** — Automatically play the generated speech

### Customization

Edit `Program.cs` to:
- Use Azure OpenAI, Ollama, or another LLM provider
- Change the voice selection
- Modify the agent's prompt

---

## Scenario 5: Batch Processing

A Python CLI that converts a folder of `.txt` files to `.wav` using parallel processing.

### Quick Start

```bash
cd src/scenario-05-batch-processing
python -m venv venv
venv\Scripts\activate
pip install -r requirements.txt
python batch_tts.py
```

### Usage Examples

```bash
# Use different voices
python batch_tts.py --voice emma --output-dir ./results

# Enable parallel processing (process 4 files simultaneously)
python batch_tts.py --parallel 4

# Custom input/output directories
python batch_tts.py --input-dir ./my-texts --output-dir ./my-audio --parallel 2
```

### YAML Front-Matter

Override the voice for specific files:

```
---
voice: fr
---
Bonjour! Ceci est un texte en français.
```

### Features

- ✅ Processes all `.txt` files in a directory
- ✅ Parallel processing for faster batch jobs
- ✅ Per-file voice override via YAML front-matter
- ✅ Progress bar and summary report
- ✅ WAV output at 24kHz sample rate

---

## Scenario 6: Real-Time Streaming

A Python demonstration of chunked audio playback for low-latency TTS.

### Quick Start

```bash
cd src/scenario-06-streaming-realtime
python -m venv venv
venv\Scripts\activate
pip install -r requirements.txt
python stream_tts.py
```

### What You'll Experience

- **Audio starts playing** ~300 ms after generation begins
- **No waiting** for the entire text to be synthesized
- **Real-time factor** printed at the end (1.8x = generation faster than playback)
- **Full audio saved** to `stream_output.wav`

### Performance Metrics

| Metric | What It Means |
|--------|--------------|
| Time to first chunk | Latency before audio playback starts |
| Total generation time | Wall-clock time for full synthesis |
| Real-time factor | >1.0 = faster than real-time |

---

## Scenario 7: MAUI Cross-Platform

A .NET MAUI application supporting Windows, macOS, Android, and iOS.

### Prerequisites

```bash
dotnet workload install maui
```

### Quick Start

**Terminal 1: Start the Python Backend**

```bash
cd src/scenario-02-fullstack/backend
python -m venv venv
venv\Scripts\activate
pip install -r requirements.txt
uvicorn main:app --port 5100
```

**Terminal 2: Run the MAUI App**

```bash
cd src/scenario-07-maui-mobile
dotnet workload install maui

# Windows Desktop
dotnet build -t:Run -f net10.0-windows10.0.19041.0

# Android Emulator
dotnet build -t:Run -f net10.0-android

# macOS (Mac Catalyst)
dotnet build -t:Run -f net10.0-maccatalyst

# iOS (requires Mac with Xcode)
dotnet build -t:Run -f net10.0-ios
```

### Configuration

Edit `MauiProgram.cs` to set the backend URL:

```csharp
var backendUrl = "http://localhost:5100";           // Local dev
// var backendUrl = "http://10.0.2.2:5100";         // Android emulator
// var backendUrl = "http://your-server:5100";      // Remote server
```

### Features

- ✅ Clean, modern UI with dark theme
- ✅ Text input with character counter
- ✅ Voice selection dropdown
- ✅ Audio playback via Plugin.Maui.Audio
- ✅ Download audio files
- ✅ Works on all platforms from single codebase

---

## Using the Web Interface

### Overview

<!-- Screenshot placeholder: Full interface screenshot -->
*The VoiceLabs interface features a modern dark theme with glassmorphism effects.*

### Step-by-Step Guide

#### 1. Enter Your Text

<!-- Screenshot placeholder: Text input area -->

- Type or paste text in the input area
- Maximum **1000 characters** allowed
- Character counter shows remaining space
- Counter turns **orange** when approaching limit

**Tips for better results:**
- Use proper punctuation (periods, commas, question marks)
- Keep sentences 10-30 words for natural pacing
- Avoid excessive special characters

#### 2. Use Sample Texts (Optional)

<!-- Screenshot placeholder: Sample texts section expanded -->

Click **"💡 Sample Texts"** to expand preset examples:

| Sample | Description |
|--------|-------------|
| 👋 Greeting | Friendly welcome message |
| 🦊 Pangram | Classic "quick brown fox" test |
| 🌟 Inspirational | Creative technology quote |
| 🚀 Tech Demo | Future of voice synthesis |
| ☀️ Weather | Weather report example |
| 📰 News | Breaking news format |

Click any sample to populate the text input.

#### 3. Select a Voice

<!-- Screenshot placeholder: Voice dropdown open -->

Choose from **6 voice presets** in the dropdown:

- All voices are English with distinct timbres
- Format: **Name (Gender)**
- Default: **Carter (Male)**

**Available voices:**
- `Carter` — Male, clear American English
- `Emma` — Female voice
- `Frank` — Male voice
- `Grace` — Female voice
- `Davis` — Male voice
- `Mike` — Male voice

#### 4. Generate Speech

<!-- Screenshot placeholder: Generate button states -->

Click the **"🔊 Generate Speech"** button:

- Button shows **spinner** during generation
- Generation takes **1-5 seconds** depending on text length
- Button is disabled while generating

#### 5. Play and Download

<!-- Screenshot placeholder: Audio player section -->

After generation completes:

1. **Audio Player** — Standard HTML5 controls
   - Play/Pause button
   - Progress bar with seeking
   - Volume control

2. **Download Button** — Click **"⬇️ Download WAV"**
   - Files named `voicelabs-YYYYMMDD-HHMMSS.wav`
   - Standard WAV format (24kHz, mono, 16-bit)

### Error Handling

<!-- Screenshot placeholder: Error toast notification -->

If an error occurs, a **toast notification** appears:

| Error | Cause | Solution |
|-------|-------|----------|
| "Failed to generate speech" | Backend unavailable | Check Aspire dashboard |
| "Error: Network error" | Connection lost | Refresh the page |
| "Text is required" | Empty input | Enter some text |

Click the **✕** button to dismiss errors.

---

## Available Voices

VibeVoice uses pre-computed voice preset files (.pt) for each speaker. Voice presets are automatically downloaded from the VibeVoice GitHub repository on first use.

### Voice Presets

| Voice | ID | Gender | Preset File |
|-------|-----|--------|-------------|
| Carter | `en-carter` | Male | `en-Carter_man.pt` |
| Davis | `en-davis` | Male | `en-Davis_man.pt` |
| Emma | `en-emma` | Female | `en-Emma_woman.pt` |
| Frank | `en-frank` | Male | `en-Frank_man.pt` |
| Grace | `en-grace` | Female | `en-Grace_woman.pt` |
| Mike | `en-mike` | Male | `en-Mike_man.pt` |

---

## Tips & Best Practices

### For Natural-Sounding Speech

1. **Use punctuation** — Commas create pauses, periods end sentences
2. **Write naturally** — Conversational text sounds better than formal
3. **Test pronunciation** — Some proper nouns may need phonetic spelling
4. **Pick the right voice** — Try different presets for your content

### For Best Performance

1. **Wait for model load** — First generation is slower (~10-30s)
2. **Keep text reasonable** — 100-500 characters per generation
3. **Use GPU if available** — Significantly faster generation
4. **Monitor Aspire dashboard** — Check service health

### For Development

1. **Use the API directly** — Integrate via `/api/tts` endpoint
2. **Cache generated audio** — Don't regenerate identical text
3. **Handle errors gracefully** — Implement retry logic
4. **Test all voices** — Verify quality for your use case

---

## Troubleshooting

### Python Issues

| Problem | Cause | Solution |
|---------|-------|----------|
| "Module not found" | Virtual env not active | Run `venv\Scripts\activate` |
| "CUDA not available" | No GPU/driver | Works on CPU (slower) |
| Slow first run | Model downloading | Wait for ~1-2 GB download |

### .NET/Aspire Issues

| Problem | Cause | Solution |
|---------|-------|----------|
| "Aspire not found" | Workload missing | `dotnet workload install aspire` |
| "Project not found" | Wrong directory | Run from `VoiceLabs.AppHost` |
| Build errors | Outdated packages | `dotnet restore` |

### Web Interface Issues

| Problem | Cause | Solution |
|---------|-------|----------|
| "Loading voices..." stuck | Backend not ready | Check `/api/health` |
| No audio plays | Browser permissions | Allow audio in browser |
| Download fails | Popup blocker | Allow downloads from site |

### Audio Quality Issues

| Problem | Cause | Solution |
|---------|-------|----------|
| Robotic sound | Text too short | Add more context |
| Unnatural pauses | Missing punctuation | Add commas/periods |
| Wrong pronunciation | Ambiguous words | Try phonetic spelling |
| Garbled output | GPU memory issue | Restart backend |

---

## FAQ

**Q: What TTS model does VibeVoice Labs use?**  
A: Microsoft's VibeVoice-Realtime-0.5B, a 0.5 billion parameter model optimized for real-time speech synthesis with ~300ms latency.

**Q: Can I use my own voice samples?**  
A: The current version uses pre-trained voices. Custom voice cloning requires the full VibeVoice model with fine-tuning capabilities.

**Q: What audio formats are supported?**  
A: Currently only WAV format (24kHz, mono, 16-bit PCM). This provides maximum compatibility and quality.

**Q: Is GPU required?**  
A: No, but highly recommended. CPU generation works but is 5-10x slower. An NVIDIA GPU with CUDA 12.1+ provides best performance.

**Q: How long can the text be?**  
A: Maximum 1000 characters per request. For longer content, split into multiple generations.

**Q: Can I use this in production?**  
A: Yes, with modifications. Add authentication, rate limiting, and proper CORS configuration. See the [Architecture Guide](ARCHITECTURE.md).

**Q: Why is the first generation slow?**  
A: The VibeVoice model (~1-2 GB) downloads and loads on first use. Subsequent generations use the cached model.

**Q: How do I add more voices?**  
A: Modify `tts_service.py` to add entries to `VOICES_REGISTRY` and `VOICE_ID_TO_SPEAKER` mappings.

---

## Getting Help

- **Documentation:** [Getting Started](GETTING_STARTED.md) | [Architecture](ARCHITECTURE.md) | [API Reference](API_REFERENCE.md)
- **Issues:** [GitHub Issues](https://github.com/elbruno/vibevoice-labs/issues)
- **VibeVoice:** [Microsoft VibeVoice](https://github.com/microsoft/VibeVoice)

---

*Last updated: February 2026*
