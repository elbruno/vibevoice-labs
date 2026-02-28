# Issue #17 Triage: Apply security, performance & CI lessons from LocalEmbeddings v1.1.0 audit

**Issue:** https://github.com/elbruno/ElBruno.VibeVoiceTTS/issues/17  
**Audit Source:** elbruno/elbruno.localembeddings#38 ([audit lessons](https://github.com/elbruno/elbruno.localembeddings/issues/38))  
**Related PR:** LocalEmbeddings #37 ([Security & Performance improvements](https://github.com/elbruno/elbruno.localembeddings/pull/37))

---

## Executive Summary

ElBruno.VibeVoiceTTS is a **mature, well-audited codebase** that has already implemented most critical security and performance hardening from the LocalEmbeddings audit. Several audit items are **NOT APPLICABLE** to this library (e.g., performance optimizations for pure-inference code; CI lessons for pre-release workflows).

**Status:**
- ✅ **COMPLETE:** 2 major security controls (path traversal, file integrity)
- ✅ **COMPLETE:** 1 CI best practice (SkippableFact usage)
- ⚠️ **PARTIAL:** 4 items need work (input validation depth, HTTPS enforcement, performance ops, CI config)
- ❌ **NOT APPLICABLE:** 3 items (array pooling in library code, git tag version stripping, simple task routing—not relevant to pure-ONNX inference)

---

## 🔒 Security Checklist

### ✅ DONE: Path traversal prevention
**Status:** COMPLETE  
**Evidence:** `VoicePresetLoader.ValidatePathWithinDirectory()` (VoicePresetLoader.cs:52–59)
- ✅ Validates paths with `Path.GetFullPath()` + `StartsWith()` check
- ✅ Rejects paths escaping base directory
- ✅ Tests in PathTraversalTests.cs (rejects "..", absolute paths)
- ✅ Called before all file reads in `GetVoicePreset()` (line 40)

### ✅ DONE: File integrity checks
**Status:** COMPLETE  
**Evidence:** `VoicePresetLoader.ReadNpyFile()` (VoicePresetLoader.cs:148–194)
- ✅ Validates .npy magic bytes (0x93 "NUMPY")
- ✅ Validates dtype (float32/float64 only; rejects unknown types)
- ✅ Validates file size before loading
- ✅ Tests in PathTraversalTests.cs (rejects non-.npy files)

### ⚠️ PARTIAL: Cross-platform file name validation
**Status:** NEEDS WORK  
**Finding:** No hardcoded `InvalidFileNameChars` validation exists
- ❌ Code doesn't validate untrusted file names (voice names come from HuggingFace manifest)
- ❌ Voice preset directory names (e.g., "en-Carter_man") are discovered from disk, not validated
- 📝 **Recommendation:** Add validation in `DiscoverVoicesFromDirectory()` or enforce name format regex

### ⚠️ PARTIAL: Input validation at API boundaries
**Status:** NEEDS WORK  
**Finding:** VibeVoiceOptions validates some parameters but misses others
- ✅ `HuggingFaceRepo` validated with regex (line 141)
- ✅ `DiffusionSteps`, `CfgScale`, `SampleRate`, `GpuDeviceId` validated with range checks
- ❌ `ModelPath` is NOT validated (accepts any user-supplied path; relies only on Directory.Exists)
- ❌ Text input to GenerateAudio validated only for null/whitespace; no length limit
- 📝 **Recommendation:** Add `ModelPath` validation (prevent traversal); add text length limit (~5000 chars)

### ⚠️ PARTIAL: URL validation — HTTPS-only enforcement
**Status:** NEEDS WORK  
**Finding:** HuggingFace downloader uses HTTPS by default but not explicitly enforced
- ℹ️ Downloads delegated to `ElBruno.HuggingFace.Downloader` NuGet dependency (external)
- ❌ No explicit HTTPS validation in this library
- ℹ️ Not a direct concern (HuggingFace API enforces HTTPS), but good practice
- 📝 **Recommendation:** Document HTTPS assumption; or wrap downloader with validation

---

## ⚡ Performance Checklist

### ❌ NOT APPLICABLE: TensorPrimitives / SIMD
**Status:** N/A  
**Finding:** This is a pure ONNX inference wrapper, not a vector math library
- ℹ️ Math operations are delegated to ONNX Runtime (GPU/CPU optimized by external library)
- ℹ️ No custom dot product, cosine similarity, or L2 norm implementations in this library
- 🎯 **VERDICT:** Inapplicable—no opportunity to optimize here

### ❌ NOT APPLICABLE: Span<T> / ArrayPool allocations in loops
**Status:** N/A  
**Finding:** Autoregressive loop allocates float[] arrays per frame, but is I/O-bound, not compute-bound
- ℹ️ OnnxInferencePipeline.GenerateAudio() (line 149–184) allocates `allLatents` list
- ℹ️ Typically runs 50–200 frames; each frame calls ONNX models (CPU/GPU-dominated)
- ℹ️ Memory allocation is negligible compared to model inference latency
- 🎯 **VERDICT:** Premature optimization; not a bottleneck in typical workloads

### ❌ NOT APPLICABLE: BenchmarkDotNet benchmarks
**Status:** N/A  
**Finding:** No need for microbenchmarks on pure inference wrapper
- ℹ️ Performance is dominated by ONNX Runtime + hardware
- ℹ️ Optimizations should target ONNX provider selection, not C# code
- 🎯 **VERDICT:** Inapplicable—end-to-end perf testing (E2E) is more relevant

### ⚠️ PARTIAL: Top-K search optimization
**Status:** NEEDS WORK  
**Finding:** EOS classification uses simple threshold (no ranking)
- ℹ️ `RunEosClassifier()` (OnnxInferencePipeline.cs, inferred) outputs single score
- ❌ No beam search, no ranking, no pruning strategies
- 📝 **Note:** This is an architectural choice (deterministic generation), not a bug

---

## 🐧 CI / Linux Checklist

### ✅ DONE: SkippableFact for platform-conditional tests
**Status:** COMPLETE  
**Evidence:** IntegrationTests.cs uses `[SkippableFact]` (line 14, 34, 48)
- ✅ IntegrationTests use `Skip.IfNot()` correctly (not Skip.If)
- ✅ Tests marked with `[SkippableFact]`, not `[Fact]`
- ✅ Will skip cleanly on Linux if models not available

### ⚠️ PARTIAL: Cross-platform file name chars in test assertions
**Status:** NEEDS WORK  
**Finding:** Tests use platform-conditional checks but no hardcoded validation
- ℹ️ PathTraversalTests.cs:30 checks `OperatingSystem.IsWindows()` for test data
- ❌ No hardcoded `InvalidFileNameChars` constant in tests
- 📝 **Recommendation:** Match production code (add hardcoded list if needed)

### ❌ NOT APPLICABLE: Git tag format version stripping in publish workflow
**Status:** N/A / NEEDS MINOR FIX  
**Finding:** Publish.yml does strip "v" prefix but doesn't validate format
- ✅ Strips leading "v" on line 35: `VERSION="${VERSION#v}"`
- ❌ Does NOT validate tag format before build (no early exit on malformed tags)
- ❌ Does NOT handle "v.1.2.3" typo (extra dot after 'v')
- 🎯 **VERDICT:** Not a blocker (csproj version takes precedence), but add validation for safety

---

## 🤖 Squad / AI Team Rule

### ⚠️ PARTIAL: Route simple tasks to fast/cheap models
**Status:** NEEDS WORK  
**Finding:** No explicit guidance in squad charter or .copilot config
- ❌ No cost-first directive for simple tasks (typo fixes, changelogs, version bumps)
- 📝 **Recommendation:** Document model selection in .squad/agents/drummer/charter.md

---

## Work Items by Priority

### 🔴 HIGH PRIORITY: Security

1. **[Alex/Naomi] Input validation: ModelPath + text length limits**
   - File: `src/ElBruno.VibeVoiceTTS/VibeVoiceOptions.cs`
   - Add `ModelPath` traversal check (like VoicePresetLoader does)
   - Add text input length limit (e.g., 5000 chars)
   - Tests: `src/ElBruno.VibeVoiceTTS.Tests/OptionsValidationTests.cs`
   - Effort: ~2 hours | Cost: Low

2. **[Naomi] File name validation for voice presets**
   - File: `src/ElBruno.VibeVoiceTTS/Pipeline/VoicePresetLoader.cs`
   - Add hardcoded `InvalidFileNameChars` constant (cross-platform)
   - Validate voice names in `DiscoverVoicesFromDirectory()` + `GetVoicePreset()`
   - Tests: Add to `PathTraversalTests.cs`
   - Effort: ~1.5 hours | Cost: Low

3. **[Naomi] Document HTTPS enforcement for HuggingFace downloads**
   - File: `src/ElBruno.VibeVoiceTTS/ModelManager.cs` (comment/docs)
   - Ensure HuggingFace.Downloader uses HTTPS by default
   - If needed, wrap with explicit validation
   - Tests: None required (documentation only)
   - Effort: ~1 hour | Cost: Low

### 🟡 MEDIUM PRIORITY: CI/DevOps

4. **[Naomi] Fix squad-ci.yml to run actual build/test**
   - File: `.github/workflows/squad-ci.yml`
   - Replace echo stub with `dotnet test`
   - Effort: ~30 min | Cost: Minimal

5. **[Naomi] Add version format validation to publish.yml**
   - File: `.github/workflows/publish.yml`
   - Add validation step before build (reject "v.1.2.3" typos)
   - Effort: ~30 min | Cost: Minimal

### 🟢 LOW PRIORITY: Documentation

6. **[Naomi] Document Squad model routing rules**
   - File: `.squad/agents/drummer/charter.md`
   - Add section: "Simple tasks → gpt-5-mini (cost first)"
   - Effort: ~15 min | Cost: Minimal

---

## Routing by Agent

| Agent | Work Items | Est. Effort |
|-------|-----------|-------------|
| **Naomi** (Backend/.NET) | #1, #2, #3, #4, #5, #6 | ~6 hours |
| **Alex** (Frontend) | — | — |
| **Amos** (Tests) | Supporting #1, #2 | ~1 hour |
| **Holden** (Architecture) | Review recommendations | Async |

---

## Recommended Implementation Order

1. **Security first (Issue #17 scope):**
   - Input validation (ModelPath + text length) — HIGH PRIORITY
   - File name validation (voice presets) — HIGH PRIORITY
   - HTTPS documentation — HIGH PRIORITY

2. **CI/DevOps (unblock future releases):**
   - Fix squad-ci.yml — MEDIUM PRIORITY
   - Add version validation to publish.yml — MEDIUM PRIORITY

3. **Documentation (knowledge transfer):**
   - Update drummer charter with model routing — LOW PRIORITY

---

## Not Applicable to This Codebase

The following audit items do **not apply** to ElBruno.VibeVoiceTTS:

1. **TensorPrimitives / SIMD optimizations**
   - Reason: Pure inference wrapper; no custom math
   - Action: None needed

2. **ArrayPool for temporary buffers**
   - Reason: Inference is GPU/CPU bound, not memory bound
   - Action: None needed

3. **Beam search / top-K ranking**
   - Reason: Deterministic generation architecture (not search-based)
   - Action: None needed

---

## Notes

- **LocalEmbeddings v1.1.0:** This repo is more mature than LocalEmbeddings was at v1.0 (path traversal, file validation already implemented)
- **HuggingFace Integration:** Relies on `ElBruno.HuggingFace.Downloader` NuGet; validate upstream compliance
- **Testing:** All changes should include unit tests in `ElBruno.VibeVoiceTTS.Tests`
- **Cross-platform:** Ensure Windows + Linux validation (tests use `OperatingSystem` checks correctly)

---

## Triage Sign-Off

**Triage Date:** 2025-01-16  
**Drummer:** GitHub Issues Manager  
**Team:** VibeVoice Labs  

Recommended for approval by Holden (architecture review) before starting work.
