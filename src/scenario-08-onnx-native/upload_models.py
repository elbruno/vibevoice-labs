"""Upload new ONNX models to HuggingFace."""
import sys, os
sys.stdout.reconfigure(encoding='utf-8')
from huggingface_hub import HfApi

repo_id = "elbruno/VibeVoice-Realtime-0.5B-ONNX"
models_dir = r"C:\src\vibevoice-labs\src\scenario-08-onnx-native\models"
api = HfApi()

# Files to upload
files = [
    # New autoregressive models
    "lm_with_kv.onnx", "lm_with_kv.onnx.data",
    "tts_lm_prefill.onnx", "tts_lm_prefill.onnx.data",
    "tts_lm_step.onnx", "tts_lm_step.onnx.data",
    "acoustic_connector.onnx", "acoustic_connector.onnx.data",
    "eos_classifier.onnx", "eos_classifier.onnx.data",
    "type_embeddings.npy",
    # Updated models
    "prediction_head.onnx", "prediction_head.onnx.data",
    "acoustic_decoder.onnx", "acoustic_decoder.onnx.data",
    # Tokenizer and config
    "tokenizer.json", "model_config.json",
]

# Voice preset files
voices_dir = os.path.join(models_dir, "voices")
for voice in ["en-Carter_man", "en-Emma_woman"]:
    voice_dir = os.path.join(voices_dir, voice)
    # Metadata
    files.append(f"voices/{voice}/metadata.json")
    # All .npy files in voice dir
    for f in os.listdir(voice_dir):
        if f.endswith('.npy'):
            files.append(f"voices/{voice}/{f}")
    # Negative subdirectory
    neg_dir = os.path.join(voice_dir, "negative")
    if os.path.exists(neg_dir):
        for f in os.listdir(neg_dir):
            if f.endswith('.npy'):
                files.append(f"voices/{voice}/negative/{f}")

print(f"Uploading {len(files)} files to {repo_id}")
for i, f in enumerate(files):
    local_path = os.path.join(models_dir, f)
    if not os.path.exists(local_path):
        print(f"  [{i+1}/{len(files)}] SKIP (not found): {f}")
        continue
    size_mb = os.path.getsize(local_path) / (1024*1024)
    print(f"  [{i+1}/{len(files)}] Uploading: {f} ({size_mb:.1f} MB)")
    api.upload_file(
        path_or_fileobj=local_path,
        path_in_repo=f,
        repo_id=repo_id,
        repo_type="model",
    )
print("Done!")
