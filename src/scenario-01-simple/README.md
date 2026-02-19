# Scenario 1: Simple VibeVoice TTS Demo

A minimal Python script demonstrating text-to-speech with VibeVoice-Realtime-0.5B.

## Prerequisites

- Python 3.10 or later
- GPU with CUDA recommended (works on CPU but slower)
- **Python environment set up at the repo root** (see [Getting Started](../../docs/GETTING_STARTED.md#python-environment-setup-one-time))

## Setup

From the repository root, activate the shared virtual environment:

**Windows:**
```powershell
.\.venv\Scripts\Activate.ps1
```

**Linux/macOS:**
```bash
source .venv/bin/activate
```

If you haven't installed dependencies yet:
```bash
pip install -r requirements.txt
```

## Usage

### Basic Demo (English)

Run the basic script:
```bash
python main.py
```

The script will:
1. Load the VibeVoice-Realtime-0.5B model (downloads on first run)
2. Generate speech from the sample text
3. Save the output as `output.wav`

### Multilingual Demo (Spanish and more)

Run the multilingual script:
```bash
python main_multilingual.py
```

This script demonstrates VibeVoice's multilingual capabilities. By default it generates Spanish audio, but supports 10 languages.

To change the language, edit `main_multilingual.py`:
```python
LANGUAGE = "sp"    # Spanish (default)
LANGUAGE = "en"    # English
LANGUAGE = "fr"    # French
LANGUAGE = "de"    # German
# ... and more
```

To generate audio in ALL supported languages at once, uncomment the last line:
```python
generate_all_languages()
```

## Customization

Edit `main.py` to:
- Change the input text
- Try different voices (see the commented examples)
- Experiment with streaming generation

Edit `main_multilingual.py` to:
- Change the language (`LANGUAGE` variable)
- Select different voices (`VOICE_INDEX` variable)
- Use custom text (`CUSTOM_TEXT` variable)
- Generate all languages at once

## Available Voices

### English voices

| Speaker | Gender | Description |
|---------|--------|-------------|
| Carter  | Male   | Clear American English (default) |
| Davis   | Male   | American English |
| Emma    | Female | American English |
| Frank   | Male   | American English |
| Grace   | Female | American English |
| Mike    | Male   | American English |

### Multilingual Voices

VibeVoice supports 10 languages with exploration-mode quality:

| Language   | Code | Voice 1      | Voice 2      |
|------------|------|--------------|--------------|
| English    | en   | Carter (M)   | + 5 more     |
| Spanish    | sp   | Spk0 (F)     | Spk1 (M)     |
| German     | de   | Spk0 (M)     | Spk1 (F)     |
| French     | fr   | Spk0 (M)     | Spk1 (F)     |
| Italian    | it   | Spk0 (F)     | Spk1 (M)     |
| Portuguese | pt   | Spk0 (F)     | Spk1 (M)     |
| Japanese   | jp   | Spk0 (M)     | Spk1 (F)     |
| Korean     | kr   | Spk0 (F)     | Spk1 (M)     |
| Dutch      | nl   | Spk0 (M)     | Spk1 (F)     |
| Polish     | pl   | Spk0 (M)     | Spk1 (F)     |

> **Note:** Non-English languages are in "exploration mode" and may have varying quality compared to English output.

To change voices:
- In `main.py`: edit the `SPEAKER_NAME` variable
- In `main_multilingual.py`: edit `LANGUAGE` and `VOICE_INDEX` variables
