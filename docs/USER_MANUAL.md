# VibeVoice Labs User Manual

Welcome to **VibeVoice Labs**, a showcase project demonstrating Microsoft's VibeVoice Text-to-Speech (TTS) capabilities using Python and .NET technologies.

---

## Table of Contents

1. [Introduction](#introduction)
2. [Prerequisites](#prerequisites)
3. [Scenario 1: Simple Python Script](#scenario-1-simple-python-script)
4. [Scenario 2: Full-Stack Application](#scenario-2-full-stack-application)
5. [Using the Web Interface](#using-the-web-interface)
6. [Available Voices](#available-voices)
7. [Tips & Best Practices](#tips--best-practices)
8. [Troubleshooting](#troubleshooting)
9. [FAQ](#faq)

---

## Introduction

VibeVoice Labs demonstrates how to integrate Microsoft's VibeVoice-Realtime-0.5B model into applications. The project includes:

- **Scenario 1:** A minimal Python script for learning TTS basics
- **Scenario 2:** A full-stack web application with Blazor UI, FastAPI backend, and .NET Aspire orchestration

### Key Features

| Feature | Description |
|---------|-------------|
| 🔊 Natural Speech | High-quality TTS with ~300ms latency |
| 🌍 Multilingual | 14 voices across 10 languages |
| 🎨 Modern UI | Glassmorphism design with dark theme |
| 📥 Download | Export audio as WAV files |

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
- **Try different voices:** Uncomment the multilingual examples
- **Enable streaming:** Use `generate_stream()` for long texts

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

Choose from **14 voices** in the dropdown:

- Voices are grouped by language
- Format: **Name (Language) - Style**
- Default: **Aria (en-US) - general**

**Popular choices:**
- `Aria` — Clear, professional American English
- `Katja` — Natural German pronunciation
- `Nanami` — Authentic Japanese voice

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

### English Voices

| Voice | ID | Accent | Best For |
|-------|-----|--------|----------|
| Aria | `en-US-Aria` | American | General purpose |
| Guy | `en-US-Guy` | American | Narration |
| Jenny | `en-US-Jenny` | American | Conversational |
| Sonia | `en-GB-Sonia` | British | UK content |
| Natasha | `en-AU-Natasha` | Australian | AU content |

### European Voices

| Voice | ID | Language | Sample Text |
|-------|-----|----------|-------------|
| Katja | `de-DE-Katja` | German | "Guten Tag!" |
| Denise | `fr-FR-Denise` | French | "Bonjour!" |
| Elsa | `it-IT-Elsa` | Italian | "Ciao!" |
| Elvira | `es-ES-Elvira` | Spanish | "¡Hola!" |
| Francisca | `pt-BR-Francisca` | Portuguese | "Olá!" |
| Colette | `nl-NL-Colette` | Dutch | "Hallo!" |
| Paulina | `pl-PL-Paulina` | Polish | "Cześć!" |

### Asian Voices

| Voice | ID | Language | Sample Text |
|-------|-----|----------|-------------|
| Nanami | `ja-JP-Nanami` | Japanese | "こんにちは！" |
| SunHi | `ko-KR-SunHi` | Korean | "안녕하세요!" |

---

## Tips & Best Practices

### For Natural-Sounding Speech

1. **Use punctuation** — Commas create pauses, periods end sentences
2. **Write naturally** — Conversational text sounds better than formal
3. **Test pronunciation** — Some proper nouns may need phonetic spelling
4. **Match voice to language** — Use German voice for German text

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
