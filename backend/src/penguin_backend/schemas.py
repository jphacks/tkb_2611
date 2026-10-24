"""契約 (api/openapi.yaml) の本文の Pydantic モデル。名前と制約は契約と同じにする。"""

from typing import Literal

from pydantic import BaseModel, ConfigDict, Field

# 守っている契約の版 (api/openapi.yaml の info.version)。
SCHEMA_VERSION = "1.2.0"

WaterContainer = Literal["pet_bottle_350", "pet_bottle_500", "unknown"]
WaterDecision = Literal["accept", "retake", "reject"]


class Check(BaseModel):
    id: str = Field(pattern=r"^[a-z0-9][a-z0-9_-]{0,63}$")
    label: str = Field(max_length=30)
    passed: bool
    detail: str | None = Field(default=None, max_length=40)


class WaterEstimate(BaseModel):
    schema_version: str = SCHEMA_VERSION
    container: WaterContainer
    fill_ratio: float = Field(ge=0, le=1)
    amount_ml: int = Field(ge=0, le=2000)
    confidence: float = Field(ge=0, le=1)
    decision: WaterDecision
    checks: list[Check] = Field(default_factory=list, max_length=8)
    retake_hint: str | None = Field(default=None, max_length=60)
    comment: str | None = Field(default=None, max_length=40)


EncounterToken = str


class EncounterReport(BaseModel):
    model_config = ConfigDict(extra="forbid", strict=True)

    day: str = Field(pattern=r"^\d{4}-\d{2}-\d{2}$")
    self_token: EncounterToken = Field(pattern=r"^[A-Za-z0-9_-]{22}$")
    peer_token: EncounterToken = Field(pattern=r"^[A-Za-z0-9_-]{22}$")


class EncounterResult(BaseModel):
    schema_version: str = SCHEMA_VERSION
    matched: bool
    pair_id: str = Field(pattern=r"^[0-9a-f]{16}$")
