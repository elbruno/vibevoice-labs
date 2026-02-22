"""
VibeVoice-Realtime-0.5B ONNX — Python Inference Example
=========================================================
Demonstrates how to load and run the ONNX-exported VibeVoice model
using onnxruntime. Downloads models from Hugging Face Hub automatically.

Requirements:
    pip install onnxruntime numpy huggingface_hub soundfile

Usage:
    python example_inference.py --text "Hello world" --voice Carter --output output.wav
"""

import argparse
import json
import os
import sys
import time

import numpy as np

# Suppress onnxruntime warnings for cleaner output
os.environ["ORT_LOG_LEVEL"] = "WARNING"


def download_models(repo_id: str, cache_dir: str | None = None) -> str:
    """Download all model files from Hugging Face Hub. Returns local directory path."""
    from huggingface_hub import snapshot_download

    print(f"📥 Downloading models from {repo_id}...")
    local_dir = snapshot_download(
        repo_id=repo_id,
        cache_dir=cache_dir,
        allow_patterns=["*.onnx", "*.json", "*.npy", "voices/**"],
    )
    print(f"   ✅ Models cached at: {local_dir}")
    return local_dir


def load_sessions(models_dir: str) -> tuple:
    """Load all three ONNX inference sessions."""
    import onnxruntime as ort

    providers = ["CPUExecutionProvider"]
    # Prefer GPU if available
    if "CUDAExecutionProvider" in ort.get_available_providers():
        providers.insert(0, "CUDAExecutionProvider")
    elif "DmlExecutionProvider" in ort.get_available_providers():
        providers.insert(0, "DmlExecutionProvider")

    opts = ort.SessionOptions()
    opts.graph_optimization_level = ort.GraphOptimizationLevel.ORT_ENABLE_ALL

    text_encoder = ort.InferenceSession(
        os.path.join(models_dir, "text_encoder.onnx"), opts, providers=providers
    )
    diffusion = ort.InferenceSession(
        os.path.join(models_dir, "diffusion_step.onnx"), opts, providers=providers
    )
    decoder = ort.InferenceSession(
        os.path.join(models_dir, "acoustic_decoder.onnx"), opts, providers=providers
    )

    return text_encoder, diffusion, decoder


def simple_tokenize(text: str, tokenizer_path: str) -> np.ndarray:
    """
    Simple BPE tokenization using the tokenizer.json vocabulary.
    For production use, consider using the `tokenizers` library.
    """
    with open(tokenizer_path, "r", encoding="utf-8") as f:
        tokenizer_data = json.load(f)

    vocab = tokenizer_data.get("model", {}).get("vocab", tokenizer_data.get("vocab", {}))
    if not vocab:
        raise ValueError("Could not find vocabulary in tokenizer.json")

    # Simple word-level lookup with fallback to character-level
    tokens = []
    for word in text.split():
        word_with_space = "Ġ" + word  # BPE convention: leading space as Ġ
        if word_with_space in vocab:
            tokens.append(vocab[word_with_space])
        elif word in vocab:
            tokens.append(vocab[word])
        else:
            for char in word:
                char_token = "Ġ" + char if not tokens else char
                if char_token in vocab:
                    tokens.append(vocab[char_token])
                elif char in vocab:
                    tokens.append(vocab[char])

    return np.array([tokens], dtype=np.int64)


def load_voice_preset(voices_dir: str, voice_name: str) -> np.ndarray:
    """Load a voice preset from .npy files."""
    manifest_path = os.path.join(voices_dir, "manifest.json")

    if os.path.exists(manifest_path):
        with open(manifest_path, "r") as f:
            manifest = json.load(f)
        voices = manifest.get("voices", {})
        if voice_name in voices:
            files = voices[voice_name].get("files", {})
            # Load first available tensor (usually speaker_embedding)
            for tensor_name, file_path in files.items():
                npy_path = os.path.join(voices_dir, file_path)
                if os.path.exists(npy_path):
                    return np.load(npy_path).reshape(1, -1).astype(np.float32)

    # Try direct file lookup
    direct_path = os.path.join(voices_dir, voice_name, "speaker_embedding.npy")
    if os.path.exists(direct_path):
        return np.load(direct_path).reshape(1, -1).astype(np.float32)

    # Fallback: zero conditioning
    print(f"   ⚠️  Voice '{voice_name}' not found, using default conditioning")
    return np.zeros((1, 256), dtype=np.float32)


def run_pipeline(
    text_encoder,
    diffusion,
    decoder,
    token_ids: np.ndarray,
    voice_conditioning: np.ndarray,
    num_steps: int = 5,
    cfg_scale: float = 1.5,
    seed: int = 42,
) -> np.ndarray:
    """
    Run the full TTS inference pipeline.

    Steps:
        1. Text encoder: tokens → hidden states
        2. Diffusion loop: noise → clean latents (N denoising steps)
        3. Acoustic decoder: latents → waveform
    """
    # --- Step 1: Text encoding ---
    attention_mask = np.ones_like(token_ids, dtype=np.int64)
    encoder_inputs = {"input_ids": token_ids, "attention_mask": attention_mask}

    # TODO: Verify input/output names match exported model (use Netron to inspect)
    hidden_states = text_encoder.run(None, encoder_inputs)[0]
    print(f"   📝 Text encoded: shape {hidden_states.shape}")

    # --- Step 2: Diffusion denoising loop ---
    rng = np.random.RandomState(seed)
    latent_shape = (1, 1024, 50)  # TODO: verify after export
    latents = rng.randn(*latent_shape).astype(np.float32)

    # Linear beta schedule
    beta_start, beta_end, num_train = 0.00085, 0.012, 1000
    betas = np.linspace(beta_start, beta_end, num_train, dtype=np.float32)
    alphas = 1.0 - betas
    alphas_cumprod = np.cumprod(alphas)

    # Compute evenly-spaced timesteps (descending)
    step_size = num_train / num_steps
    timesteps = [int(round(num_train - 1 - i * step_size)) for i in range(num_steps)]
    timesteps = [max(t, 0) for t in timesteps]

    for i, t in enumerate(timesteps):
        # Run diffusion step
        diff_inputs = {
            "latent_sample": latents,
            "encoder_hidden_states": hidden_states,
            "voice_conditioning": voice_conditioning,
            "timestep": np.array([t], dtype=np.int64),
        }
        noise_pred = diffusion.run(None, diff_inputs)[0]

        # CFG: run unconditional pass if scale > 1
        if cfg_scale > 1.0:
            uncond_inputs = {**diff_inputs, "voice_conditioning": np.zeros_like(voice_conditioning)}
            uncond_pred = diffusion.run(None, uncond_inputs)[0]
            noise_pred = uncond_pred + cfg_scale * (noise_pred - uncond_pred)

        # DDPM step
        alpha_t = alphas_cumprod[t]
        alpha_prev = alphas_cumprod[t - 1] if t > 0 else 1.0
        x0_pred = (latents - np.sqrt(1 - alpha_t) * noise_pred) / np.sqrt(alpha_t)
        x0_pred = np.clip(x0_pred, -1, 1)
        latents = np.sqrt(alpha_prev) * x0_pred + np.sqrt(1 - alpha_prev) * noise_pred

        print(f"   🔄 Diffusion step {i+1}/{num_steps} (t={t})")

    # --- Step 3: Acoustic decoding ---
    decoder_inputs = {"latent_input": latents}
    audio = decoder.run(None, decoder_inputs)[0]
    audio = np.clip(audio.flatten(), -1, 1)
    print(f"   🔊 Audio decoded: {len(audio)} samples ({len(audio)/24000:.2f}s @ 24kHz)")

    return audio


def save_wav(path: str, samples: np.ndarray, sample_rate: int = 24000):
    """Save audio samples to a WAV file."""
    try:
        import soundfile as sf
        sf.write(path, samples, sample_rate)
    except ImportError:
        # Fallback: write raw WAV manually
        import struct
        import wave

        pcm = (samples * 32767).astype(np.int16)
        with wave.open(path, "w") as wf:
            wf.setnchannels(1)
            wf.setsampwidth(2)
            wf.setframerate(sample_rate)
            wf.writeframes(pcm.tobytes())


def main():
    parser = argparse.ArgumentParser(description="VibeVoice ONNX Inference Example")
    parser.add_argument("--text", type=str, default="Hello! This is VibeVoice running with ONNX Runtime.",
                        help="Text to synthesize")
    parser.add_argument("--voice", type=str, default="Carter",
                        help="Voice preset name (Carter, Davis, Emma, Frank, Grace, Mike)")
    parser.add_argument("--output", type=str, default="output.wav",
                        help="Output WAV file path")
    parser.add_argument("--models-dir", type=str, default=None,
                        help="Local path to models (skips HF download)")
    parser.add_argument("--repo-id", type=str, default="elbruno/VibeVoice-Realtime-0.5B-ONNX",
                        help="Hugging Face repo ID")
    parser.add_argument("--steps", type=int, default=5,
                        help="Number of diffusion denoising steps")
    parser.add_argument("--seed", type=int, default=42,
                        help="Random seed for reproducibility")
    args = parser.parse_args()

    print("🎙️  VibeVoice ONNX — Python Inference Example")
    print()

    # Download or locate models
    if args.models_dir:
        models_dir = args.models_dir
    else:
        models_dir = download_models(args.repo_id)

    # Load ONNX sessions
    print("🧠 Loading ONNX models...")
    t0 = time.time()
    text_encoder, diffusion, decoder = load_sessions(models_dir)
    print(f"   ✅ Models loaded in {time.time() - t0:.2f}s")
    print()

    # Tokenize
    print(f"📝 Text: \"{args.text}\"")
    print(f"🗣️  Voice: {args.voice}")
    tokenizer_path = os.path.join(models_dir, "tokenizer.json")
    token_ids = simple_tokenize(args.text, tokenizer_path)
    print(f"   Tokens: {token_ids.shape[1]} IDs")
    print()

    # Load voice preset
    voices_dir = os.path.join(models_dir, "voices")
    voice_conditioning = load_voice_preset(voices_dir, args.voice)

    # Run pipeline
    print("🎵 Running inference pipeline...")
    t0 = time.time()
    audio = run_pipeline(
        text_encoder, diffusion, decoder,
        token_ids, voice_conditioning,
        num_steps=args.steps, seed=args.seed,
    )
    inference_time = time.time() - t0
    print()

    # Save
    save_wav(args.output, audio)
    duration = len(audio) / 24000
    print(f"✅ Saved: {args.output}")
    print(f"   Duration: {duration:.2f}s | Inference: {inference_time:.2f}s | RTF: {inference_time/duration:.2f}x")


if __name__ == "__main__":
    main()
