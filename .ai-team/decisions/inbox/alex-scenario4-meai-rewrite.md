# Decision: Scenario 4 Rewritten to Use Microsoft.Extensions.AI

**Date:** 2026-02-19  
**By:** Alex (Frontend Dev)  
**Requested by:** Bruno Capuano

## Summary

Scenario 4 has been completely rewritten from using Microsoft.SemanticKernel to using **Microsoft.Extensions.AI** (MEAI). This is a full replacement, not a migration.

## Changes Made

### 1. Directory Rename
- **Old:** `src/scenario-04-semantic-kernel/`
- **New:** `src/scenario-04-meai/`
- The entire old directory was removed and replaced with new files

### 2. Code Rewrite
- **Program.cs:** Completely rewritten to use MEAI's `IChatClient` interface
  - Uses `Microsoft.Extensions.AI` namespace
  - Pattern: `new OpenAIClient(apiKey).GetChatClient("gpt-4o-mini").AsIChatClient()`
  - Chat messages use `ChatMessage` with `ChatRole.System` and `ChatRole.User`
  - Response retrieved with `chatClient.GetResponseAsync(messages)`
  
- **SpeechPlugin.cs:** Simplified to a plain HTTP client wrapper
  - Removed all Semantic Kernel attributes (`[KernelFunction]`, `[Description]`)
  - No longer a "plugin" in the SK sense, just a service class
  - Same HTTP contract with VibeVoice backend (`POST /api/tts`)

- **VoiceLabs.MEAI.csproj:** New project file
  - Package: `Microsoft.Extensions.AI.OpenAI` (version 10.3.0)
  - Removed: `Microsoft.SemanticKernel` dependency
  - Target: `net10.0`

### 3. Documentation Updates
- **README.md** (scenario): Completely rewritten with MEAI focus
  - Explains the `IChatClient` pattern with code example
  - Notes the difference between MEAI (lightweight abstraction) and SK (orchestration framework)
  
- **Root README.md:** Updated Scenario 4 description
- **docs/USER_MANUAL.md:** Updated all references
- **docs/GETTING_STARTED.md:** Updated all references
- **docs/ARCHITECTURE.md:** Updated architecture table

## Why MEAI Instead of Semantic Kernel?

**Microsoft.Extensions.AI** is:
- **Lightweight** — minimal dependencies, thin abstraction layer
- **Provider-agnostic** — works with OpenAI, Azure OpenAI, Ollama, etc.
- **Modern .NET** — built for .NET 10+ with native async patterns
- **Focused** — designed for direct chat completion calls, not full orchestration

**Semantic Kernel** is designed for multi-agent orchestration, planning, and complex workflows. For a simple "ask AI → speak response" pattern, MEAI is a better fit.

## Impact

- Scenario 4 now demonstrates MEAI's `IChatClient` abstraction
- Same flow: user prompt → AI response → VibeVoice TTS → audio playback
- Same prerequisites: OPENAI_API_KEY, VibeVoice backend running
- Cleaner, simpler code with fewer dependencies

## Build Status

✅ **Verified:** `dotnet build` succeeds with no errors (1 package version warning is expected)

## References

- [Microsoft.Extensions.AI NuGet Package](https://www.nuget.org/packages/Microsoft.Extensions.AI.OpenAI/)
- Pattern follows MEAI's `IChatClient` abstraction for chat completion
