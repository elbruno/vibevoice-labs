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
