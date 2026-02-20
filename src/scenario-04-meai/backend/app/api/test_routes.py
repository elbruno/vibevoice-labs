"""
Test API Routes - Endpoints for testing connectivity and backend status.
"""

import logging
from fastapi import APIRouter, Request
from typing import Dict, Any

logger = logging.getLogger(__name__)

router = APIRouter()

@router.get("/ping")
async def ping():
    """Simple ping endpoint to verify backend is reachable."""
    return {"status": "ok", "message": "pong"}

@router.post("/echo")
async def echo(request: Request) -> Dict[str, Any]:
    """Echo endpoint to test request/response payload handling."""
    try:
        body = await request.json()
        return {"status": "ok", "echo": body}
    except Exception as e:
        return {"status": "error", "message": f"Failed to parse JSON: {str(e)}"}

@router.get("/headers")
async def get_headers(request: Request):
    """Returns the headers received by the backend, useful for debugging proxies/Aspire."""
    return {"status": "ok", "headers": dict(request.headers)}
