# Scenario 4: Semantic Kernel + VibeVoice — An AI Agent that Speaks

A C# console app that uses **Microsoft Semantic Kernel** to generate an AI response and then speaks it aloud through the **VibeVoice** TTS backend.

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
   cd src/scenario-04-semantic-kernel
   dotnet run
   ```

4. **Enter a question** (or press Enter for the default) and hear the AI speak!

## What the Demo Shows

| Step | What Happens |
|------|-------------|
| 1 | Configures Semantic Kernel with OpenAI (`gpt-4o-mini`) |
| 2 | Registers a **SpeechPlugin** that wraps the VibeVoice HTTP API |
| 3 | Reads a user prompt from the console |
| 4 | Generates a concise AI text response via SK |
| 5 | Sends the text to VibeVoice (`POST /api/tts`) and saves WAV audio |
| 6 | Plays the audio file automatically |

## Customisation

### Use a Different LLM Provider

Open `Program.cs` and uncomment the alternative you want:

```csharp
// Azure OpenAI
builder.AddAzureOpenAIChatCompletion("my-deployment", azureEndpoint, azureKey);

// Ollama / local model
builder.AddOpenAIChatCompletion("llama3", apiKey: "unused",
    httpClient: new HttpClient { BaseAddress = new Uri("http://localhost:11434/v1") });
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
scenario-04-semantic-kernel/
├── Program.cs              # Step-by-step console app
├── Plugins/
│   └── SpeechPlugin.cs     # SK plugin wrapping VibeVoice API
├── VoiceLabs.SK.csproj     # .NET 10 project file
└── README.md               # You are here
```
