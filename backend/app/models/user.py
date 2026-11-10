"""
User model stub — account identity only; no domain PHI fields.

Stores email + optional bcrypt hash for password auth, and a coarse RBAC role.
JWT `sub` is the opaque `id` UUID — prefer that over email in tokens and logs.

MFA columns:
- `mfa_enabled` — when True, password login requires a valid TOTP / backup code.
- `mfa_policy_required` — healthcare (and similar) accounts must enroll MFA before full app use.
- `mfa_secret_encrypted` — Fernet ciphertext of the base32 TOTP seed (never plaintext).
- `mfa_backup_codes_hashed` — JSON array of bcrypt hashes; plaintext codes shown once.

Do not add medical/clinical columns here without an explicit HIPAA data model.
"""

from datetime import datetime
from enum import Enum
from typing import Optional
from uuid import uuid4

from sqlalchemy import Column, DateTime, Text, func
from sqlmodel import Field, SQLModel


class UserRole(str, Enum):
    USER = "user"
    ADMIN = "admin"
    MODERATOR = "moderator"


class User(SQLModel, table=True):
    __tablename__ = "users"

    id: str = Field(default_factory=lambda: str(uuid4()), primary_key=True)
    email: str = Field(index=True, unique=True, max_length=320)
    hashed_password: Optional[str] = Field(default=None, max_length=255)
    full_name: Optional[str] = Field(default=None, max_length=255)
    role: UserRole = Field(default=UserRole.USER)
    is_active: bool = Field(default=True)

    # MFA (TOTP). Secrets encrypted at rest; backup codes hashed.
    mfa_enabled: bool = Field(default=False)
    mfa_policy_required: bool = Field(default=False)
    mfa_secret_encrypted: Optional[str] = Field(default=None, max_length=512)
    mfa_backup_codes_hashed: Optional[str] = Field(
        default=None,
        sa_column=Column(Text, nullable=True),
    )

    created_at: datetime | None = Field(
        default=None,
        sa_column=Column(DateTime(timezone=True), server_default=func.now(), nullable=False),
    )
    updated_at: datetime | None = Field(
        default=None,
        sa_column=Column(
            DateTime(timezone=True),
            server_default=func.now(),
            onupdate=func.now(),
            nullable=False,
        ),
    )
