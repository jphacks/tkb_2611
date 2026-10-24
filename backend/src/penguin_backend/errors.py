"""契約のエラーの形 {"error": {code, message, retryable}} を 1 か所で作る。

決まりは .claude/rules/backend.md。

code・HTTP の状態・retryable は api/openapi.yaml の ErrorCode の表に合わせる。
文は画面にそのまま出すので、入力の中身 (写真・トークン) を入れない。
"""

from typing import Literal

from fastapi import Request
from fastapi.responses import JSONResponse

ErrorCode = Literal[
    "image_missing",
    "image_invalid",
    "image_too_large",
    "token_invalid",
    "day_invalid",
    "rate_limited",
    "upstream_unavailable",
    "internal",
]

# 画面に出す既定の文 (api/examples/error.*.json と同じ)。
MESSAGES: dict[ErrorCode, str] = {
    "image_missing": "写真が届かなかったよ。もう一度撮ってね",
    "image_invalid": "この写真は読めなかったよ。もう一度撮ってね",
    "image_too_large": "写真が大きすぎたよ。もう一度撮ってね",
    "token_invalid": "すれ違いの記録を読めなかったよ",
    "day_invalid": "端末の日付を確かめてね",
    "rate_limited": "混みあってるみたい。少し待ってからもう一度ためしてね",
    "upstream_unavailable": "いまは写真を見分けられないみたい。少し待ってね",
    "internal": "うまくいかなかったよ。少し待ってからもう一度ためしてね",
}

_RETRYABLE: set[ErrorCode] = {"rate_limited", "upstream_unavailable", "internal"}
# 429・503 に付ける Retry-After の既定の秒数。
DEFAULT_RETRY_AFTER = 5


class ApiError(Exception):
    """契約のエラーとして応答する例外。ルートや下の層から投げ、app の例外ハンドラが形にする。"""

    def __init__(
        self,
        code: ErrorCode,
        status: int,
        *,
        message: str | None = None,
        retry_after: int | None = None,
    ) -> None:
        super().__init__(code)
        self.code = code
        self.status = status
        self.message = message or MESSAGES[code]
        if retry_after is None and status in (429, 503):
            retry_after = DEFAULT_RETRY_AFTER
        self.retry_after = retry_after


def error_response(error: ApiError) -> JSONResponse:
    headers = {"Cache-Control": "no-store"}
    if error.retry_after is not None:
        headers["Retry-After"] = str(max(1, error.retry_after))
    body = {
        "error": {
            "code": error.code,
            "message": error.message,
            "retryable": error.code in _RETRYABLE,
        }
    }
    return JSONResponse(body, status_code=error.status, headers=headers)


async def handle_api_error(_request: Request, error: Exception) -> JSONResponse:
    if not isinstance(error, ApiError):  # ApiError にだけ登録するので来ない
        raise error
    return error_response(error)
