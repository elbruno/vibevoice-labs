# Scenario 3 — C# Console Simple Demo (Direct Model)

A simple C# console app that runs VibeVoice TTS **directly** by invoking the Python model from C# using `System.Diagnostics.Process`. No HTTP backend required — the model runs locally.

## How It Works

```
C# Program.cs  →  python tts_helper.py  →  VibeVoice Model  →  output.wav
(orchestrator)     (TTS engine)              (0.5B params)
```

The C# app orchestrates the flow: takes user input, invokes the Python TTS helper script, streams progress output, and reports the result.

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [Python 3.11+](https://python.org) with VibeVoice installed

## Quick Start

### 1. Install Python dependencies

```bash
cd src/scenario-03-csharp-simple
pip install -r requirements.txt
```

Or use the shared repo-root virtual environment (see root README).

### 2. Run the console app

```bash
dotnet run
```

The app will:
1. Verify Python and VibeVoice are installed
2. Invoke `tts_helper.py` with the selected text and voice
3. Stream progress output (model loading, generation)
4. Save `output.wav` in the current directory

## Configuration

| Variable | Default | Description |
|---|---|---|
| `PYTHON_PATH` | `python` | Path to Python executable (use if Python is not on PATH) |

```bash
# Example: use a specific Python from a virtual environment
set PYTHON_PATH=C:\path\to\.venv\Scripts\python.exe
dotnet run
```

## What Each Step Does

| Step | Description |
|---|---|
| **1** | Configure Python path and locate `tts_helper.py` |
| **2** | Verify Python is available and VibeVoice is installed |
| **3** | Select a voice preset (Carter, Davis, Emma, Frank, Grace, Mike) |
| **4** | Define the text to synthesize |
| **5** | Generate audio by invoking `tts_helper.py` via `Process` |
| **6** | Verify and report the output WAV file |

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
| `Program.cs` | C# orchestrator — invokes Python, handles output |
| `tts_helper.py` | Python TTS engine — loads model, generates audio |
| `requirements.txt` | Python dependencies for tts_helper.py |
| `VoiceLabs.Console.csproj` | .NET 10 project file |
