using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace VoiceLabs.Web.Services;

/// <summary>
/// Service to check backend readiness before allowing user interaction
/// </summary>
public class BackendReadinessService
{
    private readonly HttpClient _httpClient;
    private BackendReadyState? _lastState;

    public BackendReadinessService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    /// <summary>
    /// Check if the backend is ready
    /// </summary>
    public async Task<BackendReadyState?> GetReadyStateAsync()
    {
        try
        {
            _lastState = await _httpClient.GetFromJsonAsync<BackendReadyState>("/api/ready");
            return _lastState;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error checking backend readiness: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Wait for the backend to be ready with polling
    /// </summary>
    public async Task<bool> WaitForReadyAsync(int maxWaitSeconds = 60, Action<BackendReadyState?>? onProgressUpdate = null)
    {
        var startTime = DateTime.UtcNow;
        var timeout = TimeSpan.FromSeconds(maxWaitSeconds);

        while (DateTime.UtcNow - startTime < timeout)
        {
            var state = await GetReadyStateAsync();
            onProgressUpdate?.Invoke(state);

            if (state?.Ready == true)
            {
                return true;
            }

            if (state?.State == "ERROR")
            {
                return false; // Backend failed initialization
            }

            // Wait 500ms before retrying
            await Task.Delay(500);
        }

        return false; // Timeout
    }

    /// <summary>
    /// Get the last known state
    /// </summary>
    public BackendReadyState? GetLastState() => _lastState;
}

/// <summary>
/// Backend readiness state response
/// </summary>
public class BackendReadyState
{
    [JsonPropertyName("ready")]
    public bool Ready { get; set; }

    [JsonPropertyName("state")]
    public string State { get; set; } = string.Empty;

    [JsonPropertyName("progress")]
    public int Progress { get; set; }

    [JsonPropertyName("services")]
    public Dictionary<string, ServiceStatus>? Services { get; set; }

    [JsonPropertyName("startup_time_ms")]
    public double StartupTimeMs { get; set; }

    [JsonPropertyName("errors")]
    public List<string> Errors { get; set; } = new();

    /// <summary>
    /// Get percentage progress
    /// </summary>
    public int ProgressPercent => Math.Max(0, Math.Min(100, Progress));

    /// <summary>
    /// Human-readable state description
    /// </summary>
    public string StateDescription => State switch
    {
        "INITIALIZING" => "Starting backend...",
        "LOADING_MODELS" => "Loading AI models...",
        "WARMING_UP" => "Warming up services...",
        "READY" => "Backend ready!",
        "ERROR" => "Backend initialization failed",
        _ => "Unknown state"
    };
}

/// <summary>
/// Individual service status
/// </summary>
public class ServiceStatus
{
    [JsonPropertyName("ready")]
    public bool Ready { get; set; }

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("warmup_time_ms")]
    public double? WarmupTimeMs { get; set; }

    [JsonPropertyName("error")]
    public string? Error { get; set; }

    [JsonPropertyName("model")]
    public string? Model { get; set; }

    [JsonPropertyName("loaded_at")]
    public string? LoadedAt { get; set; }
}
