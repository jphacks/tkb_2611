"""すれ違いの相互確認 (DynamoDB)。

両方の端末が互いのトークンを報告したときだけ成立にする。記録は
enc#<day>#<小さい方のトークン>#<大きい方> の seen (報告したトークンの集合) だけで、2 日で消える。
同じ組は何度報告されても 1 件のまま。

トークンごとの報告の数には上限を設けない (docs/decisions.md)。トークンは誰でも名乗れるので、
上限は攻撃する人を止められず、他人のトークンを拾った人が持ち主を止められてしまうため。

記録はすれ違い専用のテーブル (infra/backend.yaml の EncounterTable) に置く。
トークンは端末やユーザーと結び付かない乱数だが、ログには出さない (.claude/rules/privacy.md)。
"""

from __future__ import annotations

import hashlib
import logging
from collections.abc import Callable
from datetime import date, datetime, timedelta, timezone
from typing import TYPE_CHECKING, NoReturn

from botocore.exceptions import BotoCoreError, ClientError

from penguin_backend.errors import ApiError

if TYPE_CHECKING:
    # 型のスタブは開発用の依存なので、型の検査のときだけ読む (aws.py と同じ)
    from mypy_boto3_dynamodb import DynamoDBClient

logger = logging.getLogger(__name__)

# JST は夏時間が無いので固定の時差で扱う (tz のデータに頼らない)。
JST = timezone(timedelta(hours=9))
# アプリの 1 日は 03:00 JST で区切る (時刻から 3 時間引いた JST の日付)。
DAY_STARTS_AT = timedelta(hours=3)
RECORD_LIFETIME = timedelta(days=2)
_UNAVAILABLE_MESSAGE = "いまはすれ違いを確かめられないみたい。少し待ってね"
# 混んでいる・一時的な失敗。それ以外の ClientError (権限・テーブル名の誤り) は 500 にする
_TRANSIENT = {
    "ProvisionedThroughputExceededException",
    "RequestLimitExceeded",
    "ThrottlingException",
    "InternalServerError",
    "ServiceUnavailable",
}


def app_day(now: datetime) -> date:
    return (now.astimezone(JST) - DAY_STARTS_AT).date()


def pair_id(a: str, b: str) -> str:
    """2 つのトークンの組の ID。並べる順によらず、両方の端末で同じ値になる。

    定義は api/openapi.yaml の EncounterResult.pair_id。
    """
    return hashlib.sha256((min(a, b) + max(a, b)).encode("ascii")).hexdigest()[:16]


def accepts_day(day: date, now: datetime) -> bool:
    return abs((day - app_day(now)).days) <= 1


class EncounterStore:
    def __init__(
        self, client: DynamoDBClient, table: str, *, clock: Callable[[], datetime]
    ) -> None:
        self._client = client
        self._table = table
        self._clock = clock

    def report(self, day: date, self_token: str, peer_token: str) -> bool:
        """報告を記録し、相手も自分を報告していれば True。同じ報告は何度送っても同じ結果になる。"""
        now = self._clock()
        ttl = str(int((now + RECORD_LIFETIME).timestamp()))
        low, high = sorted((self_token, peer_token))
        key = {"pk": {"S": f"enc#{day.isoformat()}#{low}#{high}"}}
        try:
            updated = self._client.update_item(
                TableName=self._table,
                Key=key,
                UpdateExpression="ADD seen :self SET #ttl = :ttl",
                ExpressionAttributeNames={"#ttl": "ttl"},
                ExpressionAttributeValues={":self": {"SS": [self_token]}, ":ttl": {"N": ttl}},
                ReturnValues="ALL_NEW",
            )
        except ClientError as error:
            self._raise(error)
        except BotoCoreError as error:
            logger.warning("encounter store failed: %s", type(error).__name__)
            raise ApiError("upstream_unavailable", 503, message=_UNAVAILABLE_MESSAGE) from error
        seen = set(updated["Attributes"]["seen"].get("SS", []))
        return seen == {self_token, peer_token}

    def _raise(self, error: ClientError) -> NoReturn:
        code = error.response.get("Error", {}).get("Code", "")
        logger.warning("encounter store failed: %s", code)
        if code in _TRANSIENT:
            raise ApiError("upstream_unavailable", 503, message=_UNAVAILABLE_MESSAGE) from error
        raise error
