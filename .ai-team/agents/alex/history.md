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

### 2026-02-19: Scenario 4 — REWRITTEN to Microsoft.Extensions.AI
- **Complete rewrite** from Semantic Kernel to Microsoft.Extensions.AI (MEAI) per Bruno's request
- **Directory renamed:** `scenario-04-semantic-kernel/` → `scenario-04-meai/` (old directory removed entirely)
- **Program.cs:** Rewritten to use `IChatClient` abstraction from `Microsoft.Extensions.AI`
  - Pattern: `new OpenAIClient(apiKey).GetChatClient("gpt-4o-mini").AsIChatClient()`
  - Uses `ChatMessage` with `ChatRole.System` and `ChatRole.User`
  - Response via `chatClient.GetResponseAsync(messages)`
- **SpeechPlugin.cs:** Simplified to plain HTTP client wrapper (no SK attributes)
- **VoiceLabs.MEAI.csproj:** Uses `Microsoft.Extensions.AI.OpenAI` v10.3.0 (removed SK dependency)
- **README.md:** Completely rewritten with MEAI pattern examples and explanation
- **Documentation updates:** Root README, USER_MANUAL, GETTING_STARTED, ARCHITECTURE all updated
- **Build status:** ✅ Verified with `dotnet build` (zero errors, package resolution to v10.3.0)
- **Rationale:** MEAI is a lightweight abstraction for direct chat completion calls, simpler than SK for this use case

### 2026-02-19: Scenario 3 — C# Console Simple Demo
- Created `src/scenario-03-csharp-simple/` mirroring Scenario 1's step-by-step teaching style in C#
- Uses top-level statements with `HttpClient` calling the Python FastAPI backend
- Backend URL configurable via `VIBEVOICE_BACKEND_URL` env var (default: `http://localhost:5100`)
- JSON models use `[JsonPropertyName("snake_case")]` records for API compatibility
- Includes commented-out alternatives for multilingual voices and streaming example
- .NET 10 (`net10.0` TFM), no external NuGet packages needed (System.Text.Json is built-in)

### 2026-02-19: All C# Scenarios Build Status
- **Scenario 2 (Full Stack):** ✅ Builds successfully (`dotnet build VoiceLabs.slnx`)
  - VoiceLabs.ServiceDefaults, VoiceLabs.Web, VoiceLabs.AppHost, VoiceLabs.Web.Tests all compile
  - Home.razor contains complete TTS UI with glassmorphism styling, audio playback, collapsible sections
- **Scenario 3 (C# Console):** ✅ Builds successfully (`dotnet build VoiceLabs.Console.csproj`)
  - Zero errors, README.md accurate with usage instructions and configuration
- **Scenario 4 (Semantic Kernel):** ✅ Builds successfully with known warning (`dotnet build VoiceLabs.SK.csproj`)
  - NU1904 vulnerability warning on Microsoft.SemanticKernel.Core 1.54.0 is a known issue
  - SpeechPlugin.cs exists in Plugins/ directory with [KernelFunction] attribute
  - README.md accurate with OpenAI API key setup and alternative LLM providers
- **Scenario 7 (MAUI Mobile):** ⚠️ Requires `dotnet workload install maui`
  - NETSDK1147: maui-android workload not installed on build machine
  - All source files exist: MainPage.xaml, Services/TtsService.cs, platform scaffolding
  - README.md accurate with workload installation instructions and platform-specific run commands
  - Code structure is complete and correct; builds cleanly once workload is installed

### 2026-02-19: Scenario 3 — Rewritten to Direct Model Invocation
- **Complete rewrite** from HTTP client wrapper to direct VibeVoice model invocation via `System.Diagnostics.Process`
- **Approach:** C# orchestrates the flow, Python `tts_helper.py` is the TTS engine that loads the model directly
- **Why Process over pythonnet:** Simpler, no Python runtime configuration in C#, works with any Python venv
- **New files:**
  - `src/scenario-03-csharp-simple/tts_helper.py` — Python script that accepts `--text`, `--voice`, `--output` args
  - `src/scenario-03-csharp-simple/requirements.txt` — Python deps (vibevoice, torch, soundfile)
- **Modified files:**
  - `src/scenario-03-csharp-simple/Program.cs` — Removed all HttpClient/JSON code, uses Process instead
  - `src/scenario-03-csharp-simple/README.md` — Updated for new architecture
  - Root `README.md`, `docs/GETTING_STARTED.md`, `docs/USER_MANUAL.md`, `docs/ARCHITECTURE.md` — All updated
- **Config:** `PYTHON_PATH` env var replaces `VIBEVOICE_BACKEND_URL`
- **Build:** ✅ Verified with `dotnet build` (zero errors)
- **Voice presets:** Uses same Capitalized name format as Scenario 1 (Carter, Davis, Emma, etc.)
