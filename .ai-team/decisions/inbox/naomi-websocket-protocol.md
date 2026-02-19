# WebSocket Protocol: Voice Conversation

**Proposed by:** Naomi (Backend Dev)  
**Date:** 2026-02-19  
**Status:** Proposed  
**Affects:** Alex (Frontend Dev)

## Endpoint

`ws://<host>:<port>/ws/conversation`

## Protocol Summary

One conversation turn = client sends audio → server responds with text + audio.

## Client → Server Messages

### 1. Audio Data (Binary Frame)
- **Format:** Raw 16kHz, 16-bit signed integer, mono PCM
- **Delivery:** Stream binary frames as microphone captures audio
- **Max buffer:** 30 seconds (~960KB)

### 2. End of Speech (Text Frame)
```json
{"type": "end_of_speech"}
```
Signals the server to process accumulated audio. Triggers the STT → Chat → TTS pipeline.

### 3. Reset Conversation (Text Frame)
```json
{"type": "reset"}
```
Clears AI conversation history. Server responds with `{"type": "reset_ack"}`.

## Server → Client Messages

### 1. Transcript (Text Frame)
```json
{"type": "transcript", "text": "What the user said"}
```
Sent after STT completes.

### 2. AI Response (Text Frame)
```json
{"type": "response", "text": "AI's reply text"}
```
Sent after chat completion.

### 3. Audio (Binary Frame)
WAV audio data at 24kHz sample rate. Contains WAV header — playable directly.

### 4. Audio Complete (Text Frame)
```json
{"type": "audio_complete"}
```
All audio for this turn has been sent. Client can start next turn.

### 5. Error (Text Frame)
```json
{"type": "error", "error": "Description of what went wrong"}
```

## Turn Sequence Diagram

```
Client                          Server
  |                               |
  |-- binary audio frames ------->|
  |-- binary audio frames ------->|
  |-- {"type":"end_of_speech"} -->|
  |                               | [STT processing]
  |<-- {"type":"transcript"} -----|
  |                               | [Chat completion]
  |<-- {"type":"response"} -------|
  |                               | [TTS generation]
  |<-- binary WAV audio ----------|
  |<-- {"type":"audio_complete"} -|
  |                               |
  | (next turn...)                |
```

## Notes for Frontend Implementation

1. Use `MediaRecorder` or `AudioWorklet` to capture 16kHz 16-bit PCM from mic
2. Send audio chunks as binary WebSocket frames during recording
3. Send `end_of_speech` when user stops talking (button release or VAD)
4. Wait for `audio_complete` before allowing next turn
5. Play received binary frames directly as WAV audio (`new Audio(URL.createObjectURL(blob))`)
6. Display `transcript` and `response` text in the UI for visual feedback
