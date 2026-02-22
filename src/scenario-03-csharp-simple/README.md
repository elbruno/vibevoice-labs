# Scenario 3 — C# Console Simple Demo (ElBruno.VibeVoice)

A simple C# console app that runs VibeVoice TTS using the **ElBruno.VibeVoice** library — pure native C# with automatic model download from HuggingFace.

## How It Works

```
C# Program.cs  →  ElBruno.VibeVoice  →  ONNX Runtime  →  output.wav
(.NET host)        (library)              (native C#)
```

The app uses the `VibeVoiceSynthesizer` class which handles model management (auto-download from 🤗 HuggingFace) and runs the full TTS inference pipeline.

## Prerequisites

- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) or later
- Internet connection (for first-time model download, ~700 MB)

## Quick Start

```bash
cd src/scenario-03-csharp-simple
dotnet run
```

The app will:
1. Check if ONNX model files exist in the shared cache (`%LOCALAPPDATA%\ElBruno\VibeVoice\models`)
2. Auto-download from HuggingFace if missing (with progress reporting)
3. Run the TTS pipeline and generate audio
4. Save `output.wav` in the current directory

## Code Example

```csharp
using ElBruno.VibeVoice;

using var tts = new VibeVoiceSynthesizer();
await tts.EnsureModelAvailableAsync();

float[] audio = await tts.GenerateAudioAsync("Hello!", VibeVoicePreset.Emma);
tts.SaveWav("output.wav", audio);
```

## Custom Model Path

To use a custom model path instead of the shared cache:

```csharp
var options = new VibeVoiceOptions { ModelPath = @"C:\my\models" };
using var tts = new VibeVoiceSynthesizer(options);
```

## Trying Different Voices

Edit `Program.cs` and change the voice preset:

```csharp
var voice = VibeVoicePreset.Carter;  // Male (default)
var voice = VibeVoicePreset.Emma;    // Female
var voice = VibeVoicePreset.Davis;   // Male
var voice = VibeVoicePreset.Grace;   // Female
```

## Files

| File | Purpose |
|---|---|
| `Program.cs` | C# demo — uses ElBruno.VibeVoice library |
| `VoiceLabs.Console.csproj` | .NET 8.0 project with ElBruno.VibeVoice reference |

## Library

This scenario uses the [`ElBruno.VibeVoice`](../ElBruno.VibeVoice/) library. For the advanced scenario with CLI args, see [`scenario-08-onnx-native/csharp/`](../scenario-08-onnx-native/csharp/).
