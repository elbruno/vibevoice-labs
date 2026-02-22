"""
Upload VibeVoice ONNX Models to Hugging Face Hub
==================================================
Automates creating a Hugging Face repository and uploading all model files,
configuration, examples, and documentation.

Prerequisites:
    pip install huggingface_hub

Usage:
    # Login first
    huggingface-cli login

    # Upload everything
    python upload_to_hf.py --repo elbruno/VibeVoice-Realtime-0.5B-ONNX --models-dir ../models

    # Dry run (shows what would be uploaded)
    python upload_to_hf.py --repo elbruno/VibeVoice-Realtime-0.5B-ONNX --models-dir ../models --dry-run
"""

import argparse
import os
import sys
import shutil
import tempfile

def main():
    parser = argparse.ArgumentParser(
        description="Upload VibeVoice ONNX models to Hugging Face Hub"
    )
    parser.add_argument(
        "--repo", type=str, required=True,
        help="Hugging Face repo ID (e.g., elbruno/VibeVoice-Realtime-0.5B-ONNX)"
    )
    parser.add_argument(
        "--models-dir", type=str, default="../models",
        help="Path to the exported ONNX models directory"
    )
    parser.add_argument(
        "--dry-run", action="store_true",
        help="Show what would be uploaded without actually uploading"
    )
    parser.add_argument(
        "--private", action="store_true",
        help="Create the repository as private"
    )
    args = parser.parse_args()

    # Resolve paths
    script_dir = os.path.dirname(os.path.abspath(__file__))
    models_dir = os.path.abspath(os.path.join(script_dir, args.models_dir))

    print("🤗 VibeVoice ONNX → Hugging Face Hub Uploader")
    print(f"   Repo:       {args.repo}")
    print(f"   Models dir: {models_dir}")
    print(f"   Dry run:    {args.dry_run}")
    print()

    # Validate models directory
    required_onnx = ["text_encoder.onnx", "diffusion_step.onnx", "acoustic_decoder.onnx"]
    missing = [f for f in required_onnx if not os.path.exists(os.path.join(models_dir, f))]
    if missing:
        print("❌ Missing ONNX model files:")
        for f in missing:
            print(f"   • {f}")
        print()
        print("💡 Export models first:")
        print("   cd src/scenario-08-onnx-native/export")
        print("   python export_model.py --output ../models")
        sys.exit(1)

    # Collect files to upload
    files_to_upload = []

    # 1. Model card (README.md)
    readme_src = os.path.join(script_dir, "README_MODEL_CARD.md")
    if os.path.exists(readme_src):
        files_to_upload.append(("README.md", readme_src))
    else:
        print(f"⚠️  README_MODEL_CARD.md not found at {readme_src}")

    # 2. License
    license_src = os.path.join(script_dir, "LICENSE_HF")
    if os.path.exists(license_src):
        files_to_upload.append(("LICENSE", license_src))

    # 3. Config
    config_src = os.path.join(script_dir, "config.json")
    if os.path.exists(config_src):
        files_to_upload.append(("config.json", config_src))

    # 4. Examples
    for example_file in ["example_inference.py", "example_csharp.md"]:
        src = os.path.join(script_dir, example_file)
        if os.path.exists(src):
            files_to_upload.append((example_file, src))

    # 5. ONNX model files
    for onnx_file in required_onnx:
        src = os.path.join(models_dir, onnx_file)
        if os.path.exists(src):
            size_mb = os.path.getsize(src) / (1024 * 1024)
            files_to_upload.append((onnx_file, src))
            print(f"   📦 {onnx_file} ({size_mb:.1f} MB)")

    # 6. Tokenizer
    tokenizer_src = os.path.join(models_dir, "tokenizer.json")
    if os.path.exists(tokenizer_src):
        files_to_upload.append(("tokenizer.json", tokenizer_src))

    # 7. Voice presets
    voices_dir = os.path.join(models_dir, "voices")
    if os.path.isdir(voices_dir):
        for root, dirs, files in os.walk(voices_dir):
            for f in files:
                full_path = os.path.join(root, f)
                rel_path = os.path.join("voices", os.path.relpath(full_path, voices_dir))
                rel_path = rel_path.replace("\\", "/")
                files_to_upload.append((rel_path, full_path))

    print()
    print(f"📋 Files to upload: {len(files_to_upload)}")
    for dest, src in files_to_upload:
        size = os.path.getsize(src)
        if size > 1024 * 1024:
            print(f"   {dest} ({size / (1024*1024):.1f} MB)")
        else:
            print(f"   {dest} ({size / 1024:.1f} KB)")
    print()

    if args.dry_run:
        print("🏁 Dry run complete — no files were uploaded.")
        return

    # Import huggingface_hub
    try:
        from huggingface_hub import HfApi, create_repo
    except ImportError:
        print("❌ huggingface_hub not installed. Run: pip install huggingface_hub")
        sys.exit(1)

    api = HfApi()

    # Create repository if it doesn't exist
    print(f"📂 Creating repository: {args.repo}")
    try:
        create_repo(
            repo_id=args.repo,
            repo_type="model",
            private=args.private,
            exist_ok=True,
        )
        print(f"   ✅ Repository ready: https://huggingface.co/{args.repo}")
    except Exception as e:
        print(f"   ⚠️  Repository creation: {e}")

    # Upload files
    print()
    print("📤 Uploading files...")

    # Create a staging directory with the correct file layout
    with tempfile.TemporaryDirectory() as staging_dir:
        for dest, src in files_to_upload:
            dest_path = os.path.join(staging_dir, dest)
            os.makedirs(os.path.dirname(dest_path), exist_ok=True)
            shutil.copy2(src, dest_path)

        # Upload the entire folder
        try:
            api.upload_folder(
                folder_path=staging_dir,
                repo_id=args.repo,
                repo_type="model",
                commit_message="Upload VibeVoice-Realtime-0.5B ONNX models and documentation",
            )
            print()
            print(f"🎉 Upload complete!")
            print(f"   🔗 https://huggingface.co/{args.repo}")
        except Exception as e:
            print(f"❌ Upload failed: {e}")
            sys.exit(1)


if __name__ == "__main__":
    main()
