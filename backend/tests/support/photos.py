"""テストに使う合成の写真。

実在の人や部屋が写った写真はリポジトリに置かない (.claude/rules/privacy.md)。ここにあるのは、
コードで描いた 64x64 のペンギンのような図形の JPEG だけ。
Pillow で一度描いてバイト列を埋め込んだもので、Pillow は依存に足していない。

marker を渡すと、JPEG のコメント (COM) にその文字列を埋める。応答やログにその文字列が
出ていないかで、写真の中身の漏れを確かめる (support/privacy.py の assert_no_leak)。
"""

from __future__ import annotations

import base64
import struct

# 長い辺 1280px・4 MB までの JPEG (api/openapi.yaml の judgeEcoPhoto)。
MAX_BYTES = 4 * 1024 * 1024

_PENGUIN_JPEG = base64.b64decode(
    "/9j/4AAQSkZJRgABAQAAAQABAAD/2wBDAAoHBwgHBgoICAgLCgoLDhgQDg0NDh0VFhEYIx8lJCIfIiEmKzcvJik0KSEiMEExNDk7Pj4+"
    "JS5ESUM8SDc9Pjv/2wBDAQoLCw4NDhwQEBw7KCIoOzs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7"
    "Ozv/wAARCABAAEADASIAAhEBAxEB/8QAHwAAAQUBAQEBAQEAAAAAAAAAAAECAwQFBgcICQoL/8QAtRAAAgEDAwIEAwUFBAQAAAF9AQID"
    "AAQRBRIhMUEGE1FhByJxFDKBkaEII0KxwRVS0fAkM2JyggkKFhcYGRolJicoKSo0NTY3ODk6Q0RFRkdISUpTVFVWV1hZWmNkZWZnaGlq"
    "c3R1dnd4eXqDhIWGh4iJipKTlJWWl5iZmqKjpKWmp6ipqrKztLW2t7i5usLDxMXGx8jJytLT1NXW19jZ2uHi4+Tl5ufo6erx8vP09fb3"
    "+Pn6/8QAHwEAAwEBAQEBAQEBAQAAAAAAAAECAwQFBgcICQoL/8QAtREAAgECBAQDBAcFBAQAAQJ3AAECAxEEBSExBhJBUQdhcRMiMoEI"
    "FEKRobHBCSMzUvAVYnLRChYkNOEl8RcYGRomJygpKjU2Nzg5OkNERUZHSElKU1RVVldYWVpjZGVmZ2hpanN0dXZ3eHl6goOEhYaHiImK"
    "kpOUlZaXmJmaoqOkpaanqKmqsrO0tba3uLm6wsPExcbHyMnK0tPU1dbX2Nna4uPk5ebn6Onq8vP09fb3+Pn6/9oADAMBAAIRAxEAPwD0"
    "miiiuo5Aoori9a+JuladL5NhE2oyK2GZW2RjqDhsHJ4HQYIPWgLXO0orhNL+Kum3U5j1GzksVP3ZFbzV6HOcAEdsYB69q7mOSOaJJYnW"
    "SN1DK6nIYHoQe4oBqw6iiigAooooA87+JvieW326FZTbDIm67KkZ2nomc8ZGSRjkFexIrzOrutX/APamt3t8GkZZ5mdPMOWC5+UHr0GB"
    "j2r2zwR4IsvDenQ3FxbrJqkih5ZZFBaEkYKIecAZIJB557YAmUrG0Yng1d18NvE8tpqKaJdzZtLjPkbyP3cnXAJPAbnjn5iMdTn07xP4"
    "S0zxRYvFdRLHdBQIrtUHmRkZwM915OV6c9jgj5+miutM1B4X3QXVrKVba3KOpxwR3BHUURlcJRPomioLK7jv7C3vIgyx3ESyqGHIDAEZ"
    "9+anqjEKKKKAPEPANnHfeONKhlZgqzGUFTzlFLj8MqM+1fQleLOD4U+LkN3dFTBcXJlEjuEUJNuUsTzgKWbrjO3tmvaaxqbnRDYK8Q+L"
    "VnHa+NDMjMWu7aOVwx4BGU49sIPxzXt9eMeOQfFXxMj0q0KjylS1MquGHGXdsccruYFc5yvrxShuOWx6B4b/AORY0r/ryh/9AFaVFFbn"
    "MFFFFAHP+L/C0XifTlQSeVd2+TbyEnbk4yrD0OBz1GPqCzwx43jt4ItG8UltN1O3UIJLkkJcKARv3njPynJJwTyCc4HR1Bd2VpfxCK8t"
    "YbmMNuCTRhwD64Pfk1LSZUZNGdr/AI/06xgNvorprGqSr+4gtcyr3+ZivYYJIBz06A5GZ4L8JSaIJdT1KVptVuwfNJfdsBOSCf4mJAJP"
    "r09T0Vnpthp+/wCxWVva+Zjf5MSpux0zgc9T+dWaIxSHKbYUUUVRB//Z"
)

_SOI = b"\xff\xd8"
_EOI = b"\xff\xd9"


def jpeg(marker: str | None = None) -> bytes:
    """合成の JPEG。marker を渡すと、その文字列をコメントとして埋める (漏れの検査用)。"""
    if marker is None:
        return _PENGUIN_JPEG
    text = marker.encode("utf-8")
    comment = b"\xff\xfe" + struct.pack(">H", len(text) + 2) + text
    return _SOI + comment + _PENGUIN_JPEG[len(_SOI) :]


def too_large() -> bytes:
    """契約の上限 (4 MB) を 1 バイト超える JPEG の形のバイト列 (413 image_too_large の確認用)。"""
    body = jpeg()
    padding = b"\x00" * (MAX_BYTES + 1 - len(body))
    return body[: -len(_EOI)] + padding + _EOI


def png() -> bytes:
    """JPEG ではない画像 (415 / 400 image_invalid の確認用)。PNG の先頭の印だけを持つ。"""
    return b"\x89PNG\r\n\x1a\n" + b"\x00" * 32


def broken_jpeg() -> bytes:
    """先頭だけ JPEG で、途中で切れているもの (400 image_invalid の確認用)。"""
    return _PENGUIN_JPEG[:64]


def multipart(
    data: bytes, *, filename: str = "photo.jpg", content_type: str = "image/jpeg"
) -> dict[str, tuple[str, bytes, str]]:
    """TestClient の files= に渡す形。契約のフォームの項目名は image。"""
    return {"image": (filename, data, content_type)}
