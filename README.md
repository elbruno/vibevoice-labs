# 🎙️ VibeVoice Labs

[![NuGet](https://img.shields.io/nuget/v/ElBruno.VibeVoiceTTS.svg?style=flat-square&logo=nuget)](https://www.nuget.org/packages/ElBruno.VibeVoiceTTS)
[![NuGet Downloads](https://img.shields.io/nuget/dt/ElBruno.VibeVoiceTTS.svg?style=flat-square&logo=nuget)](https://www.nuget.org/packages/ElBruno.VibeVoiceTTS)
[![Build Status](https://github.com/elbruno/vibevoice-labs/actions/workflows/publish.yml/badge.svg)](https://github.com/elbruno/vibevoice-labs/actions/workflows/publish.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg?style=flat-square)](LICENSE)
[![HuggingFace](https://img.shields.io/badge/🤗_HuggingFace-ONNX_Models-orange?style=flat-square)](https://huggingface.co/elbruno/VibeVoice-Realtime-0.5B-ONNX)
[![GitHub stars](https://img.shields.io/github/stars/elbruno/vibevoice-labs?style=social)](https://github.com/elbruno/vibevoice-labs)
[![Twitter Follow](https://img.shields.io/twitter/follow/elbruno?style=social)](https://twitter.com/elbruno)

A .NET library for text-to-speech synthesis using Microsoft's [VibeVoice-Realtime-0.5B](https://huggingface.co/microsoft/VibeVoice-Realtime-0.5B) — native C# inference via ONNX Runtime, no Python required at runtime.

## Features

- 🔊 **Natural Text-to-Speech** — High-quality speech synthesis powered by VibeVoice-Realtime-0.5B
- 📦 **NuGet Package** — [`ElBruno.VibeVoiceTTS`](https://www.nuget.org/packages/ElBruno.VibeVoiceTTS) — install and start generating speech in minutes
- 🤖 **Pure C# Inference** — ONNX Runtime, zero Python dependency at runtime
- 📥 **Auto-Download** — Models automatically downloaded from 🤗 HuggingFace on first use
- 🌍 **6 Voice Presets** — Carter, Davis, Emma, Frank, Grace, Mike (English voices with multilingual experimental support)
- 💉 **Dependency Injection** — First-class `IServiceCollection` integration
- 🖥️ **Cross-Platform** — Windows, Linux, macOS, MAUI-ready

## Installation

```bash
dotnet add package ElBruno.VibeVoiceTTS
```

## Quick Start

### 1) Generate speech and save to WAV

```csharp
using ElBruno.VibeVoiceTTS;

using var tts = new VibeVoiceSynthesizer();
await tts.EnsureModelAvailableAsync(); // auto-downloads ~1.5 GB on first run

float[] audio = await tts.GenerateAudioAsync("Hello! Welcome to VibeVoice Labs.", "Carter");
tts.SaveWav("output.wav", audio);
```

### 2) Use voice presets

```csharp
float[] carter = await tts.GenerateAudioAsync("Hello from Carter!", VibeVoicePreset.Carter);
float[] emma = await tts.GenerateAudioAsync("Hello from Emma!", VibeVoicePreset.Emma);
```

### 3) Track download progress

```csharp
var progress = new Progress<DownloadProgress>(p =>
{
    if (p.Stage == DownloadStage.Downloading)
        Console.Write($"\r⬇️ [{p.CurrentFile}] {p.PercentComplete:F0}%");
    else
        Console.WriteLine($"{p.Stage}: {p.Message}");
});

await tts.EnsureModelAvailableAsync(progress);
```

### 4) Configure options

```csharp
var options = new VibeVoiceOptions
{
    DiffusionSteps = 20,       // Quality vs speed tradeoff
    CfgScale = 1.5f,           // Classifier-free guidance scale
    SampleRate = 24000,        // Output sample rate
};

using var tts = new VibeVoiceSynthesizer(options);
```

### 5) Dependency Injection

```csharp
builder.Services.AddVibeVoice(options =>
{
    options.DiffusionSteps = 20;
});

// Then inject IVibeVoiceSynthesizer in your services
```

> **💡 Tip:** For best results, keep sentences short (~10 words). Longer text may produce artifacts due to model limitations. Consider splitting long text into sentences.

For the complete API reference and advanced usage, see the [Getting Started Guide](docs/GETTING_STARTED.md).

## Scenarios

This repository includes example projects demonstrating different ways to use VibeVoice:

| # | Scenario | Stack | Level | Description |
|---|----------|-------|-------|-------------|
| 1 | [Simple Python Script](src/scenario-01-simple/) | Python | Beginner | Minimal TTS demo — useful for model export and testing |
| 2 | [Full-Stack App](src/scenario-02-fullstack/) | Python + Blazor + Aspire | Intermediate | Web app with FastAPI backend and Blazor frontend |
| 3 | [**C# Console App**](src/scenario-03-csharp-simple/) | **C# (.NET 8)** | **Beginner** | **Recommended starting point** — pure C# with `ElBruno.VibeVoiceTTS` |
| 4 | [Full C# with Aspire](src/scenario-04-meai/) | C# + Blazor + Aspire | Intermediate | Full-stack C# app with WebAPI + Blazor frontend |
| 5 | [Batch Processing](src/scenario-05-batch-processing/) | Python | Intermediate | CLI to convert folders of .txt to .wav |
| 6 | [Real-Time Streaming](src/scenario-06-streaming-realtime/) | Python | Intermediate | Chunked audio playback for low-latency |
| 7 | [MAUI Mobile](src/scenario-07-maui-mobile/) | C# (.NET 10 MAUI) | Advanced | Cross-platform app (Windows/macOS/Android/iOS) |
| 8 | [ONNX Export](src/scenario-08-onnx-native/) | Python → C# | Advanced | ONNX model export tools and pipeline docs |

> **Note:** Python scenarios (1, 2, 5, 6) are primarily for ONNX model export, testing, and reference. The C# scenarios (3, 4, 7) run entirely in .NET with no Python dependency. See the [Scenarios Guide](docs/scenarios.md) for details.

## ONNX Models on HuggingFace

Pre-exported ONNX models are available on HuggingFace — the C# library downloads them automatically:

**🤗 [elbruno/VibeVoice-Realtime-0.5B-ONNX](https://huggingface.co/elbruno/VibeVoice-Realtime-0.5B-ONNX)**

The model includes 9 ONNX files (autoregressive pipeline with KV-cache) and 6 voice presets. See [Scenario 8](src/scenario-08-onnx-native/) for export details.

## Documentation

| Topic | Description |
|-------|-------------|
| [Getting Started](docs/GETTING_STARTED.md) | Prerequisites, setup, and first steps |
| [Scenarios Guide](docs/scenarios.md) | Detailed descriptions of all 8 scenarios |
| [Architecture](docs/ARCHITECTURE.md) | System design, ONNX pipeline, and data flow |
| [Project Structure](docs/project-structure.md) | Repository layout and file organization |
| [API Reference](docs/API_REFERENCE.md) | REST API documentation (for web scenarios) |
| [User Manual](docs/USER_MANUAL.md) | End-user guide for web interfaces |
| [Publishing](docs/publishing.md) | NuGet publishing with GitHub Actions |

## Tech Stack

| Layer | Technology | Purpose |
|-------|------------|---------|
| **C# TTS Library** | [ElBruno.VibeVoiceTTS](https://www.nuget.org/packages/ElBruno.VibeVoiceTTS) | Reusable .NET library with HuggingFace auto-download |
| **TTS Model** | [VibeVoice-Realtime-0.5B](https://huggingface.co/microsoft/VibeVoice-Realtime-0.5B) | Microsoft's text-to-speech model |
| **Inference** | [ONNX Runtime](https://onnxruntime.ai/) | Native C# model inference |
| **Frontend** | [Blazor](https://dotnet.microsoft.com/apps/aspnet/web-apps/blazor) (.NET 10) | Interactive web UI |
| **Orchestration** | [.NET Aspire](https://learn.microsoft.com/en-us/dotnet/aspire/) | Service discovery & health checks |

## Building from Source

```bash
git clone https://github.com/elbruno/vibevoice-labs.git
cd vibevoice-labs
dotnet build src/ElBruno.VibeVoiceTTS/ElBruno.VibeVoiceTTS.csproj
dotnet test src/ElBruno.VibeVoiceTTS.Tests/ElBruno.VibeVoiceTTS.Tests.csproj
```

### Requirements

- .NET 8.0 SDK or later
- ONNX Runtime compatible platform (Windows, Linux, macOS)
- Python 3.11+ (only needed for ONNX model export — not for runtime use)

## 🤝 Contributing

Contributions are welcome! Please:

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/amazing-feature`)
3. Commit your changes (`git commit -m 'Add amazing feature'`)
4. Push to the branch (`git push origin feature/amazing-feature`)
5. Open a Pull Request

## 📄 License

This project is licensed under the MIT License — see the [LICENSE](LICENSE) file for details.

## 👋 About the Author

Hi! I'm **ElBruno** 🧡, a passionate developer and content creator exploring AI, .NET, and modern development practices.

**Made with ❤️ by [ElBruno](https://github.com/elbruno)**

If you like this project, consider following my work across platforms:

- 📻 **Podcast**: [No Tienen Nombre](https://notienenombre.com) — Spanish-language episodes on AI, development, and tech culture
- 💻 **Blog**: [ElBruno.com](https://elbruno.com) — Deep dives on embeddings, RAG, .NET, and local AI
- 📺 **YouTube**: [youtube.com/elbruno](https://www.youtube.com/elbruno) — Demos, tutorials, and live coding
- 🔗 **LinkedIn**: [@elbruno](https://www.linkedin.com/in/elbruno/) — Professional updates and insights
- 𝕏 **Twitter**: [@elbruno](https://www.x.com/in/elbruno/) — Quick tips, releases, and tech news