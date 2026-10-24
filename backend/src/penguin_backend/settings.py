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

    aws_region: str = "ap-northeast-1"
    # Vercel の関数が x-vercel-oidc-token で借りるロール (infra/backend.yaml の VercelBackendRole・
    # VercelPreviewRole)。環境ごとに Vercel の環境変数で入れる。
    # 空なら手元の既定の認証情報 (aws login) を使う。
    aws_role_arn: str | None = None
    # 写真を見る AI (project-penguin の ADR 0001)。jp. のプロファイル以外は IAM で拒まれる。
    nova_model_id: str = "jp.amazon.nova-2-lite-v1:0"

    # みんなの氷山のテーブル。既定は開発用にする (preview のロールは開発用のテーブルにしか
    # 触れないので、本番のテーブル名は production の環境変数でだけ入れる)。
    community_table: str = "penguin-backend-community-dev"
    # すれ違いの記録のテーブル。空ならみんなの氷山と同じテーブルに同居させる
    # (docs/decisions.md の近道)。
    encounter_table: str | None = None

    @field_validator("cors_origins", mode="before")
    @classmethod
    def _split_origins(cls, value: object) -> object:
        if isinstance(value, str):
            return [origin.strip() for origin in value.split(",") if origin.strip()]
        return value

    @property
    def encounter_table_name(self) -> str:
        return self.encounter_table or self.community_table
