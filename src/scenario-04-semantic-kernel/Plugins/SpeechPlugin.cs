// =============================================================================
// SpeechPlugin.cs — Semantic Kernel Plugin for VibeVoice TTS
// =============================================================================
// This plugin wraps the VibeVoice Python backend HTTP API so that
// Semantic Kernel agents can "speak" any text they generate.
//
// The backend contract:
//   POST /api/tts  →  { "text": "...", "voice_id": "en-US-Aria" }  →  WAV bytes

using System.ComponentModel;
using System.Text;
using System.Text.Json;
using Microsoft.SemanticKernel;

namespace VoiceLabs.SK.Plugins;

/// <summary>
/// Semantic Kernel plugin that converts text to speech via the VibeVoice backend.
/// </summary>
public class SpeechPlugin
{
    private readonly HttpClient _http;
    private readonly string _baseUrl;

    /// <param name="baseUrl">Base URL of the VibeVoice backend (e.g. http://localhost:5100)</param>
    public SpeechPlugin(HttpClient httpClient, string baseUrl = "http://localhost:5100")
    {
        _http = httpClient;
        _baseUrl = baseUrl.TrimEnd('/');
    }

    // =========================================================================
    // KernelFunction: SpeakAsync
    // =========================================================================
    // Sends text to the VibeVoice backend and saves the returned WAV audio.

    [KernelFunction("speak")]
    [Description("Converts text to speech using VibeVoice TTS and saves the audio as a WAV file.")]
    public async Task<string> SpeakAsync(
        [Description("The text to convert to speech")] string text,
        [Description("The voice ID to use (e.g. en-US-Aria)")] string voiceId = "en-US-Aria")
    {
        Console.WriteLine($"\n🎙️  Sending text to VibeVoice backend...");
        Console.WriteLine($"   Voice : {voiceId}");
        Console.WriteLine($"   Length: {text.Length} characters");

        // Build the JSON request body matching the backend contract
        var payload = JsonSerializer.Serialize(new { text, voice_id = voiceId });
        var content = new StringContent(payload, Encoding.UTF8, "application/json");

        var response = await _http.PostAsync($"{_baseUrl}/api/tts", content);
        response.EnsureSuccessStatusCode();

        // Save the WAV bytes to a file
        var audioBytes = await response.Content.ReadAsByteArrayAsync();
        var outputPath = Path.Combine(Directory.GetCurrentDirectory(), $"output_{DateTime.Now:yyyyMMdd_HHmmss}.wav");
        await File.WriteAllBytesAsync(outputPath, audioBytes);

        var durationEstimate = audioBytes.Length / (24000.0 * 2); // rough: 24 kHz, 16-bit mono
        Console.WriteLine($"✅ Audio saved → {outputPath}");
        Console.WriteLine($"   Size    : {audioBytes.Length / 1024.0:F1} KB");
        Console.WriteLine($"   Duration: ~{durationEstimate:F1}s");

        return outputPath;
    }

    // =========================================================================
    // KernelFunction: ListVoicesAsync
    // =========================================================================

    [KernelFunction("list_voices")]
    [Description("Lists available voices from the VibeVoice backend.")]
    public async Task<string> ListVoicesAsync()
    {
        var response = await _http.GetAsync($"{_baseUrl}/api/voices");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }
}
