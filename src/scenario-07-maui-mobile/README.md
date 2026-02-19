# Scenario 7 — .NET MAUI Cross-Platform TTS App

A .NET MAUI application that provides a clean, modern UI for text-to-speech using the VibeVoice Python backend. Works on **Windows, macOS, Android, and iOS**.

## What This Shows

- Cross-platform native app with a single C# / XAML codebase
- Calling a Python TTS backend over HTTP from a mobile/desktop client
- Audio playback on all platforms via `Plugin.Maui.Audio`
- Modern dark-themed UI with .NET MAUI controls

## Architecture

```
┌────────────────────┐        HTTP        ┌──────────────────┐
│  .NET MAUI App     │ ──────────────────▶ │  Python Backend  │
│  (thin client)     │   POST /api/tts    │  (FastAPI)       │
│                    │ ◀────────────────── │                  │
│  • Text input      │     WAV bytes      │  • VibeVoice TTS │
│  • Voice picker    │                    │  • Voice registry│
│  • Audio playback  │   GET /api/voices  │  • Health check  │
└────────────────────┘                    └──────────────────┘
```

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/) with MAUI workload
- Python backend running (see Scenario 2 or start manually on port 5100)

### Install the MAUI workload

```bash
dotnet workload install maui
```

## How to Run

### 1. Start the Python backend

```bash
# From the project root or scenario-02 backend
cd src/scenario-02-fullstack/backend
pip install -r requirements.txt
uvicorn main:app --port 5100
```

### 2. Configure the backend URL

Edit `MauiProgram.cs` and set `backendUrl` to your backend address:

```csharp
var backendUrl = "http://localhost:5100";
```

> For Android emulator, use `http://10.0.2.2:5100` to reach the host machine.

### 3. Run on your platform

```bash
# Windows
dotnet build -t:Run -f net10.0-windows10.0.19041.0

# Android
dotnet build -t:Run -f net10.0-android

# macOS (Mac Catalyst)
dotnet build -t:Run -f net10.0-maccatalyst

# iOS (requires Mac with Xcode)
dotnet build -t:Run -f net10.0-ios
```

## Screenshots

<!-- TODO: Add screenshots after first build -->

| Windows | Android | macOS |
|---------|---------|-------|
| _coming soon_ | _coming soon_ | _coming soon_ |

## Key Files

| File | Purpose |
|------|---------|
| `MauiProgram.cs` | App setup, DI, HttpClient config |
| `Services/TtsService.cs` | HTTP client for the TTS backend API |
| `MainPage.xaml` | UI layout (text input, voice picker, playback) |
| `MainPage.xaml.cs` | Event handlers and audio playback logic |

## NuGet Packages

- **CommunityToolkit.Maui** — Enhanced MAUI controls and helpers
- **Plugin.Maui.Audio** — Cross-platform audio playback
