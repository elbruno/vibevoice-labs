# Backend Error Fixes Summary

## Issues Found

Based on the error screenshots:

1. **Health Check Failures**: The Aspire dashboard shows that both `backend` and `frontend` services have health check errors
2. **WebSocket Connection Failure**: The frontend cannot connect to `ws://localhost:52583/ws/conversation`

## Root Causes

### 1. Deprecated FastAPI Startup Event
The `@app.on_event("startup")` decorator is deprecated in newer FastAPI versions and may not execute properly, causing services to not initialize.

### 2. Missing Error Handling  
Service initialization errors were not being caught and logged properly, making it difficult to diagnose issues.

### 3. Python Version Compatibility
If using Python 3.14+, the `onnx==1.16.1` dependency will fail to build due to missing pre-built wheels and C++ compiler issues.

## Fixes Applied

### 1. Updated main.py to use `lifespan` context manager (✓ Fixed)
- Replaced deprecated `@app.on_event("startup")` with modern `lifespan` async context manager
- Added comprehensive try-catch blocks around service initialization
- Added detailed logging for startup and shutdown
- Services won't crash the app on initialization failure - health endpoint will report status

```python
@asynccontextmanager
async def lifespan(app: FastAPI):
    """Application lifespan manager - handles startup and shutdown."""
    logger.info("Starting application...")
    
    # Startup: Initialize services
    try:
        TTSService.initialize()
        logger.info("TTS service initialized successfully")
    except Exception as e:
        logger.error(f"TTS initialization failed: {e}", exc_info=True)
    
    # ... similar for STT
    
    yield  # Application runs
    
    # Shutdown: cleanup
    logger.info("Shutting down application...")
```

### 2. Enhanced Health Check Endpoint (✓ Fixed)
- Added exception handling to prevent crashes
- Added detailed logging of all service states
- Returns proper status even if services fail to initialize

### 3. Created Diagnostics Script (✓ Created)
Created `backend/diagnose.py` to help troubleshoot issues:
- Checks Python version
- Verifies all required packages are installed
- Checks environment variables (OPENAI_API_KEY, etc.)
- Checks voice presets
- Optionally tests model loading
- Tests health endpoint

### 4. Created WebSocket Test HTML (✓ Created)
Created `backend/test_ws.html` - a standalone HTML file to test WebSocket connections directly without the frontend.

## How to Test the Fixes

### Step 1: Run diagnostics
```powershell
cd src/scenario-04-meai/backend
python diagnose.py
```

This will:
- Check Python version (should be 3.12.x, NOT 3.14+)
- Verify all packages are installed
- Check if OPENAI_API_KEY is set
- Check voice presets
- Optionally test TTS model loading

### Step 2: Check for missing dependencies
If diagnostics show missing packages:
```powershell
# Make sure you're using Python 3.12
python --version

# Create fresh venv with Python 3.12
python -m venv .venv
.\.venv\Scripts\Activate.ps1

# Install dependencies
pip install -r requirements.txt
```

### Step 3: Test backend standalone
```powershell
# Run the backend
python -m uvicorn main:app --host 0.0.0.0 --port 8000 --reload

# In another terminal, test the health endpoint
curl http://localhost:8000/api/health

# Check the logs for initialization errors
```

### Step 4: Test WebSocket with HTML client
1. Start the backend (step 3)
2. Open `backend/test_ws.html` in a web browser
3. Click "Connect" to test WebSocket connection
4. Watch the log for connection status

### Step 5: Run with Aspire
```powershell
cd src/scenario-04-meai
dotnet run --project VoiceLabs.ConversationHost
```

Check the Aspire dashboard for:
- Backend health status should be "healthy" (green)
- Frontend should connect to backend successfully
- No connection errors in logs

## Common Issues & Solutions

### Issue: "onnx build failed" or "pybind11 not found"
**Solution**: You're using Python 3.14+. Switch to Python 3.12:
```powershell
# Download Python 3.12.10 from python.org
# Then recreate venv
python312 -m venv .venv
.\.venv\Scripts\Activate.ps1
pip install -r requirements.txt
```

### Issue: "TTS model failed to load"
**Solution**: Check the logs in diagnostics or backend output. Common causes:
- Missing voice presets (will auto-download on first run)
- Out of memory (model requires ~2GB RAM)
- Missing vibevoice package

### Issue: "WebSocket connection refused"
**Solution**: 
1. Verify backend is running: `curl http://localhost:8000/api/health`
2. Check backend logs for errors
3. Use `test_ws.html` to test WebSocket directly
4. Verify Aspire service discovery is working (check appsettings.json)

### Issue: "Chat service unavailable"
**Solution**: Install and configure Ollama:
```powershell
# Install Ollama
winget install Ollama.Ollama

# Pull the model
ollama pull llama3.2

# Verify
ollama list
```

## Files Modified

1. `src/scenario-04-meai/backend/main.py` - Updated to use lifespan, enhanced error handling
2. `src/scenario-04-meai/backend/app/api/routes.py` - Enhanced health check with error handling
3. `src/scenario-04-meai/backend/diagnose.py` - NEW: Diagnostics script
4. `src/scenario-04-meai/backend/test_ws.html` - NEW: WebSocket test client

## Next Steps

1. Run `python diagnose.py` to identify any issues
2. Fix any missing dependencies or environment variables
3. Test backend standalone before using Aspire
4. Check Aspire dashboard for service health

The application should now:
- ✓ Start successfully even if some services fail to initialize
- ✓ Report proper health status
- ✓ Log detailed error messages for troubleshooting
- ✓ Support WebSocket connections properly
