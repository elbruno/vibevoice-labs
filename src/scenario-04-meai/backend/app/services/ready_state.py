"""
Ready State Manager - Tracks backend initialization and readiness.
"""

import logging
import time
from enum import Enum
from typing import Dict, Any, Optional
from datetime import datetime
from threading import Lock

logger = logging.getLogger(__name__)


class State(str, Enum):
    """Backend initialization states."""
    INITIALIZING = "INITIALIZING"
    LOADING_MODELS = "LOADING_MODELS"
    WARMING_UP = "WARMING_UP"
    READY = "READY"
    ERROR = "ERROR"


class ServiceStatus:
    """Status of an individual service."""
    
    def __init__(self, name: str):
        self.name = name
        self.ready = False
        self.status = "pending"
        self.error: Optional[str] = None
        self.warmup_time_ms: Optional[float] = None
        self.loaded_at: Optional[str] = None
        self.metadata: Dict[str, Any] = {}
    
    def to_dict(self) -> Dict[str, Any]:
        result = {
            "ready": self.ready,
            "status": self.status
        }
        if self.error:
            result["error"] = self.error
        if self.warmup_time_ms is not None:
            result["warmup_time_ms"] = self.warmup_time_ms
        if self.loaded_at:
            result["loaded_at"] = self.loaded_at
        result.update(self.metadata)
        return result


class ReadyStateManager:
    """
    Singleton manager for backend readiness state.
    Thread-safe tracking of initialization progress.
    """
    
    _instance = None
    _lock = Lock()
    
    def __new__(cls):
        if cls._instance is None:
            with cls._lock:
                if cls._instance is None:
                    cls._instance = super().__new__(cls)
                    cls._instance._initialized = False
        return cls._instance
    
    def __init__(self):
        if self._initialized:
            return
        
        self._state = State.INITIALIZING
        self._progress = 0
        self._services: Dict[str, ServiceStatus] = {
            "tts": ServiceStatus("tts"),
            "chat": ServiceStatus("chat"),
            "stt": ServiceStatus("stt")
        }
        self._errors = []
        self._startup_start_time = time.time()
        self._startup_time_ms: Optional[float] = None
        self._state_lock = Lock()
        self._initialized = True
        
        logger.info("ReadyState manager initialized")
    
    def set_state(self, state: State, progress: Optional[int] = None):
        """Update the overall state and optionally progress."""
        with self._state_lock:
            old_state = self._state
            self._state = state
            
            if progress is not None:
                self._progress = max(0, min(100, progress))
            
            logger.info(f"State transition: {old_state} -> {state} (progress: {self._progress}%)")
            
            # Calculate startup time when reaching READY or ERROR
            if state in (State.READY, State.ERROR) and self._startup_time_ms is None:
                self._startup_time_ms = (time.time() - self._startup_start_time) * 1000
                logger.info(f"Initialization completed in {self._startup_time_ms:.2f}ms")
    
    def get_state(self) -> State:
        """Get current state."""
        with self._state_lock:
            return self._state
    
    def get_progress(self) -> int:
        """Get current progress (0-100)."""
        with self._state_lock:
            return self._progress
    
    def mark_service_ready(
        self, 
        service_name: str, 
        warmup_time_ms: Optional[float] = None,
        **metadata
    ):
        """Mark a service as ready."""
        with self._state_lock:
            if service_name in self._services:
                service = self._services[service_name]
                service.ready = True
                service.status = "ready"
                service.warmup_time_ms = warmup_time_ms
                service.loaded_at = datetime.utcnow().isoformat() + "Z"
                service.metadata.update(metadata)
                logger.info(f"Service '{service_name}' marked as ready")
    
    def mark_service_error(self, service_name: str, error: str):
        """Mark a service as failed."""
        with self._state_lock:
            if service_name in self._services:
                service = self._services[service_name]
                service.ready = False
                service.status = "error"
                service.error = error
                logger.error(f"Service '{service_name}' failed: {error}")
    
    def mark_service_loading(self, service_name: str):
        """Mark a service as currently loading."""
        with self._state_lock:
            if service_name in self._services:
                service = self._services[service_name]
                service.ready = False
                service.status = "loading"
                logger.info(f"Service '{service_name}' loading...")
    
    def is_service_ready(self, service_name: str) -> bool:
        """Check if a specific service is ready."""
        with self._state_lock:
            return self._services.get(service_name, ServiceStatus("")).ready
    
    def add_error(self, error: str):
        """Add an error message."""
        with self._state_lock:
            self._errors.append(error)
            logger.error(f"Ready state error: {error}")
    
    def is_ready(self) -> bool:
        """
        Check if backend is ready.
        Required services: TTS and Chat
        Optional services: STT
        """
        with self._state_lock:
            return (
                self._state == State.READY and
                self._services["tts"].ready and
                self._services["chat"].ready
                # STT is optional
            )
    
    def to_dict(self) -> Dict[str, Any]:
        """Get complete state as dictionary."""
        with self._state_lock:
            services_dict = {
                name: service.to_dict()
                for name, service in self._services.items()
            }
            
            return {
                "ready": self.is_ready(),
                "state": self._state.value,
                "progress": self._progress,
                "services": services_dict,
                "startup_time_ms": self._startup_time_ms,
                "errors": self._errors.copy()
            }
    
    def reset(self):
        """Reset state (useful for testing)."""
        with self._state_lock:
            self._state = State.INITIALIZING
            self._progress = 0
            self._services = {
                "tts": ServiceStatus("tts"),
                "chat": ServiceStatus("chat"),
                "stt": ServiceStatus("stt")
            }
            self._errors = []
            self._startup_start_time = time.time()
            self._startup_time_ms = None
            logger.info("ReadyState reset")


# Global singleton instance
ready_state = ReadyStateManager()
