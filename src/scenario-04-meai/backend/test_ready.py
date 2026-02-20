"""
Test Script for /api/ready Endpoint
====================================
Tests the readiness endpoint and demonstrates polling pattern.
"""

import sys
import time
import json
import urllib.request
import urllib.error


def print_ready_state(state_dict):
    """Pretty print the ready state."""
    print("\n" + "=" * 60)
    print(f"Ready: {'✓ YES' if state_dict['ready'] else '✗ NO'}")
    print(f"State: {state_dict['state']}")
    print(f"Progress: {state_dict['progress']}%")
    print("-" * 60)
    
    print("Services:")
    for service_name, service_info in state_dict.get('services', {}).items():
        status_icon = "✓" if service_info.get('ready') else "✗"
        status = service_info.get('status', 'unknown')
        print(f"  {status_icon} {service_name}: {status}", end="")
        
        if service_info.get('warmup_time_ms'):
            print(f" ({service_info['warmup_time_ms']:.2f}ms)", end="")
        if service_info.get('error'):
            print(f" - Error: {service_info['error']}", end="")
        if service_info.get('model'):
            print(f" - Model: {service_info['model']}", end="")
        print()
    
    if state_dict.get('startup_time_ms'):
        print(f"\nTotal startup time: {state_dict['startup_time_ms']:.2f}ms")
    
    if state_dict.get('errors'):
        print("\nErrors:")
        for error in state_dict['errors']:
            print(f"  • {error}")
    
    print("=" * 60)


def check_ready(url="http://localhost:8000"):
    """Check ready status once."""
    try:
        response = urllib.request.urlopen(f"{url}/api/ready")
        data = json.loads(response.read())
        return data
    except urllib.error.URLError as e:
        print(f"✗ Cannot connect to {url}")
        print(f"  Error: {e}")
        print("\nIs the backend running?")
        print("  python -m uvicorn main:app --host 0.0.0.0 --port 8000")
        return None
    except Exception as e:
        print(f"✗ Error: {e}")
        return None


def wait_for_ready(url="http://localhost:8000", timeout=60):
    """Poll ready endpoint until backend is ready or timeout."""
    print(f"Waiting for backend to be ready at {url}")
    print(f"Timeout: {timeout} seconds")
    print("-" * 60)
    
    start_time = time.time()
    last_progress = -1
    
    while time.time() - start_time < timeout:
        state = check_ready(url)
        
        if state is None:
            # Connection failed
            print("Waiting for backend to start...", end="\r")
            time.sleep(2)
            continue
        
        # Show progress if changed
        progress = state.get('progress', 0)
        if progress != last_progress:
            state_name = state.get('state', 'UNKNOWN')
            print(f"[{int(time.time() - start_time)}s] {state_name} - {progress}%")
            last_progress = progress
        
        # Check if ready
        if state.get('ready'):
            print("\n" + "✓" * 30)
            print("Backend is READY!")
            print("✓" * 30)
            print_ready_state(state)
            return True
        
        # Check for errors
        if state.get('state') == 'ERROR':
            print("\n" + "✗" * 30)
            print("Backend initialization FAILED!")
            print("✗" * 30)
            print_ready_state(state)
            return False
        
        # Continue polling
        time.sleep(1)
    
    print(f"\n✗ Timeout after {timeout} seconds")
    if state:
        print_ready_state(state)
    return False


def test_ready_endpoint(url="http://localhost:8000"):
    """Test the ready endpoint."""
    print("=" * 60)
    print("Testing /api/ready Endpoint")
    print("=" * 60)
    
    print(f"\nBackend URL: {url}")
    
    # Test 1: Single check
    print("\n--- Test 1: Single Ready Check ---")
    state = check_ready(url)
    if state:
        print_ready_state(state)
    
    # Test 2: Compare with health
    print("\n--- Test 2: Compare /api/health vs /api/ready ---")
    try:
        health_response = urllib.request.urlopen(f"{url}/api/health")
        health = json.loads(health_response.read())
        
        print("Health Check:")
        print(f"  Status: {health.get('status')}")
        print(f"  TTS Loaded: {health.get('model_loaded')}")
        print(f"  STT Available: {health.get('stt_available')}")
        print(f"  Chat Available: {health.get('chat_available')}")
        
        if state:
            print("\nReady Check:")
            print(f"  Ready: {state.get('ready')}")
            print(f"  State: {state.get('state')}")
            print(f"  Progress: {state.get('progress')}%")
    except Exception as e:
        print(f"Could not fetch health: {e}")
    
    # Test 3: Root endpoint
    print("\n--- Test 3: Root Endpoint ---")
    try:
        root_response = urllib.request.urlopen(f"{url}/")
        root = json.loads(root_response.read())
        print(f"Message: {root.get('message')}")
        print(f"Status: {root.get('status')}")
        print(f"Ready: {root.get('ready')}")
    except Exception as e:
        print(f"Could not fetch root: {e}")
    
    print("\n" + "=" * 60)
    print("Test Complete!")
    print("=" * 60)


if __name__ == "__main__":
    url = "http://localhost:8000"
    
    if len(sys.argv) > 1:
        command = sys.argv[1]
        
        if command == "wait":
            # Wait for backend to be ready
            timeout = int(sys.argv[2]) if len(sys.argv) > 2 else 60
            success = wait_for_ready(url, timeout)
            sys.exit(0 if success else 1)
        
        elif command == "check":
            # Single check
            state = check_ready(url)
            if state:
                print_ready_state(state)
                sys.exit(0 if state.get('ready') else 1)
            sys.exit(1)
        
        elif command == "test":
            # Full test
            test_ready_endpoint(url)
            sys.exit(0)
        
        else:
            print(f"Unknown command: {command}")
            print("\nUsage:")
            print("  python test_ready.py check          # Single ready check")
            print("  python test_ready.py wait [timeout] # Wait until ready")
            print("  python test_ready.py test           # Full test suite")
            sys.exit(1)
    
    else:
        # Default: single check
        state = check_ready(url)
        if state:
            print_ready_state(state)
            
            if not state.get('ready'):
                print("\nTip: Use 'python test_ready.py wait' to poll until ready")
