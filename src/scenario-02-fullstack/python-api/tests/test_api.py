"""
API endpoint tests for VibeVoice TTS backend.

These tests are based on the API contract from the design review.
They may need adjustment once Naomi's implementation is final.
"""
import pytest
from httpx import AsyncClient


@pytest.mark.anyio
class TestHealthEndpoint:
    """Tests for GET /api/health endpoint."""

    async def test_health_returns_healthy_status(self, async_client: AsyncClient):
        """Health endpoint should return healthy status."""
        response = await async_client.get("/api/health")
        
        assert response.status_code == 200
        data = response.json()
        assert data["status"] == "healthy"
        assert "model_loaded" in data

    async def test_health_returns_model_loaded_state(self, async_client: AsyncClient):
        """Health endpoint should indicate if TTS model is loaded."""
        response = await async_client.get("/api/health")
        
        assert response.status_code == 200
        data = response.json()
        assert isinstance(data.get("model_loaded"), bool)


@pytest.mark.anyio
class TestVoicesEndpoint:
    """Tests for GET /api/voices endpoint."""

    async def test_voices_returns_voice_list(self, async_client: AsyncClient):
        """Voices endpoint should return a list of available voices."""
        response = await async_client.get("/api/voices")
        
        assert response.status_code == 200
        data = response.json()
        assert "voices" in data
        assert isinstance(data["voices"], list)

    async def test_voices_have_required_fields(self, async_client: AsyncClient):
        """Each voice should have id, name, language, and style fields."""
        response = await async_client.get("/api/voices")
        
        assert response.status_code == 200
        data = response.json()
        
        for voice in data["voices"]:
            assert "id" in voice, "Voice missing 'id' field"
            assert "name" in voice, "Voice missing 'name' field"
            assert "language" in voice, "Voice missing 'language' field"
            assert "style" in voice, "Voice missing 'style' field"

    async def test_voices_list_not_empty(self, async_client: AsyncClient):
        """At least one voice should be available."""
        response = await async_client.get("/api/voices")
        
        assert response.status_code == 200
        data = response.json()
        assert len(data["voices"]) > 0, "No voices available"


@pytest.mark.anyio
class TestTtsEndpoint:
    """Tests for POST /api/tts endpoint."""

    async def test_tts_returns_audio_bytes(
        self, async_client: AsyncClient, sample_tts_request: dict
    ):
        """TTS endpoint should return audio data."""
        response = await async_client.post("/api/tts", json=sample_tts_request)
        
        assert response.status_code == 200
        assert response.headers["content-type"] == "audio/wav"
        assert len(response.content) > 0, "Audio response is empty"

    async def test_tts_with_invalid_voice_returns_error(
        self, async_client: AsyncClient
    ):
        """TTS endpoint should return error for invalid voice_id."""
        request = {
            "text": "Hello, world!",
            "voice_id": "invalid-voice-id",
            "output_format": "wav"
        }
        response = await async_client.post("/api/tts", json=request)
        
        assert response.status_code in [400, 404, 422]
        data = response.json()
        assert "error" in data or "detail" in data

    async def test_tts_without_text_returns_error(self, async_client: AsyncClient):
        """TTS endpoint should return error when text is missing."""
        request = {
            "voice_id": "en-US-Aria",
            "output_format": "wav"
        }
        response = await async_client.post("/api/tts", json=request)
        
        assert response.status_code == 422
        
    async def test_tts_with_empty_text_returns_error(self, async_client: AsyncClient):
        """TTS endpoint should return error for empty text."""
        request = {
            "text": "",
            "voice_id": "en-US-Aria",
            "output_format": "wav"
        }
        response = await async_client.post("/api/tts", json=request)
        
        assert response.status_code in [400, 422]

    async def test_tts_with_long_text_returns_error(self, async_client: AsyncClient):
        """TTS endpoint should reject text exceeding 1000 characters."""
        request = {
            "text": "x" * 1001,  # Exceeds 1000 char limit from contract
            "voice_id": "en-US-Aria",
            "output_format": "wav"
        }
        response = await async_client.post("/api/tts", json=request)
        
        assert response.status_code in [400, 422]


@pytest.mark.anyio
class TestApiErrorHandling:
    """Tests for API error handling."""

    async def test_invalid_endpoint_returns_404(self, async_client: AsyncClient):
        """Non-existent endpoints should return 404."""
        response = await async_client.get("/api/nonexistent")
        
        assert response.status_code == 404

    async def test_invalid_method_returns_405(self, async_client: AsyncClient):
        """Wrong HTTP method should return 405."""
        response = await async_client.post("/api/health")
        
        assert response.status_code == 405
