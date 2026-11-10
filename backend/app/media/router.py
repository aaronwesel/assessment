"""Signed media URL HTTP surface (S3-minded stub)."""

from __future__ import annotations

from fastapi import APIRouter, HTTPException, status
from pydantic import BaseModel, Field

from app.api.deps import RequireUser
from app.media.signed_urls import MediaUrlError, create_signed_url

router = APIRouter(prefix="/media", tags=["media"])


class SignedUrlRequest(BaseModel):
    object_key: str = Field(min_length=1, max_length=512)
    method: str = Field(default="GET", max_length=8)
    ttl_seconds: int | None = Field(default=None, ge=30, le=86400)
    content_type: str | None = Field(default=None, max_length=128)


class SignedUrlResponse(BaseModel):
    url: str
    method: str
    object_key: str
    expires_at: int
    ttl_seconds: int


@router.post("/signed-url", response_model=SignedUrlResponse)
def issue_signed_media_url(_auth: RequireUser, body: SignedUrlRequest) -> SignedUrlResponse:
    """
    Issue a short-lived signed URL for GET/PUT of an opaque object key.

    Callers must already authorize access to the object; this endpoint only
    mints a time-boxed capability URL (stub today, S3 presign in AWS).
    """
    try:
        signed = create_signed_url(
            object_key=body.object_key,
            method=body.method,
            ttl_seconds=body.ttl_seconds,
            content_type=body.content_type,
        )
    except MediaUrlError as exc:
        raise HTTPException(status_code=status.HTTP_400_BAD_REQUEST, detail=str(exc)) from exc
    return SignedUrlResponse(
        url=signed.url,
        method=signed.method,
        object_key=signed.object_key,
        expires_at=signed.expires_at,
        ttl_seconds=signed.ttl_seconds,
    )
