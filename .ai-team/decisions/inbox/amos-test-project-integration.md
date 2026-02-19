# Decision: Integrate Test Project into Solution

**By:** Amos (Tester)
**Date:** 2026-02-19

## What
- Added `VoiceLabs.Web.Tests` to `VoiceLabs.slnx`
- Enabled `ProjectReference` from test project to `VoiceLabs.Web`

## Why
The test project was scaffolded but not wired into the solution, so `dotnet build` and `dotnet test` on the solution would not include tests. The ProjectReference was commented out waiting for VoiceLabs.Web to exist — it now exists and builds.

## Impact
- `dotnet test` on the solution now runs all 8 C# unit tests
- Future: C# tests should be refactored to use actual models from `VoiceLabs.Web.Services` instead of internal placeholder records
