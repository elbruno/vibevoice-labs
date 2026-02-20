# Quick Fixes for Common Configuration Issues

## Issue: "Chat Available: ✗"

**Cause:** Ollama is not running or the configured model is not available

**Fix - Install and Run Ollama:**

**Step 1: Install Ollama**
```powershell
# Install Ollama using winget
winget install Ollama.Ollama

# Or download from https://ollama.ai/
```

**Step 2: Pull the default model**
```powershell
# Pull llama3.2 (default model)
ollama pull llama3.2

# Verify it's available
ollama list
```

**Step 3: Verify Ollama is running**
```powershell
# Ollama runs automatically on Windows
# Test it with:
ollama list

# Or check if the server is responding:
curl http://localhost:11434/api/tags
```

**To use a different model:**
```powershell
# Set environment variable before starting backend
$env:OLLAMA_MODEL = "llama3.1"

# Or configure in Aspire appsettings.json:
# "Backend": { "OllamaModel": "llama3.1" }

# Then pull the model:
ollama pull llama3.1
```

**Popular Ollama models:**
- `llama3.2` - Default, good balance (2GB)
- `llama3.1` - Larger, more capable (4.7GB)
- `phi3` - Smaller, faster (2.3GB)
- `mistral` - Fast and capable (4.1GB)

---

## Issue: "STT Available: ✗"

**Cause:** Neither NeMo nor faster-whisper is installed

**This is OPTIONAL** - The app can work without STT if you only want to test TTS.

**Fix Option 1 - Install faster-whisper (Recommended, faster & lighter):**
```powershell
pip install faster-whisper
```

**Fix Option 2 - Install NeMo Toolkit (More accurate, but larger):**
```powershell
pip install nemo_toolkit[asr]
```

**Note:** You only need ONE of these, not both.

---

## Verify the Fixes

After setting OPENAI_API_KEY or installing STT packages, run diagnostics again:

```powershell
python diagnose.py
```

Expected output with fixes applied:
```
Testing health endpoint...
  Status: healthy
  TTS Model: ✓
  STT Available: ✓
  Chat Available: ✓

  ✓ All critical services ready!
```

---

## Minimal Configuration (Chat Only)

If you just want to test the conversation flow (without voice input), you only need:
- ✓ Ollama installed and running
- ✓ llama3.2 model pulled (or configured model)
- ✓ TTS Model loaded
- ✗ STT is optional (skip voice input, use text)

Install Ollama and pull the model, then you're good to go!

---

## Full Configuration (Voice + Chat)

For the complete voice conversation experience:
- ✓ Ollama installed and running with llama3.2
- ✓ TTS Model loaded
- ✓ STT package installed (faster-whisper or nemo)

---

## Quick Test After Fixing

```powershell
# 1. Install Ollama and pull model
winget install Ollama.Ollama
ollama pull llama3.2

# 2. Verify Ollama is working
ollama list

# 3. (Optional) Install STT if you want voice input
pip install faster-whisper

# 4. Run diagnostics to verify
python diagnose.py

# 5. Start the backend
python -m uvicorn main:app --host 0.0.0.0 --port 8000
```

---

## Still Having Issues?

Run the full diagnostics with model testing:
```powershell
python diagnose.py
# Answer 'y' when prompted to test TTS model loading
```

This will show you exactly what's missing or failing, including Ollama connectivity and model availability.
