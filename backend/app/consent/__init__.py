"""Consent flags + AI processing gates (DeepSeek / Rekognition)."""

from app.consent.gate import ConsentRequired, require_deepseek_consent, require_rekognition_consent
from app.consent.models import ConsentRecord

__all__ = [
    "ConsentRecord",
    "ConsentRequired",
    "require_deepseek_consent",
    "require_rekognition_consent",
]
