"""写真の中身が漏れていないかを機械的に確かめる (.claude/rules/privacy.md)。

- assert_no_leak: 写真に埋めた目印 (support/photos.py の jpeg(marker)) が、
  応答とログに出ていないこと
- block_disk_writes: 処理の間にファイルを書いたり一時ファイルを作ったりしたら失敗させる

FastAPI の UploadFile は 1 MB を超えると SpooledTemporaryFile がディスクに退避する。
block_disk_writes はその退避 (tempfile.TemporaryFile) も捕まえるので、大きな写真のテストで使う。
止めたときの例外はフレームワークに飲み込まれることがある (退避なら 400 になる) ので、
書こうとした記録 (attempts) も残し、no_disk_writes フィクスチャは最後にそれが空かを確かめる。
"""

from __future__ import annotations

import builtins
import os
import sys
import tempfile
from collections.abc import Generator
from contextlib import contextmanager
from typing import Any

import httpx2 as httpx
import pytest

_WRITE_MODES = set("wax+")
_WRITE_FLAGS = os.O_WRONLY | os.O_RDWR | os.O_CREAT | os.O_APPEND | os.O_TRUNC


class DiskWriteError(AssertionError):
    """写真の処理中にディスクへ書こうとした。"""


def assert_no_leak(
    marker: str, response: httpx.Response, caplog: pytest.LogCaptureFixture | None = None
) -> None:
    assert marker not in response.text, "写真に埋めた目印が応答に出ている"
    for name, value in response.headers.items():
        assert marker not in value, f"写真に埋めた目印がヘッダー {name} に出ている"
    if caplog is not None:
        assert marker not in caplog.text, "写真に埋めた目印がログに出ている"


@contextmanager
def block_disk_writes(monkeypatch: pytest.MonkeyPatch) -> Generator[list[str]]:
    """この中でファイルを書く・一時ファイルを作ると DiskWriteError になり、attempts に記録される。

    読むのはよい。
    """
    attempts: list[str] = []

    def refused(what: str) -> DiskWriteError:
        attempts.append(what)
        return DiskWriteError(what)

    real_open = builtins.open
    real_os_open = os.open

    def guarded_open(file: Any, mode: str = "r", *args: Any, **kwargs: Any) -> Any:
        if _WRITE_MODES & set(mode):
            raise refused(f"ディスクへの書き込み: open({file!r}, {mode!r})")
        return real_open(file, mode, *args, **kwargs)

    def guarded_os_open(path: Any, flags: int, *args: Any, **kwargs: Any) -> int:
        if flags & _WRITE_FLAGS:
            raise refused(f"ディスクへの書き込み: os.open({path!r})")
        return real_os_open(path, flags, *args, **kwargs)

    def refuse(name: str) -> Any:
        def _refuse(*_args: Any, **_kwargs: Any) -> Any:
            raise refused(f"一時ファイルの作成: tempfile.{name}")

        return _refuse

    with monkeypatch.context() as patch:
        # import のたびに .pyc を書きに行くのを止める (検査の対象はアプリの処理だけ)
        patch.setattr(sys, "dont_write_bytecode", True)
        patch.setattr(builtins, "open", guarded_open)
        patch.setattr(os, "open", guarded_os_open)
        for name in ("TemporaryFile", "NamedTemporaryFile", "mkstemp", "mkdtemp"):
            patch.setattr(tempfile, name, refuse(name))
        yield attempts
