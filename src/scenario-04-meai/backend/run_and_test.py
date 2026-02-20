import subprocess
import time
import sys
import urllib.request
import asyncio
import websockets
import json

def wait_for_server(port, timeout=120):
    print(f"Waiting for backend to start on port {port}...")
    start_time = time.time()
    while time.time() - start_time < timeout:
        try:
            response = urllib.request.urlopen(f"http://localhost:{port}/api/health")
            if response.getcode() == 200:
                print("Backend is up and running!")
                return True
        except Exception:
            pass
        time.sleep(2)
    print("Timeout waiting for backend to start.")
    return False

async def test_websocket(port):
    uri = f"ws://localhost:{port}/ws/conversation"
    print(f"Connecting to {uri}...")
    try:
        async with websockets.connect(uri) as websocket:
            print("WebSocket connected successfully!")
            
            print("Sending dummy audio data...")
            await websocket.send(b'\x00' * 1024)
            
            print("Sending end_of_speech...")
            await websocket.send(json.dumps({"type": "end_of_speech"}))
            
            print("Waiting for response...")
            while True:
                try:
                    response = await asyncio.wait_for(websocket.recv(), timeout=15.0)
                    if isinstance(response, bytes):
                        print(f"Received binary data: {len(response)} bytes")
                    else:
                        print(f"Received text data: {response}")
                        data = json.loads(response)
                        if data.get("type") == "audio_complete":
                            print("Test completed successfully!")
                            return True
                        elif data.get("type") == "error":
                            print(f"Error from server: {data.get('error')}")
                            return False
                except asyncio.TimeoutError:
                    print("Timeout waiting for response")
                    return False
    except Exception as e:
        print(f"Connection failed: {e}")
        return False

if __name__ == "__main__":
    port = 8000
    print("Starting backend server...")
    # Start the backend process without PIPE to avoid deadlock
    with open("server_test.log", "w") as log_file:
        process = subprocess.Popen(
            [sys.executable, "-m", "uvicorn", "main:app", "--host", "0.0.0.0", "--port", str(port)],
            stdout=log_file,
            stderr=subprocess.STDOUT,
            text=True
        )
        
        try:
            if wait_for_server(port):
                print("\n--- Running WebSocket Test ---")
                success = asyncio.run(test_websocket(port))
                if success:
                    print("All services are working fine!")
                else:
                    print("WebSocket test failed.")
            else:
                print("Failed to start backend.")
        finally:
            print("Shutting down backend server...")
            process.terminate()
            process.wait()
            print("\n--- Server Logs ---")
            try:
                with open("server_test.log", "r") as f:
                    print(f.read())
            except Exception:
                pass
