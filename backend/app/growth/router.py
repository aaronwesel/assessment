"""
Growth / marketing API stub — workstream C fills landing/register helpers if needed.

Most growth UI lives under `web/src/pages/Landing*` (out of scope for platform ops).
This empty mount keeps shared router includes stable for stacked PRs.
"""

from fastapi import APIRouter

router = APIRouter(prefix="/growth", tags=["growth"])


@router.get("/status")
def growth_status_stub() -> dict[str, object]:
    return {"status": "not_configured", "workstream": "C"}
