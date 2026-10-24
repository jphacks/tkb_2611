"""写真の本文 (multipart/form-data の image) をメモリだけで受け取る (.claude/rules/privacy.md)。

FastAPI の UploadFile は 1 MB を超えた部分を一時ファイルに退避する。ここでは本文の大きさを先に
上限で止め、退避の閾値をその上限より大きくしたパーサーで読むので、写真はディスクに触れない。
"""

from collections.abc import AsyncGenerator

from fastapi import Request
from starlette.datastructures import UploadFile
from starlette.formparsers import MultiPartException, MultiPartParser

from penguin_backend.errors import ApiError

# 契約の上限 (api/openapi.yaml): JPEG は 1 MB まで。
MAX_IMAGE_BYTES = 1024 * 1024
# multipart の区切りと項目の見出しの分。本文全体はこれを超えたら読まずに 413 にする。
_ENVELOPE_BYTES = 16 * 1024
MAX_BODY_BYTES = MAX_IMAGE_BYTES + _ENVELOPE_BYTES

_JPEG_START = b"\xff\xd8"
_JPEG_END = b"\xff\xd9"


async def _read_body(request: Request) -> bytes:
    declared = request.headers.get("content-length")
    if declared is not None and declared.isdigit() and int(declared) > MAX_BODY_BYTES:
        raise ApiError("image_too_large", 413)
    body = bytearray()
    async for chunk in request.stream():
        body += chunk
        if len(body) > MAX_BODY_BYTES:
            raise ApiError("image_too_large", 413)
    return bytes(body)


async def read_jpeg(request: Request, field: str = "image") -> bytes:
    """本文から JPEG を 1 枚取り出す。契約のエラー (image_*) は ApiError で投げる。"""
    if not request.headers.get("content-type", "").startswith("multipart/form-data"):
        raise ApiError("image_missing", 400)
    body = await _read_body(request)

    async def stream() -> AsyncGenerator[bytes]:
        yield body

    parser = MultiPartParser(request.headers, stream(), max_files=1, max_fields=4)
    # 本文全体が MAX_BODY_BYTES 以下なので、これより大きい閾値なら一時ファイルに退避しない
    parser.spool_max_size = MAX_BODY_BYTES + 1
    try:
        form = await parser.parse()
    except MultiPartException as error:
        raise ApiError("image_missing", 400) from error
    try:
        upload = form.get(field)
        if not isinstance(upload, UploadFile):
            raise ApiError("image_missing", 400)
        data = await upload.read()
    finally:
        await form.close()

    if not data:
        raise ApiError("image_missing", 400)
    if len(data) > MAX_IMAGE_BYTES:
        raise ApiError("image_too_large", 413)
    if not data.startswith(_JPEG_START):
        raise ApiError("image_invalid", 415)
    if not data.rstrip(b"\x00").endswith(_JPEG_END):
        raise ApiError("image_invalid", 400)
    return data
