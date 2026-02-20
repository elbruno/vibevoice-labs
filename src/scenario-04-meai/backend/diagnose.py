"""
Backend Diagnostics Script
===========================
Checks the health and configuration of the backend services.
"""

import sys
import os

def check_python_version():
    """Check Python version."""
    print(f"✓ Python version: {sys.version}")
    major, minor = sys.version_info[:2]
    if major == 3 and minor == 12:
        print("  ✓ Python 3.12 - GOOD")
    elif major == 3 and minor >= 14:
        print("  ⚠ WARNING: Python 3.14+ may have compatibility issues with onnx")
    else:
        print(f"  ℹ Python {major}.{minor}")
    print()

def check_imports():
    """Check if required packages are importable."""
    print("Checking required packages...")
    
    packages = [
        ("fastapi", "FastAPI"),
        ("uvicorn", "Uvicorn"),
        ("websockets", "WebSockets"),
        ("ollama", "Ollama"),
        ("torch", "PyTorch"),
        ("soundfile", "SoundFile"),
        ("numpy", "NumPy"),
    ]
    
    optional_packages = [
        ("vibevoice", "VibeVoice"),
        ("nemo", "NeMo Toolkit"),
        ("faster_whisper", "Faster Whisper"),
    ]
    
    all_ok = True
    for package, name in packages:
        try:
            __import__(package)
            print(f"  ✓ {name}")
        except ImportError as e:
            print(f"  ✗ {name} - NOT FOUND: {e}")
            all_ok = False
    
    print("\nOptional packages:")
    for package, name in optional_packages:
        try:
            __import__(package)
            print(f"  ✓ {name}")
        except ImportError:
            print(f"  ⚠ {name} - not installed (optional)")
    
    print()
    return all_ok

def check_environment():
    """Check environment variables."""
    print("Checking environment variables...")
    
    env_vars = {
        "OLLAMA_MODEL": "Ollama model name (default: llama3.2)",
        "OLLAMA_BASE_URL": "Ollama server URL (default: http://localhost:11434)",
        "PORT": "Backend port (default: 8000)",
        "WHISPER_MODEL_SIZE": "Whisper model size (default: base.en)",
    }
    
    for var, desc in env_vars.items():
        value = os.environ.get(var)
        if value:
            print(f"  ✓ {var} = {value}")
            print(f"    ({desc})")
        else:
            if var in ["OLLAMA_MODEL", "OLLAMA_BASE_URL"]:
                print(f"  ℹ {var} - not set (using default)")
            else:
                print(f"  ℹ {var} - not set (optional)")
            print(f"    ({desc})")
    print()

def check_voices():
    """Check if voice presets exist."""
    print("Checking voice presets...")
    voices_dir = os.path.join(os.path.dirname(__file__), "voices")
    
    if not os.path.exists(voices_dir):
        print(f"  ⚠ Voices directory not found: {voices_dir}")
        print("    Voice presets will be downloaded on first run")
        return
    
    voice_files = [f for f in os.listdir(voices_dir) if f.endswith(".pt")]
    if voice_files:
        print(f"  ✓ Found {len(voice_files)} voice presets:")
        for vf in sorted(voice_files)[:5]:  # Show first 5
            print(f"    - {vf}")
        if len(voice_files) > 5:
            print(f"    ... and {len(voice_files) - 5} more")
    else:
        print("  ⚠ No voice presets found")
        print("    Voice presets will be downloaded on first run")
    print()

def check_ollama():
    """Check Ollama installation and available models."""
    print("Checking Ollama installation...")
    
    try:
        import ollama
        
        # Try to connect to Ollama
        base_url = os.environ.get("OLLAMA_BASE_URL", "http://localhost:11434")
        configured_model = os.environ.get("OLLAMA_MODEL", "llama3.2")
        
        print(f"  Ollama server: {base_url}")
        print(f"  Configured model: {configured_model}")
        
        try:
            if base_url != "http://localhost:11434":
                client = ollama.Client(host=base_url)
            else:
                client = ollama.Client()
            
            # List available models
            models_response = client.list()
            models = models_response.get('models', [])
            
            if models:
                print(f"  ✓ Found {len(models)} Ollama model(s):")
                for model in models[:5]:  # Show first 5
                    model_name = model.get('name', 'unknown')
                    size_gb = model.get('size', 0) / (1024**3)
                    print(f"    - {model_name} ({size_gb:.1f} GB)")
                if len(models) > 5:
                    print(f"    ... and {len(models) - 5} more")
                
                # Check if configured model exists
                model_names = [m['name'].replace(':latest', '') for m in models]
                configured_base = configured_model.replace(':latest', '')
                
                if any(configured_base in name or name in configured_base for name in model_names):
                    print(f"  ✓ Configured model '{configured_model}' is available")
                else:
                    print(f"  ⚠ WARNING: Configured model '{configured_model}' NOT found")
                    print(f"    To pull it: ollama pull {configured_model}")
            else:
                print("  ⚠ No Ollama models found")
                print(f"    To pull the default model: ollama pull {configured_model}")
        
        except Exception as e:
            print(f"  ✗ Cannot connect to Ollama server at {base_url}")
            print(f"    Error: {e}")
            print("\n  To fix:")
            print("    1. Install Ollama: winget install Ollama.Ollama")
            print("    2. Start Ollama (should auto-start on Windows)")
            print(f"    3. Pull a model: ollama pull {configured_model}")
            return False
    
    except ImportError:
        print("  ✗ Ollama Python package not installed")
        print("    Run: pip install ollama")
        return False
    
    print()
    return True

def test_model_loading():
    """Attempt to load the TTS model."""
    print("Testing TTS model loading...")
    try:
        from app.services.tts_service import TTSService
        print("  ℹ Initializing TTS service (this may take a while)...")
        TTSService.initialize()
        if TTSService.is_model_loaded():
            print("  ✓ TTS model loaded successfully!")
        else:
            print("  ✗ TTS model failed to load")
            return False
    except Exception as e:
        print(f"  ✗ TTS model loading failed: {e}")
        return False
    print()
    return True

def test_health_endpoint():
    """Test the health endpoint."""
    print("Testing health endpoint...")
    try:
        from app.api.routes import health_check
        import asyncio
        import os
        
        result = asyncio.run(health_check())
        print(f"  Status: {result.status}")
        print(f"  TTS Model: {'✓' if result.model_loaded else '✗'}")
        print(f"  STT Available: {'✓' if result.stt_available else '✗'}")
        if not result.stt_available:
            print(f"    ⚠ STT is OPTIONAL. To enable, install:")
            print(f"      pip install faster-whisper")
            print(f"      or: pip install nemo_toolkit[asr]")
        
        print(f"  Chat Available: {'✓' if result.chat_available else '✗'}")
        if not result.chat_available:
            print(f"    ⚠ Chat requires Ollama to be running with the configured model")
            print(f"      Configured model: {os.environ.get('OLLAMA_MODEL', 'llama3.2')}")
            print(f"      To fix:")
            print(f"        1. Install Ollama: winget install Ollama.Ollama")
            print(f"        2. Pull model: ollama pull {os.environ.get('OLLAMA_MODEL', 'llama3.2')}")
            print(f"        3. Verify: ollama list")
        
        # Overall assessment
        if result.model_loaded and result.chat_available:
            print(f"\n  ✓ All critical services ready!")
            return True
        elif result.model_loaded:
            print(f"\n  ⚠ TTS ready, but Chat unavailable (set OPENAI_API_KEY)")
            return True
        else:
            print(f"\n  ✗ TTS model not loaded - backend won't work")
            return False
    except Exception as e:
        print(f"  ✗ Health check failed: {e}")
        import traceback
        traceback.print_exc()
        return False
    print()
    return True

def main():
    """Run all diagnostics."""
    print("=" * 60)
    print("VibeVoice Backend Diagnostics")
    print("=" * 60)
    print()
    
    check_python_version()
    imports_ok = check_imports()
    
    if not imports_ok:
        print("⚠ Some required packages are missing. Please install them:")
        print("  pip install -r requirements.txt")
        print()
        return
    
    check_environment()
    check_voices()
    
    # Check Ollama before proceeding
    ollama_ok = check_ollama()
    
    if not ollama_ok:
        print("⚠ Ollama is not ready. Please install and configure Ollama first.")
        print("\nQuick setup:")
        print("  1. Install: winget install Ollama.Ollama")
        print("  2. Pull model: ollama pull llama3.2")
        print("  3. Verify: ollama list")
        print()
        return
    
    # Only test model loading if user confirms (can be slow)
    response = input("Test TTS model loading? This may take 1-2 minutes. (y/N): ")
    if response.lower() in ('y', 'yes'):
        model_ok = test_model_loading()
        health_ok = test_health_endpoint()
        
        print("=" * 60)
        print("Diagnostics Summary")
        print("=" * 60)
        
        if model_ok and health_ok:
            print("✓ Backend is ready to run!")
            print("\nTo start the backend:")
            print("  python -m uvicorn main:app --host 0.0.0.0 --port 8000")
        else:
            print("⚠ Some issues need attention:")
            if not model_ok:
                print("  - TTS model failed to load")
            
            # Check for Ollama availability
            from app.services.chat_service import ChatService
            if not ChatService.is_available():
                print("  - Ollama chat service not available")
                print("\nTo fix:")
                print("  1. Install Ollama: winget install Ollama.Ollama")
                print("  2. Pull model: ollama pull llama3.2")
                print("  3. Verify: ollama list")
        
        print("=" * 60)
    else:
        print()
        print("=" * 60)
        print("Basic diagnostics complete!")
        print("Run with TTS model test to verify full functionality.")
        print("=" * 60)

if __name__ == "__main__":
    main()
