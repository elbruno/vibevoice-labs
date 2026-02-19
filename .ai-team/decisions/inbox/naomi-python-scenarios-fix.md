# Decision: Python Scenarios Voice ID Consistency

**By:** Naomi (Backend Dev)  
**Date:** 2026-02-19  
**Status:** Implemented

## What Changed

Fixed voice ID inconsistencies across all Python scenarios to match actual VibeVoice voice preset files.

### Scenario 1 (Simple Script)
- **Before:** README listed incorrect voices (EN-Default, EN-US, EN-BR, DE, FR, etc.)
- **After:** Updated to actual available English voices: Carter, Davis, Emma, Frank, Grace, Mike

### Scenario 2 (FastAPI Backend)
- **Before:** Default `voice_id` was "en-US-Aria" (doesn't exist)
- **After:** Changed to "en-carter" matching VOICES_REGISTRY
- **README:** Updated all examples to use correct voice IDs (en-carter, en-emma)

### Scenario 5 (Batch Processing)
- **Before:** README showed voice codes like `EN-Default`, `EN-US`, `FR`, `ES`
- **After:** Updated to match VOICE_PRESETS keys: `carter`, `emma`, `fr-woman`, `es-woman`, etc.
- **Sample files:** Fixed hello-french.txt (`FR` → `fr-woman`), hello-spanish.txt (`ES` → `es-woman`)

## Voice ID Conventions by Scenario

| Scenario | Voice ID Format | Example | Why |
|----------|----------------|---------|-----|
| Backend API (Scenario 2) | Kebab-case with lang prefix | `en-carter`, `en-emma`, `en-grace` | REST API convention |
| Batch CLI (Scenario 5) | Lowercase with hyphen | `carter`, `emma`, `fr-woman`, `es-woman` | CLI simplicity |
| Simple script (Scenario 1) | Capitalized name | `Carter`, `Emma`, `Grace` | Beginner-friendly |

All three map to the same `.pt` preset files:
- `en-Carter_man.pt`
- `en-Emma_woman.pt`
- `fr-Spk1_woman.pt`
- `sp-Spk0_woman.pt`

## Voice Registry

**English voices** (all scenarios):
- carter / en-carter → en-Carter_man.pt
- davis / en-davis → en-Davis_man.pt
- emma / en-emma → en-Emma_woman.pt
- frank / en-frank → en-Frank_man.pt
- grace / en-grace → en-Grace_woman.pt
- mike / en-mike → en-Mike_man.pt

**Other languages** (batch processing only):
- de-man → de-Spk0_man.pt
- de-woman → de-Spk1_woman.pt
- fr-man → fr-Spk0_man.pt
- fr-woman → fr-Spk1_woman.pt
- es-man → sp-Spk1_man.pt
- es-woman → sp-Spk0_woman.pt

## Impact

✅ **Fixed:** Backend default voice now works without errors  
✅ **Fixed:** Sample text files in batch processing use correct voice codes  
✅ **Fixed:** All READMEs accurate for copy-paste usage  
✅ **Verified:** All Python files compile and imports resolve  

No breaking changes to existing code — only README and sample file updates.
