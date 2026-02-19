// =============================================================================
// VibeVoice TTS - Simple C# Console Demo (CSnakes — Embedded Python)
// =============================================================================
// This demo uses CSnakes to embed the Python VibeVoice model directly inside
// the .NET process. No subprocess calls, no HTTP backends — the Python
// interpreter runs in-process via CSnakes.
//
// How it works:
//   C# → CSnakes (embedded CPython) → vibevoice_tts.py → WAV bytes returned
//
// Prerequisites:
//   - .NET 10+
//   - CSnakes NuGet package (auto-downloads Python if needed)
//   - Internet access on first run (downloads VibeVoice model ~1GB + voice presets)

using CSnakes.Runtime;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

Console.WriteLine("🎙️  VibeVoice TTS — C# Console Demo (CSnakes)");
Console.WriteLine();

// =============================================================================
// STEP 1: Configure CSnakes Python Environment
// =============================================================================
// CSnakes embeds a CPython interpreter inside the .NET process.
// It auto-creates a virtual environment and installs requirements.txt.

var pythonHome = Path.Join(Environment.CurrentDirectory, ".");

Console.WriteLine("🐍 Step 1: Setting up embedded Python environment...");
Console.WriteLine();

var builder = Host.CreateApplicationBuilder(args);
builder.Services
    .WithPython()
    .WithHome(pythonHome)
    .FromRedistributable();

using var app = builder.Build();
var env = app.Services.GetRequiredService<IPythonEnvironment>();

// =============================================================================
// STEP 2: Select Voice and Text
// =============================================================================
var voice = "Carter";    // Male, clear American English (default)
// voice = "Davis";      // Male voice
// voice = "Emma";       // Female voice
// voice = "Frank";      // Male voice
// voice = "Grace";      // Female voice
// voice = "Mike";       // Male voice

var text = "Hello! Welcome to VibeVoice Labs. This is a demonstration of the VibeVoice text-to-speech system running from C sharp using CSnakes.";
var outputPath = Path.GetFullPath("output.wav");

Console.WriteLine($"🗣️  Step 2: Voice: {voice}");
Console.WriteLine($"📝 Text: \"{text}\"");
Console.WriteLine($"📁 Output: {outputPath}");
Console.WriteLine();

// =============================================================================
// STEP 3: Call the VibeVoice Python module via CSnakes
// =============================================================================
// CSnakes auto-generates a typed C# wrapper for vibevoice_tts.py.
// The Python function synthesize_speech(text, voice, output_path) is called
// directly — no subprocess, no HTTP, no IPC.

Console.WriteLine("🎵 Step 3: Generating audio (this may take a moment on first run)...");
Console.WriteLine("   ⏳ First run downloads the model (~1 GB) and voice presets.");
Console.WriteLine();

try
{
    var module = env.VibevoiceTts();
    var result = module.SynthesizeSpeech(text, voice, outputPath);

    Console.WriteLine();
    Console.WriteLine($"✅ Audio generated successfully!");
    Console.WriteLine($"   📁 File:    {Path.GetFileName(outputPath)}");
    Console.WriteLine($"   🗣️  Voice:   {voice}");
    Console.WriteLine($"   📂 Path:    {outputPath}");

    if (File.Exists(outputPath))
    {
        var fileInfo = new FileInfo(outputPath);
        Console.WriteLine($"   📏 Size:    {fileInfo.Length / 1024.0:F1} KB");
    }

    Console.WriteLine();
    Console.WriteLine("🎧 Open the file in your favorite audio player to listen!");
}
catch (Exception ex)
{
    Console.WriteLine($"❌ Audio generation failed: {ex.Message}");
    Console.WriteLine();
    Console.WriteLine("💡 Troubleshooting:");
    Console.WriteLine("   - Ensure internet access for first-run model download");
    Console.WriteLine("   - Check that Python 3.11+ is available (CSnakes can auto-download)");
    Console.WriteLine("   - For GPU support, install CUDA-compatible PyTorch");
}
