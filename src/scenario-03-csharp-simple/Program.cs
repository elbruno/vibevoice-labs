// =============================================================================
// VibeVoice TTS - Simple C# Console Demo (ElBruno.VibeVoice Library)
// =============================================================================
// Uses the ElBruno.VibeVoice library to run VibeVoice TTS entirely in C#.
// Models are auto-downloaded from HuggingFace if not found locally.

using ElBruno.VibeVoice;

Console.WriteLine("🎙️  VibeVoice TTS — C# Console Demo");
Console.WriteLine();

// Configure the synthesizer — models auto-download to shared cache if not found
var options = new VibeVoiceOptions();
// To use a custom model path instead of the default cache:
// options.ModelPath = @"C:\path\to\models";

using var tts = new VibeVoiceSynthesizer(options);

Console.WriteLine($"📦 Model path: {tts.ModelPath}");
Console.WriteLine($"📥 Model available: {tts.IsModelAvailable}");
Console.WriteLine();

// Ensure models are downloaded (with progress reporting)
var progress = new Progress<DownloadProgress>(p =>
{
    if (p.Stage == DownloadStage.Downloading)
        Console.Write($"\r   ⬇️  {p.Message}");
    else
        Console.WriteLine($"   {p.Stage}: {p.Message}");
});

Console.WriteLine("🔍 Checking/downloading model files...");
await tts.EnsureModelAvailableAsync(progress);
Console.WriteLine();

// Generate speech
var text = "Hello! Welcome to VibeVoice Labs. This is a demonstration of the VibeVoice text to speech system running natively in C sharp.";
var voice = VibeVoicePreset.Carter;
var outputPath = "output.wav";

Console.WriteLine($"🗣️  Voice:  {voice}");
Console.WriteLine($"📝 Text:   \"{text}\"");
Console.WriteLine();

Console.WriteLine("🎵 Generating audio...");
var sw = System.Diagnostics.Stopwatch.StartNew();
float[] audio = await tts.GenerateAudioAsync(text, voice);
sw.Stop();

double duration = audio.Length / 24000.0;
Console.WriteLine($"   ✅ Generated {duration:F2}s of audio ({audio.Length:N0} samples)");
Console.WriteLine($"   ⏱️  Time: {sw.ElapsedMilliseconds}ms");
Console.WriteLine();

// Save to WAV
tts.SaveWav(outputPath, audio);
Console.WriteLine($"💾 Saved: {Path.GetFullPath(outputPath)}");
Console.WriteLine();
Console.WriteLine("🎉 Done! Open the WAV file to listen.");
