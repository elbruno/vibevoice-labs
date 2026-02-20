import asyncio
import websockets
import json
import sys

async def test_websocket(port):
    uri = f"ws://localhost:{port}/ws/conversation"
    print(f"Connecting to {uri}...")
    try:
        async with websockets.connect(uri) as websocket:
            print("Connected successfully!")
            
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
                            break
                        elif data.get("type") == "error":
                            print(f"Error from server: {data.get('error')}")
                            break
                except asyncio.TimeoutError:
                    print("Timeout waiting for response")
                    break
    except Exception as e:
        print(f"Connection failed: {e}")

if __name__ == "__main__":
    port = sys.argv[1] if len(sys.argv) > 1 else 8000
    asyncio.run(test_websocket(port))