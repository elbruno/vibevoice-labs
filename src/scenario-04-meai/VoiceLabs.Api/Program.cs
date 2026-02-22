using ElBruno.VibeVoiceTTS;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Register VibeVoice TTS as a singleton (loads models once, reuses across requests)
builder.Services.AddSingleton<VibeVoiceSynthesizer>(_ =>
{
    var options = new VibeVoiceOptions
    {
        DiffusionSteps = 20,
        SampleRate = 24000,
    };
    return new VibeVoiceSynthesizer(options);
});

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy => policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
});

var app = builder.Build();

app.UseCors();
app.MapDefaultEndpoints();

// Ensure model is downloaded at startup
var tts = app.Services.GetRequiredService<VibeVoiceSynthesizer>();
var downloadProgress = new Progress<DownloadProgress>(p =>
{
    if (p.Stage == DownloadStage.Downloading)
        Console.Write($"\r   ⬇️  {p.Message}");
    else
        Console.WriteLine($"   {p.Stage}: {p.Message}");
});

Console.WriteLine("🎙️  VibeVoice API — Initializing...");
Console.WriteLine($"📦 Model path: {tts.ModelPath}");
await tts.EnsureModelAvailableAsync(downloadProgress);
Console.WriteLine("✅ Model ready!");

// GET /api/health
app.MapGet("/api/health", () => Results.Ok(new { status = "healthy" }));

// GET /api/ready
app.MapGet("/api/ready", (VibeVoiceSynthesizer synth) =>
    Results.Ok(new { ready = synth.IsModelAvailable }));

// GET /api/voices
app.MapGet("/api/voices", (VibeVoiceSynthesizer synth) =>
    Results.Ok(new { voices = synth.GetAvailableVoices() }));

// POST /api/tts — Generate speech and return WAV audio
app.MapPost("/api/tts", async (TtsRequest request, VibeVoiceSynthesizer synth, CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(request.Text))
        return Results.BadRequest(new { error = "Text is required" });

    var voice = request.Voice ?? "Carter";
    var sw = System.Diagnostics.Stopwatch.StartNew();
    var audio = await synth.GenerateAudioAsync(request.Text, voice, ct);
    sw.Stop();

    // Write WAV to memory stream
    using var ms = new MemoryStream();
    using (var writer = new BinaryWriter(ms, System.Text.Encoding.UTF8, leaveOpen: true))
    {
        int sampleRate = 24000;
        short bitsPerSample = 16;
        short channels = 1;
        int dataSize = audio.Length * 2;

        // WAV header
        writer.Write("RIFF"u8);
        writer.Write(36 + dataSize);
        writer.Write("WAVE"u8);
        writer.Write("fmt "u8);
        writer.Write(16); // chunk size
        writer.Write((short)1); // PCM
        writer.Write(channels);
        writer.Write(sampleRate);
        writer.Write(sampleRate * channels * bitsPerSample / 8);
        writer.Write((short)(channels * bitsPerSample / 8));
        writer.Write(bitsPerSample);
        writer.Write("data"u8);
        writer.Write(dataSize);

        foreach (var sample in audio)
        {
            var clamped = Math.Clamp(sample, -1.0f, 1.0f);
            writer.Write((short)(clamped * 32767));
        }
    }

    Console.WriteLine($"🗣️  Generated {audio.Length / 24000.0:F2}s audio for \"{request.Text[..Math.Min(50, request.Text.Length)]}...\" in {sw.ElapsedMilliseconds}ms");

    return Results.File(ms.ToArray(), "audio/wav", "speech.wav");
});

app.Run();

record TtsRequest(string Text, string? Voice);
