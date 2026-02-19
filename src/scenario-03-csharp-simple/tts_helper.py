"""
VibeVoice TTS Helper Script
============================
Called by the C# console app to generate speech using the VibeVoice model directly.
This script is the "engine" — C# is the orchestrator.

Usage:
  python tts_helper.py --text "Hello world" --voice "Carter" --output "output.wav"

Arguments:
  --text    Text to synthesize
  --voice   Voice preset name (Carter, Davis, Emma, Frank, Grace, Mike)
  --output  Output WAV file path
"""

import argparse
import os
import sys
import glob
import copy
import torch

def main():
    parser = argparse.ArgumentParser(description="VibeVoice TTS Helper")
    parser.add_argument("--text", required=True, help="Text to synthesize")
    parser.add_argument("--voice", default="Carter", help="Voice preset name")
    parser.add_argument("--output", default="output.wav", help="Output WAV file path")
    args = parser.parse_args()

    # =========================================================================
    # Step 1: Download voice presets if needed
    # =========================================================================
    voices_dir = os.path.join(os.path.dirname(os.path.abspath(__file__)), "voices")

    if not os.path.exists(voices_dir) or not glob.glob(os.path.join(voices_dir, "*.pt")):
        print("Downloading voice presets (first run only)...")
        os.makedirs(voices_dir, exist_ok=True)
        import urllib.request

        base_url = "https://raw.githubusercontent.com/microsoft/VibeVoice/main/demo/voices/streaming_model"
        voice_files = [
            "en-Carter_man.pt", "en-Davis_man.pt", "en-Emma_woman.pt",
            "en-Frank_man.pt", "en-Grace_woman.pt", "en-Mike_man.pt",
        ]
        for vf in voice_files:
            dest = os.path.join(voices_dir, vf)
            if not os.path.exists(dest):
                print(f"  Downloading {vf}...")
                urllib.request.urlretrieve(f"{base_url}/{vf}", dest)
        print(f"  Done! Downloaded voice presets to {voices_dir}")

    # =========================================================================
    # Step 2: Load model and processor
    # =========================================================================
    print("Loading VibeVoice-Realtime-0.5B model...")

    from vibevoice.modular.modeling_vibevoice_streaming_inference import VibeVoiceStreamingForConditionalGenerationInference
    from vibevoice.processor.vibevoice_streaming_processor import VibeVoiceStreamingProcessor

    MODEL_NAME = "microsoft/VibeVoice-Realtime-0.5B"
    processor = VibeVoiceStreamingProcessor.from_pretrained(MODEL_NAME)

    device = "cuda" if torch.cuda.is_available() else "cpu"
    dtype = torch.bfloat16 if device == "cuda" else torch.float32
    attn_impl = "flash_attention_2" if device == "cuda" else "sdpa"

    try:
        model = VibeVoiceStreamingForConditionalGenerationInference.from_pretrained(
            MODEL_NAME, torch_dtype=dtype, attn_implementation=attn_impl,
            device_map=device if device == "cuda" else "cpu",
        )
    except Exception:
        model = VibeVoiceStreamingForConditionalGenerationInference.from_pretrained(
            MODEL_NAME, torch_dtype=dtype, attn_implementation="sdpa",
            device_map="cpu",
        )
        device = "cpu"

    model.eval()
    model.set_ddpm_inference_steps(num_steps=5)
    print(f"Model loaded on {device}!")

    # =========================================================================
    # Step 3: Load voice preset
    # =========================================================================
    voice_files = [f for f in glob.glob(os.path.join(voices_dir, "*.pt"))
                   if args.voice.lower() in os.path.basename(f).lower()]

    if not voice_files:
        print(f"ERROR: No voice preset found for '{args.voice}'", file=sys.stderr)
        print(f"Available: {os.listdir(voices_dir)}", file=sys.stderr)
        sys.exit(1)

    voice_path = voice_files[0]
    print(f"Using voice: {args.voice} ({os.path.basename(voice_path)})")

    all_prefilled_outputs = torch.load(voice_path, map_location=device, weights_only=False)

    # =========================================================================
    # Step 4: Generate audio
    # =========================================================================
    print(f"Generating audio for: '{args.text[:80]}...'")

    inputs = processor.process_input_with_cached_prompt(
        text=args.text,
        cached_prompt=all_prefilled_outputs,
        padding=True,
        return_tensors="pt",
        return_attention_mask=True,
    )

    for k, v in inputs.items():
        if torch.is_tensor(v):
            inputs[k] = v.to(device)

    output = model.generate(
        **inputs,
        tokenizer=processor.tokenizer,
        cfg_scale=1.5,
        generation_config={"do_sample": False},
        all_prefilled_outputs=copy.deepcopy(all_prefilled_outputs),
    )

    # =========================================================================
    # Step 5: Save WAV file
    # =========================================================================
    audio = output.speech_outputs[0]
    processor.save_audio(audio, output_path=args.output)

    file_size = os.path.getsize(args.output)
    sample_rate = 24000
    audio_samples = audio.shape[-1] if len(audio.shape) > 0 else len(audio)
    duration = audio_samples / sample_rate

    print(f"Saved: {args.output} ({file_size / 1024:.1f} KB, {duration:.2f}s)")

if __name__ == "__main__":
    main()
