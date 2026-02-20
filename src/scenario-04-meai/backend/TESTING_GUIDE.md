# How to Fix and Test the Backend Errors

This guide helps you resolve the health check and WebSocket connection errors shown in the screenshots.

## Quick Summary of Fixes

The following files have been fixed:
- ✅ `main.py` - Updated to use modern FastAPI `lifespan` instead of deprecated `@app.on_event`
- ✅ `app/api/routes.py` - Enhanced health check with better error handling
- ✅ `diagnose.py` - NEW diagnostic tool to check your environment
- ✅ `test_backend.py` - NEW quick test script
- ✅ `test_ws.html` - NEW WebSocket test client

## Step-by-Step Testing Guide

### Step 1: Check Your Python Version

**CRITICAL**: You MUST use Python 3.12, NOT Python 3.14+

```powershell
python --version
# Should show: Python 3.12.x
```

If you see Python 3.14 or newer:
1. Download Python 3.12.10 from [python.org](https://www.python.org/downloads/)
2. Install it
3. Recreate your virtual environment with Python 3.12

### Step 2: Ensure Virtual Environment is Active

```powershell
cd d:\elbruno\vibevoice-labs\src\scenario-04-meai\backend

# Create venv if it doesn't exist (using Python 3.12)
python -m venv .venv

# Activate it
.\.venv\Scripts\Activate.ps1

# Verify Python version in venv
python --version
```

### Step 3: Install Dependencies

```powershell
pip install -r requirements.txt
```

This may take 5-10 minutes as it downloads the VibeVoice model and dependencies.

### Step 4: Set Environment Variables

The backend needs an OpenAI API key for the chat service:

```powershell
$env:OPENAI_API_KEY = "sk-your-api-key-here"
```

Optional: Set other environment variables
```powershell
$env:PORT = "8000"  # Default port
$env:WHISPER_MODEL_SIZE = "base.en"  # For STT (if using faster-whisper)
```

### Step 5: Run Diagnostics

Before starting the backend, run the diagnostics script to check everything:

```powershell
python diagnose.py
```

This will check:
- ✓ Python version (should be 3.12.x)
- ✓ All required packages are installed
- ✓ Environment variables are set
- ✓ Voice presets exist or can be downloaded
- ✓ (Optional) TTS model can load

**Fix any issues reported before continuing!**

### Step 6: Test Backend Standalone

Start the backend server:

```powershell
python -m uvicorn main:app --host 0.0.0.0 --port 8000 --reload
```

You should see output like:
```
INFO:     Starting application...
INFO:     Initializing TTS service...
INFO:     Downloading voice presets (if needed)...
INFO:     Loading VibeVoice-Realtime-0.5B model...
INFO:     VibeVoice model loaded successfully on cpu
INFO:     TTS service initialized successfully
INFO:     Initializing STT service...
INFO:     STT service initialized successfully
INFO:     Application startup complete
INFO:     Uvicorn running on http://0.0.0.0:8000
```

**Common Issues:**

❌ **"TTS initialization failed: No module named 'vibevoice'"**
- Solution: Run `pip install -r requirements.txt` again

❌ **"Failed to build onnx" or "pybind11 not found"**
- Solution: You're using Python 3.14+. Switch to Python 3.12

❌ **"Out of memory"**
- Solution: Close other applications. The TTS model needs ~2GB RAM

### Step 7: Test the Backend (in another terminal)

While the backend is running, open a new PowerShell terminal:

```powershell
cd d:\elbruno\vibevoice-labs\src\scenario-04-meai\backend

# Activate venv
.\.venv\Scripts\Activate.ps1

# Run quick tests
python test_backend.py
```

This will test:
1. Root endpoint (/)
2. Health endpoint (/api/health)
3. Voices endpoint (/api/voices)

Expected output:
```
Testing backend at: http://localhost:8000
------------------------------------------------------------

1. Testing root endpoint (/)...
   ✓ Status: 200
   ✓ Message: VibeVoice Conversation API is running
   ✓ Status: online

2. Testing health endpoint (/api/health)...
   ✓ Status: 200
   ✓ Service Status: healthy
   ✓ TTS Model Loaded: True
   ✓ STT Available: True
   ✓ Chat Available: True

3. Testing voices endpoint (/api/voices)...
   ✓ Status: 200
   ✓ Available voices: 6
   ✓ Example: Carter (en-carter)

✓ All basic tests passed!
```

### Step 8: Test WebSocket Connection

1. Keep the backend running from Step 6
2. Open `test_ws.html` in a web browser (Chrome, Edge, or Firefox)
3. The URL should be pre-filled as: `ws://localhost:8000/ws/conversation`
4. Click **Connect**
5. You should see: `[HH:MM:SS] Connected successfully!`

**Test sending a message:**
1. Type "hello" in the message input box
2. Click **Send**
3. Watch the log for responses

### Step 9: Run with Aspire

Once standalone testing works, you can run with Aspire:

```powershell
cd d:\elbruno\vibevoice-labs\src\scenario-04-meai

# Make sure backend dependencies are installed in backend/.venv
# Set environment variable for Aspire
$env:OPENAI_API_KEY = "sk-your-api-key-here"

# Run Aspire
dotnet run --project VoiceLabs.ConversationHost
```

Open the Aspire dashboard (usually http://localhost:15888 or as shown in terminal)

Check:
- ✅ Backend status should be **healthy** (green)
- ✅ Frontend status should be **healthy** (green)
- ✅ No connection errors in logs

### Step 10: Test the Full App

1. In the Aspire dashboard, click on the **frontend** endpoint
2. The web app should open
3. Click **Test Connection** button
4. Check the Debug Console for logs
5. Try the **Hold to Talk** button to test the full conversation flow

## Troubleshooting Common Errors

### Error: "Health check unhealthy"

**Symptoms:** Aspire dashboard shows red status

**Solutions:**
1. Check backend logs for initialization errors
2. Run `python diagnose.py` to identify issues
3. Verify TTS model loaded successfully (check logs)
4. Ensure Python 3.12 is being used

### Error: "Failed to connect" (WebSocket)

**Symptoms:** Frontend shows "Disconnected" or "Failed to connect"

**Solutions:**
1. Verify backend is running: `curl http://localhost:8000/api/health`
2. Test WebSocket with `test_ws.html`
3. Check for firewall blocking WebSocket connections
4. Check backend logs for WebSocket errors

### Error: "Chat service unavailable"

**Symptoms:** Health check shows `chat_available: false`

**Solutions:**
1. Set OPENAI_API_KEY environment variable
2. Verify API key is valid
3. Check internet connection

### Error: "STT service unavailable"

**Symptoms:** Health check shows `stt_available: false`

**Solutions:**
This is optional. The app can work without STT if you're only testing TTS.
To enable STT, install one of:
- `pip install nemo_toolkit[asr]` (NVIDIA Parakeet)
- `pip install faster-whisper` (faster-whisper)

## Understanding the Fixes

### What was broken?

1. **Deprecated FastAPI startup**: The old `@app.on_event("startup")` is deprecated and may not execute in newer FastAPI versions
2. **Missing error handling**: If services failed to initialize, the app would crash
3. **No diagnostics**: Hard to identify what's wrong

### What was fixed?

1. ✅ **Modern lifespan manager**: Uses `@asynccontextmanager` for proper startup/shutdown
2. ✅ **Graceful degradation**: Services can fail to initialize without crashing the app
3. ✅ **Better logging**: All initialization steps are logged with details
4. ✅ **Diagnostic tools**: `diagnose.py` and `test_backend.py` for troubleshooting
5. ✅ **WebSocket test client**: `test_ws.html` for testing WebSocket without frontend

## Files Changed

- `main.py` - Updated startup lifecycle, added error handling
- `app/api/routes.py` - Enhanced health check endpoint
- `diagnose.py` - NEW: Environment diagnostics
- `test_backend.py` - NEW: Quick backend tests
- `test_ws.html` - NEW: WebSocket test client
- `FIXES_SUMMARY.md` - Detailed fix documentation

## Next Steps

Once everything is working:

1. ✅ Backend tests pass (`python test_backend.py`)
2. ✅ WebSocket connects (`test_ws.html`)
3. ✅ Aspire health checks are green
4. ✅ Frontend connects and shows debug logs
5. ✅ Full conversation flow works (record → transcribe → chat → TTS → play)

Then you're ready to develop and extend the application!

## Need Help?

If you're still seeing errors:

1. Run `python diagnose.py` and share the output
2. Check the backend logs when starting with Aspire
3. Use `test_ws.html` to isolate WebSocket issues
4. Verify Python version is 3.12.x (not 3.14+)
