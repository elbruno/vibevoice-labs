// =============================================================================
// VibeVoice TTS — Native ONNX Runtime Inference (ElBruno.VibeVoice Library)
// =============================================================================
// Runs the full VibeVoice TTS pipeline using the ElBruno.VibeVoice library.
// Models are auto-downloaded from HuggingFace if not found locally.
//
// Usage:
//   dotnet run -- --text "Hello world" --voice Carter --output output.wav
// =============================================================================

using System.Diagnostics;
using ElBruno.VibeVoice;

Console.WriteLine("🎙️  VibeVoice TTS — Native ONNX Runtime Inference");
Console.WriteLine("   No Python. No HTTP. Pure C# + ONNX Runtime.");
Console.WriteLine();

// =============================================================================
// Parse CLI Arguments
// =============================================================================

string text = "Hello! Welcome to VibeVoice Labs. This is a demonstration of native ONNX Runtime inference running entirely in C sharp.";
string voice = "Carter";
string outputPath = "output.wav";
string? modelsDir = null;

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
            Console.WriteLine("  --models-dir <path>   Path to ONNX models (default: auto-download to shared cache)");
            Console.WriteLine("  --help                Show this help message");
            return;
    }
}

outputPath = Path.GetFullPath(outputPath);

// =============================================================================
// Step 1: Create Synthesizer
// =============================================================================

var options = new VibeVoiceOptions();
if (modelsDir is not null)
    options.ModelPath = Path.GetFullPath(modelsDir);

using var tts = new VibeVoiceSynthesizer(options);

Console.WriteLine($"📂 Model path: {tts.ModelPath}");
Console.WriteLine($"📥 Model available: {tts.IsModelAvailable}");
Console.WriteLine();

// =============================================================================
// Step 2: Ensure Models Downloaded
// =============================================================================

Console.WriteLine("🔍 Checking/downloading model files...");
var progress = new Progress<DownloadProgress>(p =>
{
    if (p.Stage == DownloadStage.Downloading)
        Console.Write($"\r   ⬇️  {p.Message}                    ");
    else
        Console.WriteLine($"   {p.Stage}: {p.Message}");
});

await tts.EnsureModelAvailableAsync(progress);
Console.WriteLine();

// =============================================================================
// Step 3: Generate Audio
// =============================================================================

Console.WriteLine($"🗣️  Voice:  {voice}");
Console.WriteLine($"📝 Text:   \"{(text.Length > 80 ? text[..77] + "..." : text)}\"");
Console.WriteLine($"💾 Output: {outputPath}");
Console.WriteLine();

Console.WriteLine("🎵 Generating audio...");
var inferenceTimer = Stopwatch.StartNew();
float[] audioSamples = await tts.GenerateAudioAsync(text, voice);
inferenceTimer.Stop();

double durationSeconds = audioSamples.Length / 24000.0;
Console.WriteLine($"   ✅ Generated {durationSeconds:F2}s of audio ({audioSamples.Length:N0} samples @ 24kHz)");
Console.WriteLine($"   ⏱️  Inference time: {inferenceTimer.Elapsed.TotalSeconds:F2}s");
Console.WriteLine($"   📊 Real-time factor: {inferenceTimer.Elapsed.TotalSeconds / durationSeconds:F2}x");
Console.WriteLine();

// =============================================================================
// Step 4: Save WAV File
// =============================================================================

tts.SaveWav(outputPath, audioSamples);
var fileInfo = new FileInfo(outputPath);
Console.WriteLine($"💾 Saved: {outputPath} ({fileInfo.Length / 1024.0:F1} KB)");
Console.WriteLine();
Console.WriteLine("🎉 Done! Open the WAV file to listen.");
