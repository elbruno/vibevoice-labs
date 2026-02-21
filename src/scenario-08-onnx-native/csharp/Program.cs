// =============================================================================
// VibeVoice TTS — Native ONNX Runtime Inference (Zero Python Dependencies)
// =============================================================================
// Runs the full VibeVoice TTS pipeline entirely in C# using ONNX Runtime.
// Models must be pre-exported using the Python export tool (one-time step).
//
// Usage:
//   dotnet run -- --text "Hello world" --voice Carter --output output.wav
// =============================================================================

using System.Diagnostics;
using VoiceLabs.OnnxNative.Pipeline;
using VoiceLabs.OnnxNative.Utils;

Console.WriteLine("🎙️  VibeVoice TTS — Native ONNX Runtime Inference");
Console.WriteLine("   No Python. No HTTP. Pure C# + ONNX Runtime.");
Console.WriteLine();

// =============================================================================
// Parse CLI Arguments
// =============================================================================

string text = "Hello! Welcome to VibeVoice Labs. This is a demonstration of native ONNX Runtime inference running entirely in C sharp.";
string voice = "Carter";
string outputPath = "output.wav";
string modelsDir = Path.Combine("..", "models");

for (int i = 0; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--text" when i + 1 < args.Length:
            text = args[++i];
            break;
        case "--voice" when i + 1 < args.Length:
            voice = args[++i];
            break;
        case "--output" when i + 1 < args.Length:
            outputPath = args[++i];
            break;
        case "--models-dir" when i + 1 < args.Length:
            modelsDir = args[++i];
            break;
        case "--help":
            Console.WriteLine("Usage: VibeVoiceOnnx [options]");
            Console.WriteLine();
            Console.WriteLine("Options:");
            Console.WriteLine("  --text <text>         Text to synthesize (default: demo sentence)");
            Console.WriteLine("  --voice <name>        Voice preset name (default: Carter)");
            Console.WriteLine("  --output <path>       Output WAV file path (default: output.wav)");
            Console.WriteLine("  --models-dir <path>   Path to exported ONNX models (default: ../models)");
            Console.WriteLine("  --help                Show this help message");
            return;
    }
}

modelsDir = Path.GetFullPath(modelsDir);
outputPath = Path.GetFullPath(outputPath);

// =============================================================================
// Step 1: Validate Model Files
// =============================================================================

Console.WriteLine("📂 Step 1: Validating model files...");
Console.WriteLine($"   Models directory: {modelsDir}");

string[] requiredFiles = ["text_encoder.onnx", "diffusion_step.onnx", "acoustic_decoder.onnx", "tokenizer.json"];
foreach (var file in requiredFiles)
{
    var fullPath = Path.Combine(modelsDir, file);
    if (!File.Exists(fullPath))
    {
        Console.WriteLine($"   ❌ Missing: {file}");
        Console.WriteLine();
        Console.WriteLine("💡 Export models first:");
        Console.WriteLine("   cd src/scenario-08-onnx-native/export");
        Console.WriteLine("   pip install -r requirements_export.txt");
        Console.WriteLine("   python export_model.py --output ../models");
        return;
    }
    Console.WriteLine($"   ✅ Found: {file}");
}
Console.WriteLine();

// =============================================================================
// Step 2: Load Pipeline
// =============================================================================

Console.WriteLine("🧠 Step 2: Loading ONNX models into memory...");
var loadTimer = Stopwatch.StartNew();

using var pipeline = new VibeVoiceOnnxPipeline(modelsDir);

loadTimer.Stop();
Console.WriteLine($"   ✅ Pipeline loaded in {loadTimer.Elapsed.TotalSeconds:F2}s");
Console.WriteLine();

// =============================================================================
// Step 3: Configure Synthesis
// =============================================================================

Console.WriteLine($"🗣️  Step 3: Configuring synthesis...");
Console.WriteLine($"   Voice:  {voice}");
Console.WriteLine($"   Text:   \"{(text.Length > 80 ? text[..77] + "..." : text)}\"");
Console.WriteLine($"   Output: {outputPath}");

var availableVoices = pipeline.GetAvailableVoices();
if (availableVoices.Length > 0)
{
    Console.WriteLine($"   📋 Available voices: {string.Join(", ", availableVoices)}");
}
Console.WriteLine();

// =============================================================================
// Step 4: Generate Audio
// =============================================================================

Console.WriteLine("🎵 Step 4: Generating audio...");
Console.WriteLine("   ⏳ Running inference (text → tokens → latents → waveform)...");

var inferenceTimer = Stopwatch.StartNew();
float[] audioSamples = pipeline.GenerateAudio(text, voice);
inferenceTimer.Stop();

double durationSeconds = audioSamples.Length / 24000.0;
Console.WriteLine($"   ✅ Generated {durationSeconds:F2}s of audio ({audioSamples.Length:N0} samples @ 24kHz)");
Console.WriteLine($"   ⏱️  Inference time: {inferenceTimer.Elapsed.TotalSeconds:F2}s");
Console.WriteLine($"   📊 Real-time factor: {inferenceTimer.Elapsed.TotalSeconds / durationSeconds:F2}x");
Console.WriteLine();

// =============================================================================
// Step 5: Save WAV File
// =============================================================================

Console.WriteLine("💾 Step 5: Saving WAV file...");
AudioWriter.SaveWav(outputPath, audioSamples, sampleRate: 24000);

var fileInfo = new FileInfo(outputPath);
Console.WriteLine($"   ✅ Saved: {outputPath}");
Console.WriteLine($"   📏 Size: {fileInfo.Length / 1024.0:F1} KB");
Console.WriteLine();

// =============================================================================
// Done!
// =============================================================================

Console.WriteLine("🎉 Done! Audio generated successfully.");
Console.WriteLine("🎧 Open the WAV file in your favorite audio player to listen.");
