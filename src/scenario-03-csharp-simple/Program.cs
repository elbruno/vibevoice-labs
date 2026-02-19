// =============================================================================
// VibeVoice TTS - Simple C# Console Demo
// =============================================================================
// This script demonstrates how to call the VibeVoice FastAPI backend from C#
// using HttpClient. It mirrors Scenario 1 (Python simple script) step by step.
//
// Backend API (Python FastAPI):
//   GET  /api/health  → health check
//   GET  /api/voices  → list available voices
//   POST /api/tts     → { "text": "...", "voice_id": "..." } → WAV bytes

using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

// =============================================================================
// STEP 1: Setup HttpClient and Base URL
// =============================================================================
// The backend URL defaults to http://localhost:5100 but can be overridden
// with the VIBEVOICE_BACKEND_URL environment variable.

var baseUrl = Environment.GetEnvironmentVariable("VIBEVOICE_BACKEND_URL")
    ?? "http://localhost:5100";

using var httpClient = new HttpClient { BaseAddress = new Uri(baseUrl) };

Console.WriteLine("🎙️  VibeVoice TTS — C# Console Demo");
Console.WriteLine($"📡 Backend URL: {baseUrl}");
Console.WriteLine();

// =============================================================================
// STEP 2: Check Backend Health
// =============================================================================
// Make sure the Python FastAPI backend is running before proceeding.

Console.WriteLine("🔍 Step 2: Checking backend health...");
try
{
    var healthResponse = await httpClient.GetAsync("/api/health");
    healthResponse.EnsureSuccessStatusCode();
    var healthJson = await healthResponse.Content.ReadAsStringAsync();
    var health = JsonSerializer.Deserialize<HealthResponse>(healthJson);
    Console.WriteLine($"   ✅ Backend is {health?.Status ?? "unknown"} (model loaded: {health?.ModelLoaded})");
}
catch (Exception ex)
{
    Console.WriteLine($"   ❌ Backend not reachable: {ex.Message}");
    Console.WriteLine("   💡 Start the backend first: cd src/scenario-02-fullstack/backend && uvicorn main:app --port 5100");
    return;
}
Console.WriteLine();

// =============================================================================
// STEP 3: List Available Voices
// =============================================================================
// Fetch the voice catalog from the backend and display available options.

Console.WriteLine("🗣️  Step 3: Listing available voices...");
try
{
    var voicesResponse = await httpClient.GetAsync("/api/voices");
    voicesResponse.EnsureSuccessStatusCode();
    var voicesJson = await voicesResponse.Content.ReadAsStringAsync();
    var voicesResult = JsonSerializer.Deserialize<VoicesResponse>(voicesJson);

    if (voicesResult?.Voices is { Count: > 0 })
    {
        foreach (var voice in voicesResult.Voices)
        {
            Console.WriteLine($"   🎤 {voice.Id} — {voice.Name} ({voice.Language})");
        }
    }
    else
    {
        Console.WriteLine("   ⚠️  No voices returned from backend.");
    }
}
catch (Exception ex)
{
    Console.WriteLine($"   ⚠️  Could not fetch voices: {ex.Message}");
}
Console.WriteLine();

// =============================================================================
// STEP 4: Define the Text to Synthesize
// =============================================================================
// VibeVoice supports up to ~10 minutes of audio generation.
// For best results, use natural text with proper punctuation.

var text = "Hello! Welcome to VibeVoice Labs. This is a demonstration of the VibeVoice text-to-speech system.";
var voiceId = "en-US-Aria";

Console.WriteLine("📝 Step 4: Text to synthesize:");
Console.WriteLine($"   \"{text}\"");
Console.WriteLine($"   Voice: {voiceId}");
Console.WriteLine();

// =============================================================================
// AVAILABLE VOICES
// =============================================================================
// Uncomment any of the following lines to try different voices:

// --- English Voices ---
// voiceId = "en-US-Aria";       // American English (default)
// voiceId = "en-GB-Sonia";      // British English
// voiceId = "en-AU-Natasha";    // Australian English

// --- Multilingual Voices ---
// text = "Guten Tag! Willkommen bei VibeVoice.";       voiceId = "de-DE-Katja";    // German
// text = "Bonjour! Bienvenue à VibeVoice.";             voiceId = "fr-FR-Denise";   // French
// text = "Ciao! Benvenuto a VibeVoice.";                voiceId = "it-IT-Elsa";     // Italian
// text = "¡Hola! Bienvenido a VibeVoice.";              voiceId = "es-ES-Elvira";   // Spanish
// text = "Olá! Bem-vindo ao VibeVoice.";                voiceId = "pt-BR-Francisca";// Portuguese
// text = "Hallo! Welkom bij VibeVoice.";                voiceId = "nl-NL-Colette";  // Dutch
// text = "こんにちは！VibeVoice へようこそ。";            voiceId = "ja-JP-Nanami";   // Japanese
// text = "안녕하세요! VibeVoice에 오신 것을 환영합니다."; voiceId = "ko-KR-SunHi";   // Korean

// =============================================================================
// STEP 5: Generate Audio (POST /api/tts)
// =============================================================================
// Send the text and voice selection to the backend. The response is WAV bytes.

Console.WriteLine("🎵 Step 5: Generating audio...");

var requestBody = JsonSerializer.Serialize(new TtsRequest { Text = text, VoiceId = voiceId });
var content = new StringContent(requestBody, Encoding.UTF8, "application/json");

HttpResponseMessage ttsResponse;
try
{
    ttsResponse = await httpClient.PostAsync("/api/tts", content);
    ttsResponse.EnsureSuccessStatusCode();
}
catch (Exception ex)
{
    Console.WriteLine($"   ❌ Audio generation failed: {ex.Message}");
    return;
}

var audioBytes = await ttsResponse.Content.ReadAsByteArrayAsync();
Console.WriteLine($"   ✅ Received {audioBytes.Length} bytes of audio data");
Console.WriteLine();

// =============================================================================
// STEP 6: Save WAV File
// =============================================================================
// Write the raw WAV bytes to a local file.

var outputFilename = "output.wav";

Console.WriteLine($"💾 Step 6: Saving audio to {outputFilename}...");
await File.WriteAllBytesAsync(outputFilename, audioBytes);

// =============================================================================
// STEP 7: Confirmation
// =============================================================================
// Report success and provide file info.

var fileInfo = new FileInfo(outputFilename);
Console.WriteLine();
Console.WriteLine("✅ Audio generated successfully!");
Console.WriteLine($"   📁 File: {outputFilename}");
Console.WriteLine($"   📏 Size: {fileInfo.Length / 1024.0:F1} KB");
Console.WriteLine($"   🎤 Voice: {voiceId}");
Console.WriteLine();
Console.WriteLine("🎧 Open the file in your favorite audio player to listen!");

// =============================================================================
// ADVANCED: Streaming Generation (Optional)
// =============================================================================
// For longer texts, you can use streaming to start playback before generation
// is complete. This calls the backend with Accept: chunked and reads
// the response stream incrementally.

// async Task GenerateWithStreamingAsync()
// {
//     var longText = """
//         VibeVoice is a state-of-the-art text-to-speech model developed by Microsoft.
//         It provides natural-sounding speech synthesis with low latency, making it
//         ideal for real-time applications like voice assistants and accessibility tools.
//         """;
//
//     Console.WriteLine("🔄 Streaming generation...");
//
//     var request = new HttpRequestMessage(HttpMethod.Post, "/api/tts");
//     request.Content = new StringContent(
//         JsonSerializer.Serialize(new TtsRequest { Text = longText, VoiceId = "en-US-Aria" }),
//         Encoding.UTF8, "application/json");
//
//     var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
//     response.EnsureSuccessStatusCode();
//
//     await using var stream = await response.Content.ReadAsStreamAsync();
//     var buffer = new byte[4096];
//     int totalBytes = 0;
//     int bytesRead;
//
//     while ((bytesRead = await stream.ReadAsync(buffer)) > 0)
//     {
//         totalBytes += bytesRead;
//         Console.WriteLine($"   📦 Received chunk: {bytesRead} bytes (total: {totalBytes})");
//         // Process each chunk — write to file, play audio, etc.
//     }
//
//     Console.WriteLine($"   ✅ Streaming complete: {totalBytes} total bytes");
// }
//
// Uncomment to try streaming:
// await GenerateWithStreamingAsync();

// =============================================================================
// JSON Models
// =============================================================================

record HealthResponse(
    [property: JsonPropertyName("status")] string? Status,
    [property: JsonPropertyName("model_loaded")] bool? ModelLoaded);

record VoicesResponse(
    [property: JsonPropertyName("voices")] List<VoiceInfo>? Voices);

record VoiceInfo(
    [property: JsonPropertyName("id")] string? Id,
    [property: JsonPropertyName("name")] string? Name,
    [property: JsonPropertyName("language")] string? Language,
    [property: JsonPropertyName("style")] string? Style);

record TtsRequest
{
    [JsonPropertyName("text")]
    public string Text { get; init; } = "";

    [JsonPropertyName("voice_id")]
    public string VoiceId { get; init; } = "en-US-Aria";
}
