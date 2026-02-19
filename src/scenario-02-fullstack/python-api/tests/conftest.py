"""
Pytest fixtures for FastAPI test client.
"""
import pytest
from httpx import AsyncClient, ASGITransport


@pytest.fixture
def anyio_backend():
    """Use asyncio backend for async tests."""
    return "asyncio"


@pytest.fixture
async def async_client():
    """
    Create an async test client for the FastAPI application.
    
    Note: Import path will need adjustment once Naomi's implementation is final.
    Expected import: from app.main import app (or from main import app)
    """
    # Placeholder import - adjust path based on final implementation structure
    try:
        from main import app
    except ImportError:
        try:
            from app.main import app
        except ImportError:
            pytest.skip("API not yet implemented - waiting for Naomi's implementation")
            return
    
    transport = ASGITransport(app=app)
    async with AsyncClient(transport=transport, base_url="http://test") as client:
        yield client


@pytest.fixture
def sample_tts_request():
    """Sample TTS request payload matching API contract."""
    return {
        "text": "Hello, world!",
        "voice_id": "en-US-Aria",
        "output_format": "wav"
    }


@pytest.fixture
def sample_voice():
    """Sample voice object matching API contract."""
    return {
        "id": "en-US-Aria",
        "name": "Aria",
        "language": "en-US",
        "style": "general"
    }
