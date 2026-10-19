"""環境変数から読む設定。値はコードにも設定ファイルにも書かない (.claude/rules/backend.md)。"""

from typing import Annotated

from pydantic import field_validator
from pydantic_settings import BaseSettings, NoDecode, SettingsConfigDict


class Settings(BaseSettings):
    """`PENGUIN_` で始まる環境変数を読む。"""

    model_config = SettingsConfigDict(env_prefix="PENGUIN_", extra="ignore")

    # 別のオリジンから呼ぶときだけ許可する配信元 (カンマ区切り)。
    # 本番は Web ビルドと API が同じ URL (Vercel) なので空のままでよい。
    # 手元で Web ビルドを別のポートから開いて確かめるときに
    # PENGUIN_CORS_ORIGINS=http://localhost:8090 のように渡す。
    cors_origins: Annotated[list[str], NoDecode] = []

    @field_validator("cors_origins", mode="before")
    @classmethod
    def _split_origins(cls, value: object) -> object:
        if isinstance(value, str):
            return [origin.strip() for origin in value.split(",") if origin.strip()]
        return value
