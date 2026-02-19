// =============================================================================
// VibeVoice TTS - Simple C# Console Demo (Direct Model Invocation)
// =============================================================================
// This script demonstrates how to run VibeVoice TTS directly from C# by
// invoking the Python VibeVoice model via System.Diagnostics.Process.
// No HTTP backend required — the model runs locally.
//
// How it works:
//   C# orchestrates the flow → calls tts_helper.py → VibeVoice generates WAV
//
// Prerequisites:
//   - Python 3.11+ with VibeVoice installed (see requirements.txt)
//   - pip install -r requirements.txt

using System.Diagnostics;

// =============================================================================
// STEP 1: Configuration
// =============================================================================
// Set the Python executable and paths. Override PYTHON_PATH env var if your
// Python is not on PATH (e.g., inside a virtual environment).

var pythonExe = Environment.GetEnvironmentVariable("PYTHON_PATH") ?? "python";
var scriptDir = AppContext.BaseDirectory;

// Look for tts_helper.py next to the .csproj (source dir), not bin output
var projectDir = Path.GetFullPath(Path.Combine(scriptDir, "..", "..", ".."));
var helperScript = Path.Combine(projectDir, "tts_helper.py");

if (!File.Exists(helperScript))
{
    // Fallback: check current working directory
    helperScript = Path.Combine(Directory.GetCurrentDirectory(), "tts_helper.py");
}

Console.WriteLine("🎙️  VibeVoice TTS — C# Console Demo (Direct Model)");
Console.WriteLine($"🐍 Python: {pythonExe}");
Console.WriteLine($"📂 Script: {helperScript}");
Console.WriteLine();

if (!File.Exists(helperScript))
{
    Console.WriteLine("❌ tts_helper.py not found! Make sure it exists in the project directory.");
    return;
}

// =============================================================================
// STEP 2: Verify Python & VibeVoice Installation
// =============================================================================
// Quick check that Python is available and VibeVoice is installed.

Console.WriteLine("🔍 Step 2: Verifying Python environment...");
try
{
    var checkProcess = new Process
    {
        StartInfo = new ProcessStartInfo
        {
            FileName = pythonExe,
            Arguments = "-c \"import vibevoice; print('VibeVoice OK')\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        }
    };
    checkProcess.Start();
    var checkOutput = await checkProcess.StandardOutput.ReadToEndAsync();
    await checkProcess.WaitForExitAsync();

    if (checkProcess.ExitCode != 0)
    {
        var checkError = await checkProcess.StandardError.ReadToEndAsync();
        Console.WriteLine($"   ❌ VibeVoice not installed: {checkError.Trim()}");
        Console.WriteLine("   💡 Run: pip install -r requirements.txt");
        return;
    }
    Console.WriteLine($"   ✅ {checkOutput.Trim()}");
}
catch (Exception ex)
{
    Console.WriteLine($"   ❌ Python not found: {ex.Message}");
    Console.WriteLine($"   💡 Set PYTHON_PATH env var to your Python executable");
    return;
}
Console.WriteLine();

// =============================================================================
// STEP 3: Select a Voice
// =============================================================================
// Available English voices: Carter, Davis, Emma, Frank, Grace, Mike
// These map to pre-computed .pt voice preset files.

var voice = "Carter";    // Male, clear American English (default)
// voice = "Davis";      // Male voice
// voice = "Emma";       // Female voice
// voice = "Frank";      // Male voice
// voice = "Grace";      // Female voice
// voice = "Mike";       // Male voice

Console.WriteLine($"🗣️  Step 3: Selected voice: {voice}");
Console.WriteLine();

// =============================================================================
// STEP 4: Define the Text to Synthesize
// =============================================================================
// VibeVoice supports up to ~10 minutes of audio generation.
// For best results, use natural text with proper punctuation.

var text = "Hello! Welcome to VibeVoice Labs. This is a demonstration of the VibeVoice text-to-speech system running directly from C sharp.";

Console.WriteLine("📝 Step 4: Text to synthesize:");
Console.WriteLine($"   \"{text}\"");
Console.WriteLine();

// =============================================================================
// STEP 5: Generate Audio (via Python VibeVoice model)
// =============================================================================
// Invoke tts_helper.py which loads the VibeVoice model, generates audio,
// and saves the WAV file. C# captures stdout for progress updates.

var outputFilename = "output.wav";
var outputPath = Path.Combine(Directory.GetCurrentDirectory(), outputFilename);

Console.WriteLine("🎵 Step 5: Generating audio (this may take a moment on first run)...");
Console.WriteLine("   ⏳ First run downloads the model (~1-2 GB) and voice presets.");
Console.WriteLine();

var process = new Process
{
    StartInfo = new ProcessStartInfo
    {
        FileName = pythonExe,
        Arguments = $"\"{helperScript}\" --text \"{text.Replace("\"", "\\\"")}\" --voice \"{voice}\" --output \"{outputPath}\"",
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        UseShellExecute = false,
        CreateNoWindow = true,
    }
};

process.Start();

// Stream stdout in real-time so the user sees progress
var stdoutTask = Task.Run(async () =>
{
    while (await process.StandardOutput.ReadLineAsync() is { } line)
    {
        Console.WriteLine($"   🐍 {line}");
    }
});

var stderrTask = Task.Run(async () =>
{
    return await process.StandardError.ReadToEndAsync();
});

await process.WaitForExitAsync();
await stdoutTask;
var stderr = await stderrTask;

Console.WriteLine();

if (process.ExitCode != 0)
{
    Console.WriteLine("❌ Audio generation failed!");
    if (!string.IsNullOrWhiteSpace(stderr))
    {
        Console.WriteLine($"   Error: {stderr.Trim()[..Math.Min(stderr.Trim().Length, 500)]}");
    }
    return;
}

// =============================================================================
// STEP 6: Verify Output
// =============================================================================
// Check that the WAV file was created and report file info.

if (!File.Exists(outputPath))
{
    Console.WriteLine($"❌ Output file not found: {outputPath}");
    return;
}

var fileInfo = new FileInfo(outputPath);
Console.WriteLine("✅ Audio generated successfully!");
Console.WriteLine($"   📁 File:    {outputFilename}");
Console.WriteLine($"   📏 Size:    {fileInfo.Length / 1024.0:F1} KB");
Console.WriteLine($"   🗣️  Voice:   {voice}");
Console.WriteLine($"   📂 Path:    {outputPath}");
Console.WriteLine();
Console.WriteLine("🎧 Open the file in your favorite audio player to listen!");
