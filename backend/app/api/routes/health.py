"""
Backward-compatible re-export of the platform-ops health router.

New code should import from `app.health.router`. This module remains so older
imports (`from app.api.routes import health`) keep working.
"""

from app.health.router import router

__all__ = ["router"]
