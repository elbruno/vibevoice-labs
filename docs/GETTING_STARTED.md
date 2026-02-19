# Getting Started

This guide walks you through setting up and running VibeVoice Labs.

## Prerequisites

### Required Software

| Software | Version | Installation |
|----------|---------|--------------|
| **Python** | 3.11+ | [python.org/downloads](https://python.org/downloads/) |
| **pip** | Latest | Included with Python |
| **.NET SDK** | 10.0+ | [dotnet.microsoft.com](https://dotnet.microsoft.com/download/dotnet/10.0) |
| **Aspire Workload** | Latest | `dotnet workload install aspire` |

### Optional (Recommended)

| Software | Purpose | Installation |
|----------|---------|--------------|
| **CUDA Toolkit** | GPU acceleration | [developer.nvidia.com/cuda-downloads](https://developer.nvidia.com/cuda-downloads) |
| **Git** | Version control | [git-scm.com](https://git-scm.com/) |

### Hardware Requirements

| Component | Minimum | Recommended |
|-----------|---------|-------------|
| **RAM** | 8 GB | 16 GB |
| **Storage** | 5 GB free | 10 GB free (for model caching) |
| **GPU** | None | NVIDIA GPU with CUDA 12.1+ |

---

## Python Environment Setup (One-Time)

All Python scenarios share a single virtual environment at the repository root. Set this up once before running any Python scenario.

### Step 1: Create Virtual Environment

From the repository root:

**Windows (PowerShell):**
```powershell
python -m venv .venv
.\.venv\Scripts\Activate.ps1
```

**Windows (Command Prompt):**
```cmd
python -m venv .venv
.venv\Scripts\activate.bat
```

**Linux/macOS:**
```bash
python -m venv .venv
source .venv/bin/activate
```

### Step 2: Install All Python Dependencies

```bash
pip install -r requirements.txt
```

**Note:** First installation clones the VibeVoice repo from GitHub and downloads the model (~1-2 GB). Voice presets (~4 MB each) are auto-downloaded on first run.

### Step 3: Verify Installation

```bash
python -c "import vibevoice; print('VibeVoice installed successfully!')"
```

> **Tip:** Always activate the virtual environment from the repo root (`.venv\Scripts\Activate.ps1` on Windows or `source .venv/bin/activate` on Linux/macOS) before running any Python scenario.

---

## Scenario 1: Simple Python Script

A minimal script to learn VibeVoice TTS basics, including multilingual support.

> **Prerequisite:** Complete the [Python Environment Setup](#python-environment-setup-one-time) first.

### Step 1: Activate Virtual Environment (if not active)

From the repository root:

**Windows:** `.\.venv\Scripts\Activate.ps1`  
**Linux/macOS:** `source .venv/bin/activate`

### Step 2: Navigate to Scenario Directory

```bash
cd src/scenario-01-simple
```

### Step 3: Run the Script

**Basic English Demo:**
```bash
python main.py
```

**Multilingual Demo (Spanish, French, German, and more):**
```bash
python main_multilingual.py
```

### Expected Output (Basic)

```
Downloading voice presets (first run only)...
  Downloading en-Carter_man.pt...
Loading VibeVoice-Realtime-0.5B model...
Model loaded successfully on cpu!
Generating audio for: 'Hello! Welcome to VibeVoice Labs...'
Saving audio to output.wav...

Audio generated successfully!
   File:     output.wav
   Size:     475.0 KB
   Duration: 10.13s
   Speaker:  Carter
```

### Expected Output (Multilingual)

```
Checking voice presets...
  Downloading sp-Spk0_woman.pt...

Loading VibeVoice-Realtime-0.5B model...
Model loaded successfully on cpu!

Using voice: Spk0_woman (Spanish)

Language: Spanish
Text: ¡Hola! Esta es una demostración de VibeVoice...

Generating audio...
Saving audio to output_sp.wav...

✅ Audio generated successfully!
   File:     output_sp.wav
   Size:     512.0 KB
   Duration: 8.5s
   Language: Spanish
   Voice:    Spk0_woman
```

### Verification

1. Check that `output.wav` (or `output_sp.wav` for multilingual) was created
2. Play the file with your system audio player
3. Verify the audio sounds natural

### Customization

Edit `main.py` to try:
- Different input text (change the `text` variable)
- Different voices: uncomment a `SPEAKER_NAME` line (Carter, Davis, Emma, Frank, Grace, Mike)
- The script auto-downloads voice preset files on first run

Edit `main_multilingual.py` to try:
- Different languages: change `LANGUAGE` (en, sp, de, fr, it, pt, jp, kr, nl, pl)
- Different voices: change `VOICE_INDEX` (0 or 1)
- Custom text: set `CUSTOM_TEXT`
- Generate all languages: uncomment `generate_all_languages()` at the end

---

## Scenario 2: Full-Stack Application

A complete web application with Blazor frontend and FastAPI backend.

> **Prerequisite:** Complete the [Python Environment Setup](#python-environment-setup-one-time) first.

### Step 1: Activate Virtual Environment (if not active)

From the repository root:

**Windows:** `.\.venv\Scripts\Activate.ps1`  
**Linux/macOS:** `source .venv/bin/activate`

### Step 2: Create Virtual Environment Link (First Time Only)

Aspire expects a `.venv` folder in the backend directory. Create a link to the shared root venv:

**Windows (Command Prompt as Administrator OR regular user):**
```cmd
cd src\scenario-02-fullstack\backend
mklink /J .venv ..\..\..\.venv
```

**Windows (PowerShell - requires junction):**
```powershell
cd src/scenario-02-fullstack/backend
cmd /c mklink /J .venv ..\..\..\.venv
```

**Linux/macOS:**
```bash
cd src/scenario-02-fullstack/backend
ln -s ../../../.venv .venv
```

> **Note:** This creates a symbolic link (junction on Windows) so both paths point to the same venv. You only need to do this once.

### Step 3: Install Aspire Workload (if not done)

```bash
dotnet workload install aspire
```

### Step 4: Restore .NET Dependencies

```bash
cd src/scenario-02-fullstack
dotnet restore VoiceLabs.slnx
```

### Step 5: Run with Aspire

```bash
cd VoiceLabs.AppHost
dotnet run
```

### Expected Output

```
info: Aspire.Hosting.DistributedApplication[0]
      Aspire version: 9.2.0
info: Aspire.Hosting.DistributedApplication[0]
      Distributed application starting.
info: Aspire.Hosting.DistributedApplication[0]
      Application started successfully.
```

The Aspire dashboard opens automatically in your browser.

### Step 6: Access the Application

From the Aspire dashboard:

1. Click the **frontend** endpoint link to open the Blazor UI
2. Click the **backend** endpoint link to access the API docs at `/docs`

**Default URLs:**
- Frontend: `http://localhost:{assigned-port}`
- Backend: `http://localhost:5100`
- API Docs: `http://localhost:5100/docs`

### Verification

1. **Health Check:** Visit `http://localhost:5100/api/health`
   ```json
   {"status": "healthy", "model_loaded": true}
   ```

2. **Voices List:** Visit `http://localhost:5100/api/voices`
   ```json
   {"voices": [{"id": "en-carter", "name": "Carter", ...}, ...]}
   ```

3. **Generate Speech:** Use the Blazor UI to:
   - Enter text
   - Select a voice
   - Click "Generate Speech"
   - Play and download the audio

---

## Scenario 3: Simple C# Console App (CSnakes)

A .NET 10 console app that runs VibeVoice TTS using **CSnakes** to embed the Python model directly in the .NET process.

### Run the Console App

```bash
cd src/scenario-03-csharp-simple
dotnet run
```

CSnakes auto-downloads Python and installs dependencies on first run.

### Expected Output

```
🎙️  VibeVoice TTS — C# Console Demo (CSnakes)

🐍 Step 1: Setting up embedded Python environment...

🗣️  Step 2: Voice: Carter
📝 Text: "Hello! Welcome to VibeVoice Labs..."

🎵 Step 3: Generating audio...

✅ Audio generated successfully!
   📁 File:    output.wav
   📏 Size:    475.0 KB
   🗣️  Voice:   Carter
```

---

## Scenario 4: Real-Time Voice Conversation

A full-stack real-time voice conversation app orchestrated by Aspire. Speak into your mic, AI responds with voice.

> **Prerequisite:** Complete the [Python Environment Setup](#python-environment-setup-one-time) first.

### Step 1: Install Backend Dependencies

```bash
cd src/scenario-04-meai/backend
pip install -r requirements.txt
```

### Step 2: Set Your OpenAI API Key

```bash
# Windows PowerShell
$env:OPENAI_API_KEY = "sk-..."

# Linux/macOS
export OPENAI_API_KEY="sk-..."
```

### Step 3: Run with Aspire

```bash
cd src/scenario-04-meai/VoiceLabs.ConversationHost
dotnet run
```

### Expected Output

Open the Aspire dashboard, click the frontend endpoint. You'll see a conversation UI with:
- Push-to-talk button (hold to speak)
- Chat bubbles showing transcriptions and AI responses
- Voice selector dropdown
- Real-time audio playback of AI responses
Saving audio to agent_output.wav...
Playing audio...

---

## Scenario 5: Batch TTS Processing

A Python CLI that converts a folder of `.txt` files to `.wav` using parallel processing.

> **Prerequisite:** Complete the [Python Environment Setup](#python-environment-setup-one-time) first.

### Step 1: Activate Virtual Environment (if not active)

From the repository root:

**Windows:** `.\.venv\Scripts\Activate.ps1`  
**Linux/macOS:** `source .venv/bin/activate`

### Step 2: Navigate to Scenario Directory

```bash
cd src/scenario-05-batch-processing
```

### Step 3: Run the Batch Processor

```bash
# Basic (uses defaults)
python batch_tts.py

# Custom directories
python batch_tts.py --input-dir ./texts --output-dir ./audio

# Use a different default voice
python batch_tts.py --voice carter

# Enable parallel processing (4 files at once)
python batch_tts.py --parallel 4

# All options combined
python batch_tts.py --input-dir ./texts --output-dir ./audio --voice emma --parallel 2
```

### Expected Output

```
Batch TTS Processing
====================

Input:  ./sample-texts
Output: ./output
Voice:  carter (default)
Parallel: 2

Processing files...
  ✓ hello-english.txt → hello-english.wav (45.2 KB)
  ✓ hello-french.txt → hello-french.wav (52.1 KB)
  ✓ story-english.txt → story-english.wav (128.5 KB)

Summary:
  Total files: 3
  Successful: 3
  Failed: 0
  Total duration: 35.2s
```

### YAML Front-Matter

Any `.txt` file can specify a per-file voice using YAML front-matter:

```
---
voice: fr
---
Bonjour! Ceci est un texte en français.
```

---

## Scenario 6: Real-Time Streaming TTS

A Python script demonstrating chunked audio playback for low-latency TTS.

> **Prerequisite:** Complete the [Python Environment Setup](#python-environment-setup-one-time) first.

### Step 1: Activate Virtual Environment (if not active)

From the repository root:

**Windows:** `.\.venv\Scripts\Activate.ps1`  
**Linux/macOS:** `source .venv/bin/activate`

### Step 2: Navigate to Scenario Directory

```bash
cd src/scenario-06-streaming-realtime
```

### Step 3: Run the Streaming Demo

```bash
python stream_tts.py
```

### Expected Output

```
VibeVoice Streaming Real-Time Demo
===================================

Loading model...
Model loaded on CUDA!

Generating audio stream...
Chunk 1 received (44100 samples)
Chunk 2 received (44100 samples)
Chunk 3 received (44100 samples)
...

Performance Summary:
  Time to first chunk: 280 ms
  Total generation time: 4.2s
  Real-time factor: 1.8x (generation faster than playback!)
  Output saved: stream_output.wav
```

---

## Scenario 7: MAUI Cross-Platform App

A .NET MAUI application for Windows, macOS, Android, and iOS with full TTS UI.

### Step 1: Start the Backend

```bash
cd src/scenario-02-fullstack/backend
python -m venv venv
# Activate venv
pip install -r requirements.txt
uvicorn main:app --port 5100
```

### Step 2: Install MAUI Workload

```bash
dotnet workload install maui
```

### Step 3: Run on Your Platform

```bash
cd src/scenario-07-maui-mobile

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

Edit `MauiProgram.cs` to set the backend URL for your environment:

```csharp
var backendUrl = "http://localhost:5100";  // Local dev
// var backendUrl = "http://10.0.2.2:5100";  // Android emulator
// var backendUrl = "http://your-server:5100";  // Production
```

---

## Environment Variables

### Backend Configuration

| Variable | Default | Description |
|----------|---------|-------------|
| `PORT` | `5100` | HTTP port for FastAPI |
| `CUDA_VISIBLE_DEVICES` | (all) | GPU selection for TTS model |

### Frontend Configuration

Service discovery is handled automatically by Aspire. The frontend uses `http://backend` which resolves to the actual backend URL.

---

## Troubleshooting

### Python Issues

#### "Module not found: vibevoice"
```bash
# Ensure virtual environment is activated, then install from GitHub
pip install "vibevoice[streamingtts] @ git+https://github.com/microsoft/VibeVoice.git"
```

#### "No module named 'vibevoice.modular.modeling_vibevoice_streaming_inference'"
The `vibevoice` PyPI package (0.0.1) does NOT include streaming classes. You must install from GitHub:
```bash
pip install "vibevoice[streamingtts] @ git+https://github.com/microsoft/VibeVoice.git"
```

#### "CUDA not available"
VibeVoice works on CPU but is slower. For GPU acceleration:
```bash
# Install PyTorch with CUDA support
pip install torch --index-url https://download.pytorch.org/whl/cu121
```

#### Slow first-time generation
The VibeVoice model downloads on first use (~1-2 GB). Subsequent runs use the cached model from `~/.cache/huggingface/`.

### .NET Issues

#### "Aspire workload not found"
```bash
dotnet workload install aspire
dotnet workload update
```

#### "Project not found: VoiceLabs_Web"
Ensure you're running from the correct directory:
```bash
cd src/scenario-02-fullstack/VoiceLabs.AppHost
dotnet run
```

#### Build errors after .NET update
```bash
dotnet clean
dotnet restore
dotnet build
```

### Connection Issues

#### "Connection refused" on frontend
1. Check that the backend is running in Aspire dashboard
2. Wait for the model to load (check `/api/health`)
3. Verify the backend shows "healthy" status

#### CORS errors in browser console
The backend is configured to allow all origins in development. For production:
```python
# In main.py, specify exact origins:
allow_origins=["https://your-domain.com"]
```

### Audio Issues

#### No audio output
1. Check browser audio permissions
2. Ensure the WAV file is not empty (> 0 bytes)
3. Try a different browser

#### Audio is garbled or distorted
1. Ensure the text is well-formed with punctuation
2. Try a different voice
3. Check if GPU memory is exhausted (use CPU fallback)

---

## Next Steps

- Read the [Architecture Guide](ARCHITECTURE.md) to understand the system design
- Explore the [API Reference](API_REFERENCE.md) for backend integration
- Check the [User Manual](USER_MANUAL.md) for detailed usage instructions

---

## Quick Reference

### All Scenarios at a Glance

| Scenario | Language | Focus | Difficulty | Command |
|----------|----------|-------|-----------|---------|
| 1 | Python | Learning TTS basics | Beginner | `python main.py` |
| 2 | C# + Python | Full-stack web app | Intermediate | `cd VoiceLabs.AppHost && dotnet run` |
| 3 | C# | Direct model (via Python Process) | Beginner | `dotnet run` |
| 4 | C# | AI agent + TTS | Intermediate | `dotnet run` |
| 5 | Python | Batch processing | Intermediate | `python batch_tts.py` |
| 6 | Python | Real-time streaming | Intermediate | `python stream_tts.py` |
| 7 | C# (MAUI) | Cross-platform app | Advanced | `dotnet build -t:Run` |

### Scenario 1 Commands
```bash
cd src/scenario-01-simple
python -m venv venv && venv\Scripts\activate  # Windows
pip install -r requirements.txt
python main.py                    # Basic English demo
python main_multilingual.py       # Multilingual demo (Spanish default)
```

### Scenario 2 Commands
```bash
cd src/scenario-02-fullstack
cd backend && pip install -r requirements.txt && cd ..
dotnet workload install aspire
dotnet restore VoiceLabs.slnx
cd VoiceLabs.AppHost && dotnet run
```

### Scenario 3 Commands
```bash
cd src/scenario-03-csharp-simple
dotnet run
```

### Scenario 4 Commands
```bash
# Set API key first
$env:OPENAI_API_KEY = "sk-..."

# Install backend dependencies
cd src/scenario-04-meai/backend
pip install -r requirements.txt

# Run with Aspire
cd ../VoiceLabs.ConversationHost
dotnet run
```

### Scenario 5 Commands
```bash
cd src/scenario-05-batch-processing
python -m venv venv && .\venv\Scripts\activate
pip install -r requirements.txt
python batch_tts.py --parallel 2
```

### Scenario 6 Commands
```bash
cd src/scenario-06-streaming-realtime
python -m venv venv && .\venv\Scripts\activate
pip install -r requirements.txt
python stream_tts.py
```

### Scenario 7 Commands
```bash
dotnet workload install maui
cd src/scenario-07-maui-mobile
dotnet build -t:Run -f net10.0-windows10.0.19041.0
```
