# Test Results — Full Project Verification

**Date:** 2026-02-19  
**By:** Amos (Tester)  
**Requested by:** Bruno Capuano

## Summary

Executed comprehensive test run across all scenarios. **All active tests pass, all buildable projects compile clean.** No code fixes were required.

## Test Results

### C# Tests (Scenario 02)
- **Project:** `VoiceLabs.Web.Tests`
- **Framework:** xUnit, .NET 10
- **Result:** ✅ 8/8 tests passed
- **Details:** All HTTP client mocking tests for TtsService (health, voices, TTS endpoints) passed without errors

### Python Tests (Scenario 02)
- **Location:** `src/scenario-02-fullstack/python-api/tests/test_api.py`
- **Framework:** pytest with anyio
- **Result:** ✅ 12/12 tests collected, all skipped
- **Reason:** Backend FastAPI app not yet implemented (Naomi's pending work)
- **Note:** Tests will automatically activate once `main.py` or `app/main.py` exists with FastAPI app

## Build Verification

### ✅ Clean Builds
1. **Scenario 02 (Fullstack):** `VoiceLabs.slnx` — 4 projects, 0 errors, 0 warnings
2. **Scenario 03 (C# Console):** `VoiceLabs.Console.csproj` — clean build
3. **Python Scripts:** All syntax valid
   - `scenario-01-simple/main.py`
   - `scenario-05-batch-processing/batch_tts.py`
   - `scenario-06-streaming-realtime/stream_tts.py`

### ⚠️ Build with Warnings
- **Scenario 04 (Semantic Kernel):** `VoiceLabs.SK.csproj` — builds successfully
  - 2 NU1904 warnings: Microsoft.SemanticKernel.Core 1.54.0 has known vulnerability GHSA-2ww3-72rp-wpp4
  - **Recommendation:** Upgrade to newer version when available

### ❌ Cannot Build (Not a Code Issue)
- **Scenario 07 (MAUI Mobile):** `VoiceLabs.Maui.csproj`
  - Error: NETSDK1147 — requires `maui-android` workload
  - **Cause:** Workload not installed on this machine
  - **Resolution:** Run `dotnet workload restore` on deployment environment
  - **Note:** This is an environment issue, not a code defect

## Conclusions

1. **All working code is healthy** — no test failures, no compilation errors
2. **Python backend pending** — once Naomi implements FastAPI endpoints, 12 pytest tests will automatically activate
3. **Security advisory** — Scenario 04 should upgrade SemanticKernel package when Bruno has time
4. **MAUI scenario** — requires Android workload installation on deployment/test machines

## No Action Required

All tests that can run are passing. All code that should build is building. Project is in good health.
