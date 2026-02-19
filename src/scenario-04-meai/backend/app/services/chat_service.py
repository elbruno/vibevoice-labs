"""
Chat Service - AI conversation brain using OpenAI.
"""

import logging
from openai import OpenAI

logger = logging.getLogger(__name__)

SYSTEM_PROMPT = (
    "You are a friendly conversational assistant. "
    "Keep responses concise (1-3 sentences) for natural spoken dialogue."
)


class ChatService:
    """Manages a conversation with OpenAI's chat completion API."""

    def __init__(self):
        self.client = OpenAI()  # Uses OPENAI_API_KEY env var
        self.history: list[dict] = []
        self.model = "gpt-4o-mini"

    def chat(self, user_text: str) -> str:
        """Send user text, get AI response. Maintains conversation history."""
        self.history.append({"role": "user", "content": user_text})

        try:
            response = self.client.chat.completions.create(
                model=self.model,
                messages=[
                    {"role": "system", "content": SYSTEM_PROMPT},
                    *self.history,
                ],
            )
            ai_text = response.choices[0].message.content
            self.history.append({"role": "assistant", "content": ai_text})
            logger.info(f"Chat response: '{ai_text[:80]}...'")
            return ai_text
        except Exception as e:
            logger.error(f"Chat error: {e}")
            raise

    def reset(self):
        """Clear conversation history."""
        self.history.clear()

    @staticmethod
    def is_available() -> bool:
        """Check if OpenAI API key is configured."""
        import os
        return bool(os.environ.get("OPENAI_API_KEY"))
