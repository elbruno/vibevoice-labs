using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace VoiceLabs.Web.Services;

public class TtsService
{
    private readonly HttpClient _httpClient;

    public TtsService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<VoicesResponse?> GetVoicesAsync()
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<VoicesResponse>("/api/voices");
        }
        catch (Exception)
        {
            return null;
        }
    }

    public async Task<byte[]?> GenerateSpeechAsync(string text, string voiceId)
    {
        try
        {
            var request = new TtsRequest { Text = text, VoiceId = voiceId };
            var response = await _httpClient.PostAsJsonAsync("/api/tts", request);
            
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadAsByteArrayAsync();
            }
            return null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    public async Task<HealthResponse?> CheckHealthAsync()
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<HealthResponse>("/api/health");
        }
        catch (Exception)
        {
            return null;
        }
    }
}

public class VoicesResponse
{
    [JsonPropertyName("voices")]
    public List<Voice> Voices { get; set; } = new();
}

public class Voice
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;
    
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
    
    [JsonPropertyName("language")]
    public string Language { get; set; } = string.Empty;
    
    [JsonPropertyName("style")]
    public string Style { get; set; } = string.Empty;
}

public class TtsRequest
{
    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;
    
    [JsonPropertyName("voice_id")]
    public string VoiceId { get; set; } = string.Empty;
    
    [JsonPropertyName("output_format")]
    public string OutputFormat { get; set; } = "wav";
}

public class HealthResponse
{
    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;
    
    [JsonPropertyName("model_loaded")]
    public bool ModelLoaded { get; set; }
}
