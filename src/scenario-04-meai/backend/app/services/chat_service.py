"""
Chat Service - AI conversation brain using Ollama.
"""

import os
import logging
import ollama

logger = logging.getLogger(__name__)

SYSTEM_PROMPT = (
    "You are a friendly conversational assistant. "
    "Keep responses concise (1-3 sentences) for natural spoken dialogue."
)


class ChatService:
    """Manages a conversation with local Ollama LLM."""

    def __init__(self):
        self.model = os.environ.get("OLLAMA_MODEL", "llama3.2")
        self.base_url = os.environ.get("OLLAMA_BASE_URL", "http://localhost:11434")
        self.history: list[dict] = []
        self.client = None
        
        # Defer client creation to avoid crashes on initialization
        self._initialize_client()
        
    def _initialize_client(self):
        """Initialize Ollama client with error handling."""
        try:
            # Configure Ollama client
            if self.base_url != "http://localhost:11434":
                self.client = ollama.Client(host=self.base_url)
            else:
                self.client = ollama.Client()
            
            logger.info(f"Chat service using Ollama model: {self.model} at {self.base_url}")
        except Exception as e:
            logger.error(f"Failed to initialize Ollama client: {e}")
            self.client = None

    def chat(self, user_text: str) -> str:
        """Send user text, get AI response. Maintains conversation history."""
        # Ensure client is initialized
        if not self.client:
            self._initialize_client()
            if not self.client:
                raise RuntimeError(
                    f"Ollama client not available. "
                    f"Ensure Ollama is running at {self.base_url} "
                    f"and model '{self.model}' is installed."
                )
        
        self.history.append({"role": "user", "content": user_text})

        try:
            response = self.client.chat(
                model=self.model,
                messages=[
                    {"role": "system", "content": SYSTEM_PROMPT},
                    *self.history,
                ],
            )
            ai_text = response['message']['content']
            self.history.append({"role": "assistant", "content": ai_text})
            logger.info(f"Chat response: '{ai_text[:80]}...'")
            return ai_text
        except Exception as e:
            logger.error(f"Chat error: {e}")
            # Re-raise with more context
            raise RuntimeError(f"Ollama chat failed: {e}") from e

    def reset(self):
        """Clear conversation history."""
        self.history.clear()

    @staticmethod
    def is_available() -> bool:
        """Check if Ollama is available and the model exists."""
        try:
            model = os.environ.get("OLLAMA_MODEL", "llama3.2")
            base_url = os.environ.get("OLLAMA_BASE_URL", "http://localhost:11434")
            
            # Try to connect to Ollama
            if base_url != "http://localhost:11434":
                client = ollama.Client(host=base_url)
            else:
                client = ollama.Client()
            
            # Check if the model is available
            models = client.list()
            model_names = [m['name'].replace(':latest', '') for m in models.get('models', [])]
            
            # Check if our model is in the list (with or without :latest tag)
            model_base = model.replace(':latest', '')
            is_available = any(model_base in name or name in model_base for name in model_names)
            
            if not is_available:
                logger.warning(f"Ollama model '{model}' not found. Available models: {model_names}")
            
            return is_available
        except Exception as e:
            logger.error(f"Ollama availability check failed: {e}")
            return False
