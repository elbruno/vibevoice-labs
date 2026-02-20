# Quick Fixes for Common Configuration Issues

## Issue: "Chat Available: ✗"

**Cause:** OPENAI_API_KEY environment variable is not set

**Fix:**
```powershell
# Set the environment variable for the current session
$env:OPENAI_API_KEY = "sk-your-actual-api-key-here"

# Verify it's set
echo $env:OPENAI_API_KEY
```

**To make it permanent (optional):**
1. Open Windows Settings → System → About → Advanced system settings
2. Click "Environment Variables"
3. Under "User variables", click "New"
   - Variable name: `OPENAI_API_KEY`
   - Variable value: `sk-your-actual-api-key-here`
4. Click OK

**Get an API key:**
- Sign up at https://platform.openai.com/
- Go to API Keys section
- Create a new key (starts with `sk-`)

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
- ✓ TTS Model loaded
- ✓ OPENAI_API_KEY set
- ✗ STT is optional (skip voice input, use text)

Set the API key and you're good to go!

---

## Full Configuration (Voice + Chat)

For the complete voice conversation experience:
- ✓ TTS Model loaded
- ✓ OPENAI_API_KEY set
- ✓ STT package installed (faster-whisper or nemo)

---

## Quick Test After Fixing

```powershell
# 1. Set the API key
$env:OPENAI_API_KEY = "sk-..."

# 2. (Optional) Install STT if you want voice input
pip install faster-whisper

# 3. Run diagnostics to verify
python diagnose.py

# 4. Start the backend
python -m uvicorn main:app --host 0.0.0.0 --port 8000
```

---

## Still Having Issues?

Run the full diagnostics with model testing:
```powershell
python diagnose.py
# Answer 'y' when prompted to test TTS model loading
```

This will show you exactly what's missing or failing.
