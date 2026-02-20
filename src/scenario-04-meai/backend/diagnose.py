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
        ("openai", "OpenAI"),
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
        "OPENAI_API_KEY": "Required for chat service",
        "PORT": "Backend port (default: 8000)",
        "WHISPER_MODEL_SIZE": "Whisper model size (default: base.en)",
    }
    
    for var, desc in env_vars.items():
        value = os.environ.get(var)
        if value:
            # Mask API keys
            if "KEY" in var or "SECRET" in var:
                display_value = value[:8] + "..." if len(value) > 8 else "***"
            else:
                display_value = value
            print(f"  ✓ {var} = {display_value}")
            print(f"    ({desc})")
        else:
            if var == "OPENAI_API_KEY":
                print(f"  ✗ {var} - NOT SET (required for chat)")
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
            print(f"    ⚠ Chat requires OPENAI_API_KEY environment variable")
            print(f"      Set it with: $env:OPENAI_API_KEY = 'sk-...'")
            print(f"      (Required for conversation functionality)")
        
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
            
            # Check for missing OPENAI_API_KEY
            import os
            if not os.environ.get("OPENAI_API_KEY"):
                print("  - OPENAI_API_KEY not set (required for chat)")
                print("\nTo fix:")
                print("  $env:OPENAI_API_KEY = 'sk-your-api-key-here'")
        
        print("=" * 60)
    else:
        print()
        print("=" * 60)
        print("Basic diagnostics complete!")
        print("Run with TTS model test to verify full functionality.")
        print("=" * 60)

if __name__ == "__main__":
    main()
