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

## Scenario 1: Simple Python Script

A minimal script to learn VibeVoice TTS basics.

### Step 1: Navigate to Scenario Directory

```bash
cd src/scenario-01-simple
```

### Step 2: Create Virtual Environment

**Windows (PowerShell):**
```powershell
python -m venv venv
.\venv\Scripts\Activate.ps1
```

**Windows (Command Prompt):**
```cmd
python -m venv venv
venv\Scripts\activate.bat
```

**Linux/macOS:**
```bash
python -m venv venv
source venv/bin/activate
```

### Step 3: Install Dependencies

```bash
pip install -r requirements.txt
```

**Note:** First installation clones the VibeVoice repo from GitHub and downloads the model (~1-2 GB). Voice presets (~4 MB each) are auto-downloaded on first run.

### Step 4: Run the Script

```bash
python main.py
```

### Expected Output

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

### Verification

1. Check that `output.wav` was created
2. Play the file with your system audio player
3. Verify the audio sounds natural

### Customization

Edit `main.py` to try:
- Different input text (change the `text` variable)
- Different voices: uncomment a `SPEAKER_NAME` line (Carter, Davis, Emma, Frank, Grace, Mike)
- The script auto-downloads voice preset files on first run

---

## Scenario 2: Full-Stack Application

A complete web application with Blazor frontend and FastAPI backend.

### Step 1: Navigate to Scenario Directory

```bash
cd src/scenario-02-fullstack
```

### Step 2: Install Python Backend Dependencies

```bash
cd backend
python -m venv venv

# Windows
.\venv\Scripts\Activate.ps1

# Linux/macOS
source venv/bin/activate

pip install -r requirements.txt
cd ..
```

### Step 3: Install Aspire Workload (if not done)

```bash
dotnet workload install aspire
```

### Step 4: Restore .NET Dependencies

```bash
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

### Scenario 1 Commands
```bash
cd src/scenario-01-simple
python -m venv venv && venv\Scripts\activate  # Windows
pip install -r requirements.txt
python main.py
```

### Scenario 2 Commands
```bash
cd src/scenario-02-fullstack
cd backend && pip install -r requirements.txt && cd ..
dotnet workload install aspire
dotnet restore VoiceLabs.slnx
cd VoiceLabs.AppHost && dotnet run
```
