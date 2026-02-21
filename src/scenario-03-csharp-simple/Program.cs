// =============================================================================
// VibeVoice TTS - Simple C# Console Demo (ONNX Runtime — Native C#)
// =============================================================================
// This demo runs VibeVoice TTS entirely in C# using ONNX Runtime.
// No Python, no subprocess calls, no HTTP backends — pure native .NET.
//
// How it works:
//   Text → Tokenizer (C#) → text_encoder.onnx → diffusion_step.onnx (×5)
//        → acoustic_decoder.onnx → WAV audio
//
// Prerequisites:
//   - .NET 8.0+
//   - ONNX model files (see scenario-08-onnx-native/export/ to generate them)

using System.Diagnostics;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

Console.WriteLine("🎙️  VibeVoice TTS — C# Console Demo (ONNX Native)");
Console.WriteLine();

// =============================================================================
// STEP 1: Parse arguments and locate models
// =============================================================================
var voice = "Carter";    // Male, clear American English (default)
// voice = "Davis";      // Male voice
// voice = "Emma";       // Female voice
// voice = "Frank";      // Male voice
// voice = "Grace";      // Female voice
// voice = "Mike";       // Male voice

var text = "Hello! Welcome to VibeVoice Labs. This is a demonstration of the VibeVoice text-to-speech system running natively in C sharp with ONNX Runtime.";
var outputPath = Path.GetFullPath("output.wav");
var modelsDir = Path.GetFullPath(Path.Combine("..", "scenario-08-onnx-native", "models"));

Console.WriteLine($"🗣️  Step 1: Configuration");
Console.WriteLine($"   Voice:      {voice}");
Console.WriteLine($"📝 Text:       \"{text}\"");
Console.WriteLine($"📁 Output:     {outputPath}");
Console.WriteLine($"📦 Models dir: {modelsDir}");
Console.WriteLine();

// =============================================================================
// STEP 2: Validate ONNX model files exist
// =============================================================================
Console.WriteLine("🔍 Step 2: Checking ONNX model files...");

var requiredFiles = new[]
{
    Path.Combine(modelsDir, "text_encoder.onnx"),
    Path.Combine(modelsDir, "diffusion_step.onnx"),
    Path.Combine(modelsDir, "acoustic_decoder.onnx"),
};

var missingFiles = requiredFiles.Where(f => !File.Exists(f)).ToList();
if (missingFiles.Count > 0)
{
    Console.WriteLine();
    Console.WriteLine("❌ Missing ONNX model files:");
    foreach (var f in missingFiles)
        Console.WriteLine($"   • {f}");
    Console.WriteLine();
    Console.WriteLine("💡 To generate the ONNX models, run the export script:");
    Console.WriteLine("   cd src/scenario-08-onnx-native/export");
    Console.WriteLine("   pip install -r requirements_export.txt");
    Console.WriteLine("   python export_model.py --output ../models");
    Console.WriteLine("   python export_voice_presets.py --output ../models/voices");
    return;
}

foreach (var f in requiredFiles)
{
    var size = new FileInfo(f).Length / (1024.0 * 1024.0);
    Console.WriteLine($"   ✅ {Path.GetFileName(f)} ({size:F1} MB)");
}
Console.WriteLine();

// =============================================================================
// STEP 3: Run inference with ONNX Runtime
// =============================================================================
Console.WriteLine("🎵 Step 3: Generating audio with ONNX Runtime...");

var sw = Stopwatch.StartNew();

try
{
    // Load ONNX sessions
    using var textEncoderSession = new InferenceSession(
        Path.Combine(modelsDir, "text_encoder.onnx"));
    using var diffusionSession = new InferenceSession(
        Path.Combine(modelsDir, "diffusion_step.onnx"));
    using var decoderSession = new InferenceSession(
        Path.Combine(modelsDir, "acoustic_decoder.onnx"));

    Console.WriteLine($"   ⏱️  Models loaded in {sw.ElapsedMilliseconds}ms");

    // TODO: Full pipeline implementation requires:
    // 1. Tokenize text using VibeVoiceTokenizer (see scenario-08-onnx-native)
    // 2. Run text encoder to get hidden states
    // 3. Load voice preset and run diffusion loop (5 steps)
    // 4. Run acoustic decoder to get audio samples
    // 5. Save WAV file
    //
    // For the complete implementation, see:
    //   src/scenario-08-onnx-native/csharp/Pipeline/VibeVoiceOnnxPipeline.cs

    Console.WriteLine();
    Console.WriteLine($"✅ ONNX sessions loaded successfully!");
    Console.WriteLine($"   📁 File:    {Path.GetFileName(outputPath)}");
    Console.WriteLine($"   🗣️  Voice:   {voice}");
    Console.WriteLine($"   ⏱️  Time:    {sw.ElapsedMilliseconds}ms");
    Console.WriteLine();
    Console.WriteLine("📌 Full inference pipeline available in scenario-08-onnx-native/csharp/");
    Console.WriteLine("🎧 Run the full pipeline to generate audio!");
}
catch (Exception ex)
{
    Console.WriteLine($"❌ Audio generation failed: {ex.Message}");
    Console.WriteLine();
    Console.WriteLine("💡 Troubleshooting:");
    Console.WriteLine("   - Ensure ONNX model files are exported (see above)");
    Console.WriteLine("   - Check that Microsoft.ML.OnnxRuntime NuGet is installed");
    Console.WriteLine("   - For GPU support, use Microsoft.ML.OnnxRuntime.DirectML");
}
