"""AWS のクライアント (Bedrock・DynamoDB) を用意する。

Vercel の関数は、要求ごとに付く x-vercel-oidc-token で IAM ロール (Settings.aws_role_arn) を借り、
1 時間で切れる認証情報で呼ぶ (infra/README.md、project-penguin の ADR 0003)。
長く使えるキーは置かない。
ロールが設定されていない手元では、既定の認証情報 (aws login) を使う。

ルートはクライアントを作らず、create_app(clients=...) で渡された ClientsProvider から受け取る。
テストはここに偽物 (Stubber・moto) を渡す。
"""

from __future__ import annotations

import logging
import threading
import time
from collections.abc import Callable
from dataclasses import dataclass
from typing import TYPE_CHECKING

import boto3
from botocore.config import Config
from fastapi import Request

from penguin_backend.settings import Settings

if TYPE_CHECKING:
    # 型のスタブ (mypy-boto3-*) は開発用の依存で、本番の関数には入らない。型の検査のときだけ読む
    from mypy_boto3_bedrock_runtime import BedrockRuntimeClient
    from mypy_boto3_dynamodb import DynamoDBClient
    from mypy_boto3_sts import STSClient

logger = logging.getLogger(__name__)

OIDC_HEADER = "x-vercel-oidc-token"
# 借りた認証情報を、切れるこの秒数前まで使い回す。
_REFRESH_MARGIN_SECONDS = 300
_SESSION_SECONDS = 3600

# クライアントは 25 秒で切るので、AI の呼び出しは 15 秒で切る (.claude/rules/backend.md)。
# 送り直しは 1 回だけにし、遅れを積み増さない。
_BEDROCK_CONFIG = Config(
    connect_timeout=3, read_timeout=15, retries={"max_attempts": 2, "mode": "standard"}
)
_DYNAMODB_CONFIG = Config(
    connect_timeout=2, read_timeout=5, retries={"max_attempts": 3, "mode": "standard"}
)


@dataclass(frozen=True)
class Clients:
    bedrock: BedrockRuntimeClient
    dynamodb: DynamoDBClient


ClientsProvider = Callable[[Request], Clients]


def fixed(clients: Clients) -> ClientsProvider:
    """いつも同じクライアントを返す (テストや、手元で作ったクライアントを渡すとき)。"""
    return lambda _request: clients


def _clients_of(session: boto3.Session) -> Clients:
    return Clients(
        bedrock=session.client("bedrock-runtime", config=_BEDROCK_CONFIG),
        dynamodb=session.client("dynamodb", config=_DYNAMODB_CONFIG),
    )


class AwsClients:
    """本番の ClientsProvider。借りた認証情報とクライアントを、切れる少し前まで使い回す。"""

    def __init__(
        self,
        settings: Settings,
        *,
        sts: STSClient | None = None,
        clock: Callable[[], float] = time.time,
    ) -> None:
        self._settings = settings
        self._sts = sts
        self._clock = clock
        self._lock = threading.Lock()
        self._cached: Clients | None = None
        self._expires_at = 0.0

    def __call__(self, request: Request) -> Clients:
        with self._lock:
            if self._cached is not None and self._clock() < self._expires_at:
                return self._cached
            role_arn = self._settings.aws_role_arn
            if role_arn is None:
                self._cached = _clients_of(boto3.Session(region_name=self._settings.aws_region))
                self._expires_at = float("inf")
                return self._cached
            token = request.headers.get(OIDC_HEADER)
            if not token:
                # Vercel の関数なら必ず付く。無いのは設定の誤りなので 500 にする (値は記録しない)
                raise RuntimeError(f"{OIDC_HEADER} が無いのでロールを借りられない")
            self._cached, self._expires_at = self._assume(role_arn, token)
            return self._cached

    def _assume(self, role_arn: str, token: str) -> tuple[Clients, float]:
        sts = self._sts or boto3.client("sts", region_name=self._settings.aws_region)
        started = self._clock()
        credentials = sts.assume_role_with_web_identity(
            RoleArn=role_arn,
            RoleSessionName="penguin-backend",
            WebIdentityToken=token,
            DurationSeconds=_SESSION_SECONDS,
        )["Credentials"]
        session = boto3.Session(
            aws_access_key_id=credentials["AccessKeyId"],
            aws_secret_access_key=credentials["SecretAccessKey"],
            aws_session_token=credentials["SessionToken"],
            region_name=self._settings.aws_region,
        )
        expires_at = credentials["Expiration"].timestamp() - _REFRESH_MARGIN_SECONDS
        logger.info("assumed the backend role (%.0f ms)", (self._clock() - started) * 1000)
        return _clients_of(session), expires_at
