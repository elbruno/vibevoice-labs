# Alex — History

## Project Learnings (from init)
- Project: VibeVoice Labs — showcase for VibeVoice TTS
- User: Bruno Capuano (bcapuano@gmail.com)
- Scenario 2: Blazor .NET 10 frontend + Aspire orchestration
- Aspire Python integration: `Aspire.Hosting.Python` NuGet package
- Use `AddUvicornApp` for FastAPI backend: `builder.AddUvicornApp("python-api", "../python-app", "main:app")`
- Configure endpoint: `.WithHttpEndpoint(port: 8000, env: "PORT")`
- Reference Python service: `builder.AddProject<Projects.BlazorApp>().WithReference(python)`

## Learnings

### 2026-02-19: Aspire + Blazor Solution Structure
- Used `dotnet new aspire-apphost` and `dotnet new aspire-servicedefaults` templates for proper .NET 10 scaffolding
- .NET 10 creates `.slnx` solution files (XML-based) instead of traditional `.sln` files
- AppHost orchestration uses `Projects.VoiceLabs_Web` (underscore replaces dot) for project references
- Backend service discovery uses `http://backend` URL scheme via Aspire service discovery
- Use `WaitFor(backend)` to ensure backend is ready before frontend starts

### 2026-02-19: Blazor TTS UI Implementation
- Created glassmorphism dark theme with CSS custom properties for consistent styling
- Used `@rendermode InteractiveServer` for server-side interactivity
- HTML5 audio element works with base64 data URLs for dynamic audio playback
- JSON property names need `[JsonPropertyName("snake_case")]` for Python API compatibility
- Fallback voices implemented if backend is unavailable during initialization
- Collapsible sections improve UX for sample texts without cluttering main interface

### 2026-02-19: API Contract Implementation
- TtsService uses typed HttpClient injection via `builder.Services.AddHttpClient<TtsService>`
- Request model uses `voice_id` (snake_case) to match Python FastAPI expectations
- Audio returned as binary bytes, converted to base64 for browser playback
- Error handling returns null to allow graceful degradation in UI

### 2026-02-19: .NET MAUI Cross-Platform TTS App (Scenario 7)
- Created full MAUI app targeting net10.0-android/ios/maccatalyst/windows
- Used Plugin.Maui.Audio for cross-platform audio playback from MemoryStream
- CommunityToolkit.Maui added for enhanced controls
- MainPage uses constructor DI — must register `AddTransient<MainPage>()` in MauiProgram
- TtsService uses typed HttpClient with configurable base URL
- Dark theme (#1a1a2e background, #7c3aed accent) matches project style
- Fallback voices list if backend is unreachable during init
- Android needs `INTERNET` permission in AndroidManifest.xml
- For Android emulator, backend URL should be `http://10.0.2.2:PORT`
- Platform scaffolding: Android (MainActivity + MainApplication), Windows (App.xaml), iOS/MacCatalyst (AppDelegate + Program)

### 2026-02-19: Scenario 4 — Semantic Kernel + VibeVoice
- Created `src/scenario-04-semantic-kernel/` with Program.cs, SpeechPlugin.cs, VoiceLabs.SK.csproj, README.md
- Used `Microsoft.SemanticKernel` v1.54.0 with `[KernelFunction]` attribute for the speech plugin
- SpeechPlugin wraps VibeVoice backend (`POST /api/tts`) using snake_case JSON (`voice_id`) for Python API compatibility
- Pattern: user prompt → SK chat completion (gpt-4o-mini) → VibeVoice TTS → WAV file → auto-play
- Included commented alternatives for Azure OpenAI, Ollama/local models, and different voices
- Backend URL configurable via `VIBEVOICE_BACKEND_URL` env var (default `http://localhost:5100`)
- .NET 10 project builds cleanly with zero errors

### 2026-02-19: Scenario 3 — C# Console Simple Demo
- Created `src/scenario-03-csharp-simple/` mirroring Scenario 1's step-by-step teaching style in C#
- Uses top-level statements with `HttpClient` calling the Python FastAPI backend
- Backend URL configurable via `VIBEVOICE_BACKEND_URL` env var (default: `http://localhost:5100`)
- JSON models use `[JsonPropertyName("snake_case")]` records for API compatibility
- Includes commented-out alternatives for multilingual voices and streaming example
- .NET 10 (`net10.0` TFM), no external NuGet packages needed (System.Text.Json is built-in)
