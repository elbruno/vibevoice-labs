# Scenario 05 — Batch TTS Processing

A Python CLI tool that reads `.txt` files from an input directory and generates corresponding `.wav` audio files using the VibeVoice-Realtime-0.5B model. No HTTP server needed — talks to the model directly!

## What This Scenario Does

- Scans a directory for `.txt` files
- Generates a `.wav` file for each text file
- Supports YAML front-matter for per-file voice overrides
- Optional parallel processing for faster batch jobs
- Displays a progress bar and summary report

## Prerequisites

- **Python 3.11+**
- **GPU recommended** (CUDA-capable) — works on CPU too, but slower

## Setup

```bash
cd src/scenario-05-batch-processing
pip install -r requirements.txt
```

## Usage

### Basic — process sample texts with default voice

```bash
python batch_tts.py
```

### Custom input/output directories

```bash
python batch_tts.py --input-dir ./my-texts --output-dir ./my-audio
```

### Use a different default voice

```bash
python batch_tts.py --voice FR
```

### Parallel processing (4 files at once)

```bash
python batch_tts.py --parallel 4
```

### All options together

```bash
python batch_tts.py --input-dir ./my-texts --output-dir ./results --voice EN-US --parallel 2
```

## YAML Front-Matter

Any `.txt` file can include YAML front-matter to override the voice for that specific file:

```text
---
voice: FR
---
Bonjour le monde! Ceci est un texte en français.
```

If no front-matter is present, the `--voice` flag value (or its default `EN-Default`) is used.

### Available Voices

| Speaker Code | Language |
|---|---|
| `EN-Default` | English (Standard) |
| `EN-US` | American English |
| `EN-BR` | British English |
| `EN-AU` | Australian English |
| `DE` | German |
| `FR` | French |
| `IT` | Italian |
| `ES` | Spanish |
| `PT` | Portuguese |
| `NL` | Dutch |
| `PL` | Polish |
| `JP` | Japanese |
| `KR` | Korean |

## Output Structure

```
output/
├── hello-english.wav
├── hello-french.wav
├── hello-spanish.wav
├── story-english.wav
└── technical-demo.wav
```

Each `.wav` file is named to match its source `.txt` file. Audio is saved at 24kHz sample rate.

## Sample Files Included

| File | Language | Description |
|---|---|---|
| `hello-english.txt` | English (default) | Simple greeting |
| `hello-french.txt` | French (via front-matter) | French greeting |
| `hello-spanish.txt` | Spanish (via front-matter) | Spanish greeting |
| `story-english.txt` | English (default) | Longer narrative paragraph |
| `technical-demo.txt` | English (default) | Technical description of VibeVoice |
