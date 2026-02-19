# Scenario 3 — C# Console Simple Demo (CSnakes)

A simple C# console app that runs VibeVoice TTS using **CSnakes** to embed the Python model directly inside the .NET process. No subprocess calls, no HTTP backends — the Python interpreter runs in-process.

## How It Works

```
C# Program.cs  →  CSnakes (embedded CPython)  →  vibevoice_tts.py  →  output.wav
(.NET host)        (in-process Python runtime)     (VibeVoice model)
```

CSnakes embeds a real CPython interpreter inside the .NET process and auto-generates typed C# wrappers from the type-annotated Python functions.

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- Internet access on first run (CSnakes auto-downloads Python + VibeVoice model ~1GB)

## Quick Start

```bash
cd src/scenario-03-csharp-simple
dotnet run
```

The app will:
1. Set up an embedded Python environment via CSnakes
2. Auto-install Python dependencies (first run)
3. Load the VibeVoice model and generate audio
4. Save `output.wav` in the current directory

## What Each Step Does

| Step | Description |
|---|---|
| **1** | CSnakes configures embedded Python with virtual environment |
| **2** | Select voice preset and text to synthesize |
| **3** | Call `vibevoice_tts.synthesize_speech()` via CSnakes interop |

## Trying Different Voices

Edit `Program.cs` and uncomment any of the alternative voice lines:

```csharp
var voice = "Carter";    // Male, clear American English (default)
// voice = "Davis";      // Male voice
// voice = "Emma";       // Female voice
// voice = "Frank";      // Male voice
// voice = "Grace";      // Female voice
// voice = "Mike";       // Male voice
```

## Files

| File | Purpose |
|---|---|
| `Program.cs` | C# host — configures CSnakes, calls Python TTS |
| `vibevoice_tts.py` | Python TTS module (embedded via CSnakes) |
| `requirements.txt` | Python dependencies (install manually or via CSnakes venv) |
| `VoiceLabs.Console.csproj` | .NET 10 project file with CSnakes NuGet |
