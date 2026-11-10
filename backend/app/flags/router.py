"""Admin feature-flag / kill-switch HTTP API."""

from __future__ import annotations

from fastapi import APIRouter, HTTPException, status
from pydantic import BaseModel, Field

from app.api.deps import RedisDep, RequireAdmin, SessionDep
from app.audit.service import record_event
from app.flags import service as flag_service

router = APIRouter(prefix="/flags", tags=["flags"])


class FlagOut(BaseModel):
    name: str
    description: str
    enabled: bool
    override: bool | None = None
    default: bool


class FlagUpdateRequest(BaseModel):
    enabled: bool = Field(description="Force on/off kill switch in Redis")


@router.get("", response_model=list[FlagOut])
def list_feature_flags(_auth: RequireAdmin, redis: RedisDep) -> list[FlagOut]:
    return [FlagOut(**item) for item in flag_service.list_flags(redis=redis)]


@router.put("/{name}", response_model=FlagOut)
def set_feature_flag(
    auth: RequireAdmin,
    session: SessionDep,
    redis: RedisDep,
    name: str,
    body: FlagUpdateRequest,
) -> FlagOut:
    try:
        flag_service.set_flag(
            name, body.enabled, redis=redis, actor_user_id=auth.subject
        )
    except ValueError as exc:
        raise HTTPException(status_code=status.HTTP_404_NOT_FOUND, detail=str(exc)) from exc
    record_event(
        session,
        action="feature_flag_set",
        resource_type="feature_flag",
        actor_user_id=auth.subject,
        resource_id=name,
        metadata={"enabled": body.enabled},
    )
    items = {i["name"]: i for i in flag_service.list_flags(redis=redis)}
    return FlagOut(**items[name])


@router.delete("/{name}", response_model=FlagOut)
def clear_feature_flag(
    auth: RequireAdmin,
    session: SessionDep,
    redis: RedisDep,
    name: str,
) -> FlagOut:
    try:
        flag_service.clear_flag(name, redis=redis)
    except ValueError as exc:
        raise HTTPException(status_code=status.HTTP_404_NOT_FOUND, detail=str(exc)) from exc
    record_event(
        session,
        action="feature_flag_cleared",
        resource_type="feature_flag",
        actor_user_id=auth.subject,
        resource_id=name,
        metadata={},
    )
    items = {i["name"]: i for i in flag_service.list_flags(redis=redis)}
    return FlagOut(**items[name])
