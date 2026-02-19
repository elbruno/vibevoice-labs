# Scenario 6 — Real-Time Streaming TTS Demo

## What This Demo Shows

VibeVoice's **streaming** TTS capability via `generate_stream()`.  
Audio chunks are played through your speaker **the moment they are ready**, so you hear the first words in roughly **~300 ms** — no need to wait for the entire sentence to be synthesized.

Key metrics printed at the end:

| Metric | What it means |
|---|---|
| **Time to first chunk** | How fast audio starts playing (~300 ms) |
| **Total generation time** | Wall-clock time for the full text |
| **Real-time factor** | Values > 1× mean audio is generated faster than real-time |

## Prerequisites

| Requirement | Details |
|---|---|
| Python | 3.11 or newer |
| GPU | NVIDIA GPU with CUDA recommended (CPU works but is slower) |
| Audio output | Speakers or headphones connected to the machine |
| Model | `microsoft/VibeVoice-Realtime-0.5B` (auto-downloaded on first run, ~1 GB) |
| **Python environment** | Set up at the repo root (see [Getting Started](../../docs/GETTING_STARTED.md#python-environment-setup-one-time)) |

## How to Run

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

Then run the streaming demo:
```bash
cd src/scenario-06-streaming-realtime
python stream_tts.py
```

## What to Expect

1. The model loads (~5-15 s on first run while downloading).
2. Audio playback begins almost instantly (~300 ms) — you'll hear the first words before the full text is generated.
3. A progress bar in the terminal shows chunks arriving in real-time.
4. A performance summary prints when generation finishes.
5. The full audio is saved to `stream_output.wav`.

## Troubleshooting

| Problem | Solution |
|---|---|
| **No audio device / `sounddevice` error** | The script automatically falls back to file-only mode and saves `stream_output.wav`. You can play it with any media player. |
| **CUDA out of memory** | Close other GPU-intensive applications, or the model will fall back to CPU automatically. |
| **`ModuleNotFoundError: vibevoice_realtime`** | Make sure you installed dependencies: `pip install -r requirements.txt` |
| **Choppy / slow playback** | Ensure you're using a CUDA-capable GPU. CPU inference may not keep up with real-time playback. |
| **PortAudio error on Linux** | Install the system library: `sudo apt install libportaudio2` |
