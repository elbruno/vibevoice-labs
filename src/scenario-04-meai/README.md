# Scenario 4: Microsoft.Extensions.AI + VibeVoice — An AI Agent that Speaks

A C# console app that uses **Microsoft.Extensions.AI** (MEAI) to generate an AI response and then speaks it aloud through the **VibeVoice** TTS backend.

**Pattern:** Ask a question → AI generates text → VibeVoice speaks it 🎙️

## Prerequisites

| Requirement | Details |
|---|---|
| .NET 10 SDK | [Download](https://dotnet.microsoft.com/download/dotnet/10.0) |
| OpenAI API key | Set as `OPENAI_API_KEY` environment variable |
| VibeVoice Python backend | Running on `http://localhost:5100` (see Scenario 2) |

## Quick Start

1. **Start the VibeVoice backend** (from the repo root):
   ```bash
   cd src/scenario-02-fullstack/backend
   pip install -r requirements.txt
   uvicorn main:app --port 5100
   ```

2. **Set your OpenAI API key:**
   ```bash
   # Windows PowerShell
   $env:OPENAI_API_KEY = "sk-..."

   # Linux / macOS
   export OPENAI_API_KEY="sk-..."
   ```

3. **Run the app:**
   ```bash
   cd src/scenario-04-meai
   dotnet run
   ```

4. **Enter a question** (or press Enter for the default) and hear the AI speak!

## What the Demo Shows

| Step | What Happens |
|------|-------------|
| 1 | Configures Microsoft.Extensions.AI `IChatClient` with OpenAI (`gpt-4o-mini`) |
| 2 | Creates a **SpeechPlugin** that wraps the VibeVoice HTTP API |
| 3 | Reads a user prompt from the console |
| 4 | Generates a concise AI text response via MEAI chat completion |
| 5 | Sends the text to VibeVoice (`POST /api/tts`) and saves WAV audio |
| 6 | Plays the audio file automatically |

## Microsoft.Extensions.AI Pattern

This demo uses the **IChatClient** abstraction from Microsoft.Extensions.AI:

```csharp
using Microsoft.Extensions.AI;
using OpenAI;

var chatClient = new OpenAIClient(apiKey)
    .GetChatClient("gpt-4o-mini")
    .AsIChatClient();

var messages = new List<ChatMessage>
{
    new(ChatRole.System, "You are a helpful assistant."),
    new(ChatRole.User, "Why is the sky blue?")
};

var response = await chatClient.GetResponseAsync(messages);
Console.WriteLine(response.Text);
```

## Customisation

### Use a Different OpenAI Model

Open `Program.cs` and uncomment the alternative you want:

```csharp
// OpenAI gpt-4o (more capable)
var chatClient = new OpenAIClient(openAiKey)
    .GetChatClient("gpt-4o")
    .AsIChatClient();
```

### Change the Voice

Edit the `voiceId` variable in `Program.cs`, or call `GET /api/voices` to see what's available:

```bash
curl http://localhost:5100/api/voices
```

### Point to a Different Backend URL

```bash
$env:VIBEVOICE_BACKEND_URL = "http://my-server:8000"
dotnet run
```

## Project Structure

```
scenario-04-meai/
├── Program.cs              # Step-by-step console app
├── Plugins/
│   └── SpeechPlugin.cs     # HTTP client wrapper for VibeVoice API
├── VoiceLabs.MEAI.csproj   # .NET 10 project file
└── README.md               # You are here
```

## Why Microsoft.Extensions.AI?

**Microsoft.Extensions.AI** is a .NET abstraction layer for AI services that provides:

- **Provider-agnostic** — works with OpenAI, Azure OpenAI, Ollama, and other providers
- **Lightweight** — minimal dependencies, no heavy frameworks
- **Modern .NET** — built for .NET 10+ with async/await patterns
- **Type-safe** — strongly-typed request/response models

Unlike Semantic Kernel (which focuses on orchestration and agents), MEAI is a thin abstraction for direct chat completion calls.
