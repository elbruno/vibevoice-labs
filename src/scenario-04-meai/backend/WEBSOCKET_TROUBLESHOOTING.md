# WebSocket Connection Troubleshooting Guide

## Summary of Fixes

The following changes were made to fix WebSocket connection issues:

### 1. **ChatService Initialization** (✅ Fixed)
**Problem:** ChatService would crash on initialization if Ollama wasn't available  
**Fix:** Deferred client initialization with graceful error handling
- Client creation now happens lazily
- Provides clear error messages if Ollama unavailable
- Won't crash WebSocket connection on startup

### 2. **Enhanced Logging** (✅ Added)
**Problem:** Hard to debug WebSocket connection failures  
**Fix:** Added comprehensive logging throughout the WebSocket flow
- Logs connection attempts with client IP
- Logs all message types (binary/text)
- Logs service initialization errors
- Logs disconnections with reasons

### 3. **Test WebSocket Endpoint** (✅ Added)
**Problem:** Couldn't isolate if issue was WebSocket or services  
**Fix:** New `/ws/test` endpoint that echoes messages without loading services
- Simple echo functionality
- No dependencies on TTS/STT/Chat
- Perfect for testing WebSocket connectivity

### 4. **Warmup Endpoint** (✅ Added)
**Problem:** First request slow due to model loading  
**Fix:** New `/api/warmup` endpoint to pre-load all services
- Tests TTS by generating short audio
- Tests Chat by sending test message
- Returns timing information
- Can be called on startup

## Testing Strategy

### Step 1: Test Simple WebSocket Connection

**Use the test endpoint to verify WebSocket works:**

```html
<!-- Open test_ws.html in browser -->
1. Click "Use /ws/test"
2. Click "Connect"
3. Type "hello" and click "Send"
4. Should see echo response
```

Expected log:
```
[HH:MM:SS] Connecting to ws://localhost:8000/ws/test...
[HH:MM:SS] Connected successfully!
[HH:MM:SS] Received: {"type": "connected", "message": "WebSocket test endpoint ready"}
[HH:MM:SS] Sent: {"type": "text", "text": "hello"}
[HH:MM:SS] Received: {"type": "echo", "data": {"type": "text", "text": "hello"}}
```

**If this fails:** WebSocket itself isn't working (firewall, port, etc.)  
**If this works:** Problem is with services, not WebSocket

### Step 2: Check Backend Logs

Start backend and watch logs:

```powershell
python -m uvicorn main:app --host 0.0.0.0 --port 8000 --reload
```

When WebSocket connects, you should see:
```
INFO:  WebSocket connection attempt from 127.0.0.1
INFO:  WebSocket connection accepted from 127.0.0.1
INFO:  Chat service initialized for WebSocket session from 127.0.0.1
```

If you see errors here, they'll indicate what's failing.

### Step 3: Call Warmup Endpoint

Before testing full conversation, warm up services:

```powershell
curl -X POST http://localhost:8000/api/warmup
```

Expected response:
```json
{
  "status": "ok",
  "services": {
    "tts": {"status": "ready", "time_ms": 234.5},
    "stt": {"status": "not_available"},
    "chat": {"status": "ready", "time_ms": 1523.2, "test_response_length": 45}
  },
  "total_time_ms": 1825.7
}
```

This confirms services are ready before WebSocket connection.

### Step 4: Test Full Conversation WebSocket

Once warmup succeeds:

```html
<!-- In test_ws.html -->
1. Click "Use /ws/conversation"
2. Click "Connect"
3. Send test message
```

## Common Issues & Solutions

### Issue 1: "Connection refused" on /ws/test

**Symptoms:**
- Can't connect to ws://localhost:8000/ws/test
- Browser console shows connection error

**Causes:**
- Backend not running
- Wrong port
- Firewall blocking

**Solutions:**
```powershell
# Verify backend is running
curl http://localhost:8000/api/health

# Check if port is in use
netstat -ano | findstr :8000

# Temporarily disable firewall to test
# Or add exception for port 8000
```

### Issue 2: Connects to /ws/test but not /ws/conversation

**Symptoms:**
- /ws/test works fine
- /ws/conversation fails or closes immediately

**Causes:**
- Services (TTS, Chat, STT) failing to initialize
- Ollama not running

**Solutions:**
```powershell
# Check health
curl http://localhost:8000/api/health

# Should show:
# "chat_available": true

# If chat_available is false:
ollama list
# Ensure llama3.2 is installed

# Try warmup
curl -X POST http://localhost:8000/api/warmup

# Check backend logs for specific errors
```

### Issue 3: Connects but errors on first message

**Symptoms:**
- WebSocket connects successfully
- Error when sending first message
- Backend logs show service errors

**Causes:**
- TTS model not loaded
- Ollama model not available
- Out of memory

**Solutions:**
```powershell
# Warmup first
curl -X POST http://localhost:8000/api/warmup

# Check what failed in warmup response
# TTS error? Check memory, reinstall vibevoice
# Chat error? Check Ollama running, model exists
```

### Issue 4: Frontend can't connect (Aspire scenario)

**Symptoms:**
- Backend works standalone
- Fails when run via Aspire
- Frontend shows "Disconnected"

**Causes:**
- Service discovery resolving to wrong URL
- HTTPS/WSS vs HTTP/WS mismatch
- Frontend using cached wrong URL

**Solutions:**

Check what URL Aspire assigned:
``powershell
# In Aspire dashboard, check backend endpoint
# Should be http://localhost:XXXXX
```

Update frontend to use correct URL (or fix service discovery)

Check appsettings.json for correct Ollama URL:
```json
{
  "Backend": {
    "OllamaModel": "llama3.2",
    "OllamaBaseUrl": "http://localhost:11434"  // For local dev
  }
}
```

### Issue 5: Slow first connection

**Symptoms:**
- First WebSocket connection times out
- Subsequent connections work fine

**Cause:**
- Models loading on first request

**Solution:**
Call warmup endpoint after startup:

```powershell
# Add to startup script
curl -X POST http://localhost:8000/api/warmup

# Or call from frontend on page load
fetch('/api/warmup', { method: 'POST' });
```

## Debugging Checklist

When WebSocket fails, check in order:

1. ✅ **Is backend running?**
   ```powershell
   curl http://localhost:8000/api/health
   ```

2. ✅ **Can you connect to /ws/test?**
   - Use test_ws.html with /ws/test endpoint
   - If NO: WebSocket itself broken (firewall/port)
   - If YES: Services are the issue

3. ✅ **Are services ready?**
   ```powershell
   curl -X POST http://localhost:8000/api/warmup
   # Check which services failed
   ```

4. ✅ **Is Ollama running?**
   ```powershell
   ollama list
   # Should show llama3.2
   ```

5. ✅ **Check backend logs**
   - Look for "WebSocket connection attempt"
   - Look for "Failed to initialize chat service"
   - Look for any exceptions

6. ✅ **Check frontend browser console**
   - WebSocket connection errors
   - Network tab shows WebSocket upgrade

## New Endpoints Reference

### GET /api/health
Returns service status (unchanged)

### POST /api/warmup
Pre-loads all services and returns timing:
```json
{
  "status": "ok",
  "services": {
    "tts": {"status": "ready", "time_ms": 234.5},
    "chat": {"status": "ready", "time_ms": 1523.2}
  },
  "total_time_ms": 1825.7
}
```

### WS /ws/test
Simple echo WebSocket (no services required):
- Connects immediately
- Echoes back all messages
- Perfect for testing WebSocket connectivity

### WS /ws/conversation
Full conversation WebSocket (requires services):
- Requires TTS, Chat, and optionally STT
- Call `/api/warmup` first for best performance

## Performance Tips

1. **Always warmup after startup:**
   ```javascript
   // In frontend, on page load:
   await fetch('/api/warmup', { method: 'POST' });
   ```

2. **Use /ws/test for connectivity checks:**
   - Quick, lightweight
   - No model loading
   - Isolates WebSocket from services

3. **Check /api/health before connecting:**
   - Verify services are ready
   - Show user if services unavailable

4. **Enable debug logging:**
   ```powershell
   # Set log level to DEBUG
   $env:LOG_LEVEL = "DEBUG"
   python -m uvicorn main:app --log-level debug
   ```

## Summary

The WebSocket implementation now has:
- ✅ Better error handling (won't crash on service failures)
- ✅ Enhanced logging (easier to debug)
- ✅ Test endpoint (isolate connectivity issues)
- ✅ Warmup endpoint (pre-load models)
- ✅ Graceful degradation (services can fail individually)

Follow the testing strategy above to identify and fix any remaining issues!
