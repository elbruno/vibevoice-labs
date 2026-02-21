# Scenario 3 — C# Console Simple Demo (ONNX Native)

A simple C# console app that runs VibeVoice TTS using **ONNX Runtime** — pure native C# with no Python dependency at runtime.

## How It Works

```
C# Program.cs  →  ONNX Runtime  →  text_encoder.onnx + diffusion_step.onnx + acoustic_decoder.onnx  →  output.wav
(.NET host)        (native C#)      (exported VibeVoice model subcomponents)
```

The app loads pre-exported ONNX model files and runs the full TTS inference pipeline in C#.

## Prerequisites

- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) or later
- ONNX model files (see [Export Models](#export-models) below)

## Export Models (One-Time)

ONNX models must be exported first using the Python export tool in `scenario-08-onnx-native/export/`:

```bash
cd src/scenario-08-onnx-native/export
pip install -r requirements_export.txt
python export_model.py --output ../models
python export_voice_presets.py --output ../models/voices
```

## Quick Start

```bash
cd src/scenario-03-csharp-simple
dotnet run
```

The app will:
1. Locate ONNX model files in `../scenario-08-onnx-native/models/`
2. Load ONNX Runtime inference sessions
3. Run the TTS pipeline and generate audio
4. Save `output.wav` in the current directory

## What Each Step Does

| Step | Description |
|---|---|
| **1** | Parse configuration and locate ONNX model files |
| **2** | Validate all required ONNX models exist |
| **3** | Load ONNX sessions and run inference pipeline |

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
| `Program.cs` | C# host — loads ONNX models, runs TTS pipeline |
| `VoiceLabs.Console.csproj` | .NET 8.0 project file with ONNX Runtime NuGet |

## Full Pipeline

For the complete ONNX inference implementation with tokenizer, diffusion scheduler, and audio output, see [`scenario-08-onnx-native/csharp/`](../scenario-08-onnx-native/csharp/).
