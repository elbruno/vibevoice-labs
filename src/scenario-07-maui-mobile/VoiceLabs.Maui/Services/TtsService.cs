using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace VoiceLabs.Maui.Services;

public class TtsService
{
    private readonly HttpClient _http;

    public TtsService(HttpClient http)
    {
        _http = http;
    }

    public async Task<List<VoiceInfo>> GetVoicesAsync()
    {
        var response = await _http.GetFromJsonAsync<VoicesResponse>("/api/voices");
        return response?.Voices ?? [];
    }

    public async Task<byte[]?> GenerateAudioAsync(string text, string voiceId)
    {
        var request = new TtsRequest { Text = text, VoiceId = voiceId };
        var response = await _http.PostAsJsonAsync("/api/tts", request);

        if (!response.IsSuccessStatusCode)
            return null;

        return await response.Content.ReadAsByteArrayAsync();
    }

    public async Task<bool> CheckHealthAsync()
    {
        try
        {
            var response = await _http.GetAsync("/api/health");
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }
}

public class VoicesResponse
{
    [JsonPropertyName("voices")]
    public List<VoiceInfo> Voices { get; set; } = [];
}

public class VoiceInfo
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("language")]
    public string Language { get; set; } = string.Empty;

    [JsonPropertyName("style")]
    public string Style { get; set; } = string.Empty;

    public string DisplayName => $"{Name} ({Language})";
}

public class TtsRequest
{
    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;

    [JsonPropertyName("voice_id")]
    public string VoiceId { get; set; } = string.Empty;
}
