"""
AI consent gate — DeepSeek / Rekognition must not run without active consent.

Call `require_ai_consent` (or the purpose-specific helpers) before any outbound
AI call. Combines:
1. Redis/env feature flag kill switch (`flags.is_feature_enabled`)
2. Per-subject consent row (`consent_records`)

Raises `ConsentRequired` so HTTP layers can map to 403 without leaking PHI.
"""

from __future__ import annotations

from sqlmodel import Session

from app.consent.models import PURPOSE_AI_DEEPSEEK, PURPOSE_AI_REKOGNITION
from app.consent.service import is_consent_granted
from app.flags.service import is_feature_enabled


class ConsentRequired(PermissionError):
    """Raised when AI processing is blocked by missing consent or a kill switch."""

    def __init__(self, purpose: str, *, reason: str) -> None:
        self.purpose = purpose
        self.reason = reason
        super().__init__(f"consent required for {purpose}: {reason}")


def require_ai_consent(
    session: Session,
    *,
    subject_user_id: str,
    purpose: str,
    flag_name: str | None = None,
) -> None:
    """
    Raise ConsentRequired unless the subject has an active grant for `purpose`.

    Feature-flag / kill-switch checks belong in the AI clients themselves so
    operators get a clear "disabled" error distinct from missing consent.
    When `flag_name` is provided and the flag is off we still raise here with
    `feature_disabled` for callers that want a single gate (optional).
    """
    if not subject_user_id:
        raise ConsentRequired(purpose, reason="missing_subject")
    if not is_consent_granted(session, subject_user_id=subject_user_id, purpose=purpose):
        raise ConsentRequired(purpose, reason="consent_not_granted")
    if flag_name is not None and not is_feature_enabled(flag_name):
        raise ConsentRequired(purpose, reason="feature_disabled")


def require_deepseek_consent(session: Session, *, subject_user_id: str) -> None:
    # Consent only — DeepSeek client enforces env/Redis kill switches.
    require_ai_consent(
        session,
        subject_user_id=subject_user_id,
        purpose=PURPOSE_AI_DEEPSEEK,
        flag_name=None,
    )


def require_rekognition_consent(session: Session, *, subject_user_id: str) -> None:
    require_ai_consent(
        session,
        subject_user_id=subject_user_id,
        purpose=PURPOSE_AI_REKOGNITION,
        flag_name=None,
    )
