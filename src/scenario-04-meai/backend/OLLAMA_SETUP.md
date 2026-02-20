# Ollama Setup Guide for VibeVoice Backend

This guide explains how to set up Ollama for local LLM inference with the VibeVoice conversation backend.

## What is Ollama?

Ollama is a local LLM runtime that allows you to run large language models on your own machine. Benefits:

✅ **No API keys needed** - Everything runs locally  
✅ **Privacy** - Your data never leaves your machine  
✅ **Offline capable** - Works without internet  
✅ **Free** - No usage charges  
✅ **Fast** - Local inference with no network latency  

## Installation

### Windows (Recommended)

**Option 1: Using winget**
```powershell
winget install Ollama.Ollama
```

**Option 2: Manual download**
1. Visit https://ollama.ai/download
2. Download the Windows installer
3. Run the installer
4. Ollama will start automatically

### Verify Installation

```powershell
# Check if Ollama is running
ollama list

# Should show an empty list or any models you've already installed
```

## Installing Models

### Default Model: llama3.2

The backend is configured to use `llama3.2` by default. To install it:

```powershell
ollama pull llama3.2
```

This will download ~2GB of data. Wait for it to complete.

### Verify Model Installation

```powershell
ollama list
```

You should see:
```
NAME            ID              SIZE    MODIFIED
llama3.2:latest abc123def       2.0 GB  X minutes ago
```

### Alternative Models

You can use different models by setting the `OLLAMA_MODEL` environment variable:

**Smaller/Faster Models:**
```powershell
# Phi-3: Smaller, faster (2.3GB)
ollama pull phi3
$env:OLLAMA_MODEL = "phi3"

# Gemma 2B: Very small, very fast (1.4GB)
ollama pull gemma:2b
$env:OLLAMA_MODEL = "gemma:2b"
```

**Larger/More Capable Models:**
```powershell
# Llama 3.1: More capable (4.7GB)
ollama pull llama3.1
$env:OLLAMA_MODEL = "llama3.1"

# Mistral: Fast and capable (4.1GB)
ollama pull mistral
$env:OLLAMA_MODEL = "mistral"
```

See all available models at: https://ollama.ai/library

## Configuration

### Environment Variables

The backend supports these environment variables:

| Variable | Default | Description |
|----------|---------|-------------|
| `OLLAMA_MODEL` | `llama3.2` | Model name to use |
| `OLLAMA_BASE_URL` | `http://localhost:11434` | Ollama server URL |

**Set for current session:**
```powershell
$env:OLLAMA_MODEL = "llama3.2"
$env:OLLAMA_BASE_URL = "http://localhost:11434"
```

### Aspire Configuration

When running with Aspire, configure Ollama in `appsettings.json`:

**File:** `VoiceLabs.ConversationHost/appsettings.json`
```json
{
  "Backend": {
    "OllamaModel": "llama3.2",
    "OllamaBaseUrl": "http://localhost:11434"
  }
}
```

**Development overrides:** `appsettings.Development.json`
```json
{
  "Backend": {
    "OllamaModel": "phi3",  // Use faster model for development
    "OllamaBaseUrl": "http://localhost:11434"
  }
}
```

## Testing Ollama

### 1. Basic Connectivity Test

```powershell
# Test if Ollama server is responding
curl http://localhost:11434/api/tags

# Should return JSON with list of models
```

### 2. Test Model Inference

```powershell
# Chat with your model directly
ollama run llama3.2

# Type a message and press Enter
# Type /bye to exit
```

### 3. Test with Backend Diagnostics

```powershell
cd src\scenario-04-meai\backend
python diagnose.py
```

The diagnostics will:
- ✓ Check if Ollama is installed and running
- ✓ List available models
- ✓ Verify your configured model exists
- ✓ Test chat service connectivity

## Troubleshooting

### Issue: "Cannot connect to Ollama server"

**Symptoms:** 
- `diagnose.py` shows "Cannot connect to Ollama server"
- Chat service unavailable

**Solutions:**

1. **Check if Ollama is running:**
   ```powershell
   # On Windows, Ollama should auto-start
   # Check with:
   ollama list
   ```

2. **Restart Ollama (if needed):**
   - Open Task Manager
   - Find "Ollama" process
   - End it
   - Run `ollama serve` in a terminal
   - Or just restart your computer

3. **Check firewall:**
   - Ensure port 11434 is not blocked
   - Add Ollama to firewall exceptions

### Issue: "Model not found"

**Symptoms:**
- Diagnostics shows "Configured model 'llama3.2' NOT found"

**Solution:**
```powershell
# Pull the model
ollama pull llama3.2

# Verify it's available
ollama list
```

### Issue: "Out of memory" or slow performance

**Symptoms:**
- Model takes forever to respond
- System becomes sluggish

**Solutions:**

1. **Use a smaller model:**
   ```powershell
   ollama pull phi3
   $env:OLLAMA_MODEL = "phi3"
   ```

2. **Close other applications** to free up RAM

3. **Check system requirements:**
   - Minimum: 8GB RAM (for 2B-3B models)
   - Recommended: 16GB RAM (for 7B+ models)

### Issue: "Model downloads are slow"

**Solution:**
- Be patient - models are 1-5GB
- Download happens once
- Use a smaller model if needed

## Advanced Configuration

### Running Ollama on a Different Port

```powershell
# Set custom port
$env:OLLAMA_HOST = "0.0.0.0:11435"

# Start Ollama
ollama serve

# Update backend config
$env:OLLAMA_BASE_URL = "http://localhost:11435"
```

### Using Remote Ollama Server

```powershell
# If Ollama is running on another machine
$env:OLLAMA_BASE_URL = "http://192.168.1.100:11434"

# Or in Aspire appsettings.json:
# "OllamaBaseUrl": "http://192.168.1.100:11434"
```

### GPU Acceleration

Ollama automatically uses GPU if available (NVIDIA, AMD, or Apple Silicon).

To verify GPU usage:
```powershell
# Run a model and check GPU usage in Task Manager
ollama run llama3.2
```

## Best Practices

1. **Start with llama3.2** - Good balance of quality and speed
2. **Pull models in advance** - Don't wait during development
3. **Keep Ollama updated** - Check for updates regularly
4. **Monitor resource usage** - Larger models need more RAM
5. **Test locally first** - Verify Ollama works before integrating

## Quick Reference

```powershell
# Install Ollama
winget install Ollama.Ollama

# Pull default model
ollama pull llama3.2

# List installed models
ollama list

# Test a model
ollama run llama3.2

# Remove a model (free up space)
ollama rm llama3.2

# Check Ollama version
ollama --version

# View Ollama help
ollama --help
```

## Next Steps

Once Ollama is set up:

1. ✅ Run `python diagnose.py` to verify configuration
2. ✅ Start the backend: `python -m uvicorn main:app --host 0.0.0.0 --port 8000`
3. ✅ Test with `python test_backend.py`
4. ✅ Run with Aspire: `dotnet run --project VoiceLabs.ConversationHost`

Happy coding! 🚀
