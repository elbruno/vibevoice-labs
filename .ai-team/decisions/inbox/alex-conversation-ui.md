# Decision: Conversation Web UI Architecture

**Date:** 2026-02-19  
**By:** Alex (Frontend Dev)  
**Requested by:** Bruno Capuano

## What

Created `src/scenario-04-meai/VoiceLabs.ConversationWeb/` — a Blazor Server real-time voice conversation frontend for Scenario 4.

## Key Decisions

### WebSocket over HTTP polling
- Real-time voice conversation requires low-latency bidirectional communication
- WebSocket URL derived from Aspire service discovery: `http://backend` → `ws://backend/ws/conversation`
- Binary frames for audio data, JSON text frames for control messages

### Push-to-Talk (not voice activity detection)
- Simpler and more reliable than VAD
- Works across all browsers without extra libraries
- Touch events supported for mobile usage
- Clear UX: hold button = recording, release = send

### MediaRecorder with webm format
- Native browser API, no external dependencies
- webm/opus is well-supported across modern browsers
- Backend handles transcoding to PCM/WAV as needed

### Auto-play AI responses
- Audio auto-plays when `audio_complete` message received
- Users can replay via inline 🔊 button on each AI message

### ServiceDefaults duplication
- Copied ServiceDefaults from scenario-02 into scenario-04-meai
- Each scenario is self-contained; avoids cross-scenario project references

## Files Created

```
VoiceLabs.ConversationWeb/
├── VoiceLabs.ConversationWeb.csproj
├── Program.cs
├── Properties/launchSettings.json
├── appsettings.json
├── appsettings.Development.json
├── Components/
│   ├── App.razor
│   ├── Routes.razor
│   ├── _Imports.razor
│   ├── Layout/MainLayout.razor
│   └── Pages/Home.razor
└── wwwroot/
    ├── favicon.ico
    ├── css/app.css
    └── js/audio.js

VoiceLabs.ServiceDefaults/
├── VoiceLabs.ServiceDefaults.csproj
└── Extensions.cs
```

## Build Status

✅ `dotnet build` succeeds with zero errors.
