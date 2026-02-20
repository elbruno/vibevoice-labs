"""
Quick Backend Test Script
==========================
Quick test to verify backend is running and accessible.
"""

import sys
import time
import urllib.request
import json

def test_health(url="http://localhost:8000"):
    """Test the health endpoint."""
    print(f"Testing backend at: {url}")
    print("-" * 60)
    
    # Test 1: Root endpoint
    print("\n1. Testing root endpoint (/)...")
    try:
        response = urllib.request.urlopen(f"{url}/")
        data = json.loads(response.read())
        print(f"   ✓ Status: {response.getcode()}")
        print(f"   ✓ Message: {data.get('message')}")
        print(f"   ✓ Status: {data.get('status')}")
    except Exception as e:
        print(f"   ✗ Failed: {e}")
        return False
    
    # Test 2: Health endpoint
    print("\n2. Testing health endpoint (/api/health)...")
    try:
        response = urllib.request.urlopen(f"{url}/api/health")
        data = json.loads(response.read())
        print(f"   ✓ Status: {response.getcode()}")
        print(f"   ✓ Service Status: {data.get('status')}")
        print(f"   ✓ TTS Model Loaded: {data.get('model_loaded')}")
        print(f"   ✓ STT Available: {data.get('stt_available')}")
        print(f"   ✓ Chat Available: {data.get('chat_available')}")
        
        if data.get('status') != 'healthy':
            print("\n   ⚠ WARNING: Backend is unhealthy!")
            print("   This usually means the TTS model failed to load.")
            print("   Run 'python diagnose.py' for more details.")
    except Exception as e:
        print(f"   ✗ Failed: {e}")
        return False
    
    # Test 3: Voices endpoint
    print("\n3. Testing voices endpoint (/api/voices)...")
    try:
        response = urllib.request.urlopen(f"{url}/api/voices")
        data = json.loads(response.read())
        voices = data.get('voices', [])
        print(f"   ✓ Status: {response.getcode()}")
        print(f"   ✓ Available voices: {len(voices)}")
        if voices:
            print(f"   ✓ Example: {voices[0].get('name')} ({voices[0].get('id')})")
    except Exception as e:
        print(f"   ✗ Failed: {e}")
        return False
    
    print("\n" + "-" * 60)
    print("✓ All basic tests passed!")
    print("\nNext steps:")
    print("  1. Test WebSocket: Open 'test_ws.html' in a browser")
    print("  2. Or run the full app with Aspire")
    print("-" * 60)
    return True

def wait_for_backend(url="http://localhost:8000", timeout=30):
    """Wait for the backend to become available."""
    print(f"Waiting for backend to start (timeout: {timeout}s)...")
    start_time = time.time()
    
    while time.time() - start_time < timeout:
        try:
            response = urllib.request.urlopen(f"{url}/api/health")
            if response.getcode() == 200:
                print("✓ Backend is ready!")
                return True
        except Exception:
            pass
        time.sleep(2)
        print(".", end="", flush=True)
    
    print("\n✗ Timeout waiting for backend")
    return False

if __name__ == "__main__":
    url = "http://localhost:8000"
    
    if len(sys.argv) > 1:
        url = sys.argv[1]
    
    # First, check if backend is running
    try:
        urllib.request.urlopen(f"{url}/", timeout=2)
        # Backend is running, test it
        test_health(url)
    except:
        print("Backend is not running yet.")
        print("\nStart the backend in another terminal:")
        print("  cd src/scenario-04-meai/backend")
        print("  python -m uvicorn main:app --host 0.0.0.0 --port 8000")
        print("\nOr wait for it to start...")
        
        if wait_for_backend(url):
            test_health(url)
        else:
            print("\nBackend did not start in time.")
            sys.exit(1)
