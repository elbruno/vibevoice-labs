# Scenario 1: Simple VibeVoice TTS Demo

A minimal Python script demonstrating text-to-speech with VibeVoice-Realtime-0.5B.

## Prerequisites

- Python 3.10 or later
- GPU with CUDA recommended (works on CPU but slower)

## Setup

1. Create a virtual environment:
   ```bash
   python -m venv venv
   source venv/bin/activate  # Linux/Mac
   venv\Scripts\activate     # Windows
   ```

2. Install dependencies:
   ```bash
   pip install -r requirements.txt
   ```

## Usage

Run the script:
```bash
python main.py
```

The script will:
1. Load the VibeVoice-Realtime-0.5B model (downloads on first run)
2. Generate speech from the sample text
3. Save the output as `output.wav`

## Customization

Edit `main.py` to:
- Change the input text
- Try different voices (see the commented examples)
- Experiment with streaming generation

## Available Voices

English voices included in this scenario:

| Speaker | Gender | Description |
|---------|--------|-------------|
| Carter  | Male   | Clear American English (default) |
| Davis   | Male   | American English |
| Emma    | Female | American English |
| Frank   | Male   | American English |
| Grace   | Female | American English |
| Mike    | Male   | American English |

To change voices, edit the `SPEAKER_NAME` variable in `main.py`.
