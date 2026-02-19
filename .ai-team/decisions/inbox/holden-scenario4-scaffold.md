# Decision: Scenario 4 Aspire Scaffold

**Date:** 2026-02-19
**By:** Holden (Lead)
**Requested by:** Bruno Capuano

## What

Restructured `src/scenario-04-meai/` from a standalone C# console app into an Aspire-orchestrated fullstack application for real-time voice conversation, following the exact pattern from scenario-02-fullstack.

## Changes

1. **Preserved old code** — `Program.cs` → `.bak`, `SpeechPlugin.cs` → `.bak`
2. **VoiceLabs.ConversationHost/** — Aspire AppHost (SDK 13.0.0, Aspire.Hosting.Python 13.1.1)
3. **VoiceLabs.ServiceDefaults/** — Already existed, verified identical to scenario-02
4. **VoiceLabs.slnx** — Solution referencing ConversationHost, ServiceDefaults, ConversationWeb
5. **Directory stubs** — `backend/` (for Naomi), `VoiceLabs.ConversationWeb/` (for Alex)
6. **`.aspire/settings.json`** — Points to ConversationHost project

## Why

- Consistent Aspire orchestration pattern across scenarios
- Enables real-time voice conversation with Python backend + Blazor frontend
- Clean separation: Holden scaffolds, Naomi builds backend, Alex builds frontend

## Impact

- Old MEAI console code preserved as `.bak` files
- Solution won't build until Alex creates the ConversationWeb project
- Backend directory ready for Naomi to populate with FastAPI conversation API
