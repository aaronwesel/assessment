"""Time-boxed emergency access with mandatory reason + alert hooks."""

from app.break_glass.models import BreakGlassGrant
from app.break_glass.service import create_grant, has_active_grant, revoke_grant

__all__ = ["BreakGlassGrant", "create_grant", "has_active_grant", "revoke_grant"]
