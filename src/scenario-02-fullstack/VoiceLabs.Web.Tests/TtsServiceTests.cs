using Moq;
using Moq.Protected;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace VoiceLabs.Web.Tests;

/// <summary>
/// Unit tests for TtsService.
/// 
/// These tests are based on the API contract from the design review.
/// They may need adjustment once Alex's implementation is final.
/// </summary>
public class TtsServiceTests
{
    #region Models (to be replaced with actual models from VoiceLabs.Web)
    
    // Placeholder models matching API contract
    public record Voice(string Id, string Name, string Language, string Style);
    public record VoicesResponse(List<Voice> Voices);
    public record TtsRequest(string Text, string VoiceId, string OutputFormat);
    public record ErrorResponse(string Error, string Code);
    public record HealthResponse(string Status, bool ModelLoaded);
    
    #endregion

    #region GetVoicesAsync Tests

    [Fact]
    public async Task GetVoicesAsync_ReturnsVoiceList_WhenApiSucceeds()
    {
        // Arrange
        var expectedVoices = new VoicesResponse(new List<Voice>
        {
            new("en-US-Aria", "Aria", "en-US", "general"),
            new("de-DE-Katja", "Katja", "de-DE", "general")
        });
        
        var mockHandler = CreateMockHandler(
            HttpStatusCode.OK,
            JsonSerializer.Serialize(expectedVoices)
        );
        var httpClient = new HttpClient(mockHandler.Object)
        {
            BaseAddress = new Uri("http://localhost:5100")
        };

        // Act
        var response = await httpClient.GetFromJsonAsync<VoicesResponse>("/api/voices");

        // Assert
        Assert.NotNull(response);
        Assert.Equal(2, response.Voices.Count);
        Assert.Contains(response.Voices, v => v.Id == "en-US-Aria");
    }

    [Fact]
    public async Task GetVoicesAsync_ThrowsException_WhenApiReturnsError()
    {
        // Arrange
        var mockHandler = CreateMockHandler(HttpStatusCode.InternalServerError, "");
        var httpClient = new HttpClient(mockHandler.Object)
        {
            BaseAddress = new Uri("http://localhost:5100")
        };

        // Act & Assert
        await Assert.ThrowsAsync<HttpRequestException>(
            () => httpClient.GetFromJsonAsync<VoicesResponse>("/api/voices")
        );
    }

    #endregion

    #region GenerateSpeechAsync Tests

    [Fact]
    public async Task GenerateSpeechAsync_ReturnsAudioBytes_WhenApiSucceeds()
    {
        // Arrange
        var audioBytes = new byte[] { 0x52, 0x49, 0x46, 0x46 }; // WAV header start
        var mockHandler = CreateMockHandlerWithBytes(HttpStatusCode.OK, audioBytes, "audio/wav");
        var httpClient = new HttpClient(mockHandler.Object)
        {
            BaseAddress = new Uri("http://localhost:5100")
        };

        var request = new TtsRequest("Hello, world!", "en-US-Aria", "wav");

        // Act
        var response = await httpClient.PostAsJsonAsync("/api/tts", request);
        var resultBytes = await response.Content.ReadAsByteArrayAsync();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotEmpty(resultBytes);
    }

    [Fact]
    public async Task GenerateSpeechAsync_ThrowsException_WhenVoiceInvalid()
    {
        // Arrange
        var errorResponse = new ErrorResponse("Voice not found", "VALIDATION_ERROR");
        var mockHandler = CreateMockHandler(
            HttpStatusCode.BadRequest,
            JsonSerializer.Serialize(errorResponse)
        );
        var httpClient = new HttpClient(mockHandler.Object)
        {
            BaseAddress = new Uri("http://localhost:5100")
        };

        var request = new TtsRequest("Hello!", "invalid-voice", "wav");

        // Act
        var response = await httpClient.PostAsJsonAsync("/api/tts", request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GenerateSpeechAsync_ThrowsException_WhenTextEmpty()
    {
        // Arrange
        var errorResponse = new ErrorResponse("Text is required", "VALIDATION_ERROR");
        var mockHandler = CreateMockHandler(
            HttpStatusCode.BadRequest,
            JsonSerializer.Serialize(errorResponse)
        );
        var httpClient = new HttpClient(mockHandler.Object)
        {
            BaseAddress = new Uri("http://localhost:5100")
        };

        var request = new TtsRequest("", "en-US-Aria", "wav");

        // Act
        var response = await httpClient.PostAsJsonAsync("/api/tts", request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GenerateSpeechAsync_ThrowsException_WhenTextTooLong()
    {
        // Arrange - text exceeds 1000 character limit
        var longText = new string('x', 1001);
        var errorResponse = new ErrorResponse("Text exceeds maximum length", "VALIDATION_ERROR");
        var mockHandler = CreateMockHandler(
            HttpStatusCode.BadRequest,
            JsonSerializer.Serialize(errorResponse)
        );
        var httpClient = new HttpClient(mockHandler.Object)
        {
            BaseAddress = new Uri("http://localhost:5100")
        };

        var request = new TtsRequest(longText, "en-US-Aria", "wav");

        // Act
        var response = await httpClient.PostAsJsonAsync("/api/tts", request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    #endregion

    #region HealthCheckAsync Tests

    [Fact]
    public async Task HealthCheckAsync_ReturnsHealthy_WhenApiSucceeds()
    {
        // Arrange
        var healthResponse = new HealthResponse("healthy", true);
        var mockHandler = CreateMockHandler(
            HttpStatusCode.OK,
            JsonSerializer.Serialize(healthResponse)
        );
        var httpClient = new HttpClient(mockHandler.Object)
        {
            BaseAddress = new Uri("http://localhost:5100")
        };

        // Act
        var response = await httpClient.GetFromJsonAsync<HealthResponse>("/api/health");

        // Assert
        Assert.NotNull(response);
        Assert.Equal("healthy", response.Status);
        Assert.True(response.ModelLoaded);
    }

    [Fact]
    public async Task HealthCheckAsync_ThrowsException_WhenApiUnreachable()
    {
        // Arrange
        var mockHandler = CreateMockHandler(HttpStatusCode.ServiceUnavailable, "");
        var httpClient = new HttpClient(mockHandler.Object)
        {
            BaseAddress = new Uri("http://localhost:5100")
        };

        // Act & Assert
        await Assert.ThrowsAsync<HttpRequestException>(
            () => httpClient.GetFromJsonAsync<HealthResponse>("/api/health")
        );
    }

    #endregion

    #region Helper Methods

    private static Mock<HttpMessageHandler> CreateMockHandler(HttpStatusCode statusCode, string content)
    {
        var mockHandler = new Mock<HttpMessageHandler>();
        mockHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = statusCode,
                Content = new StringContent(content, System.Text.Encoding.UTF8, "application/json")
            });
        return mockHandler;
    }

    private static Mock<HttpMessageHandler> CreateMockHandlerWithBytes(
        HttpStatusCode statusCode, 
        byte[] content, 
        string contentType)
    {
        var mockHandler = new Mock<HttpMessageHandler>();
        mockHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = statusCode,
                Content = new ByteArrayContent(content)
                {
                    Headers = { { "Content-Type", contentType } }
                }
            });
        return mockHandler;
    }

    #endregion
}
