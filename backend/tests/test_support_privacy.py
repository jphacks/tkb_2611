"""写真の漏れの検査 (tests/support/privacy.py) が、漏れを捕まえること。"""

import logging
import os
import tempfile
from pathlib import Path

import pytest
from fastapi import FastAPI, UploadFile
from fastapi.testclient import TestClient

from tests.support import photos
from tests.support.privacy import DiskWriteError, assert_no_leak, block_disk_writes


def test_writing_a_file_is_caught(tmp_path: Path, monkeypatch: pytest.MonkeyPatch) -> None:
    with block_disk_writes(monkeypatch) as attempts, pytest.raises(DiskWriteError):
        open(tmp_path / "photo.jpg", "wb")  # noqa: SIM115 (書き込みを捕まえるか見る)

    assert len(attempts) == 1


def test_low_level_writes_are_caught(tmp_path: Path, monkeypatch: pytest.MonkeyPatch) -> None:
    with block_disk_writes(monkeypatch) as attempts, pytest.raises(DiskWriteError):
        os.open(tmp_path / "photo.jpg", os.O_WRONLY | os.O_CREAT)

    assert len(attempts) == 1


def test_temporary_files_are_caught(monkeypatch: pytest.MonkeyPatch) -> None:
    with block_disk_writes(monkeypatch) as attempts, pytest.raises(DiskWriteError):
        tempfile.NamedTemporaryFile()  # noqa: SIM115

    assert attempts == ["一時ファイルの作成: tempfile.NamedTemporaryFile"]


@pytest.mark.usefixtures("no_disk_writes")
def test_reading_is_still_allowed() -> None:
    assert Path(__file__).read_text(encoding="utf-8")


def test_large_upload_spooling_to_disk_is_caught(monkeypatch: pytest.MonkeyPatch) -> None:
    """UploadFile は 1 MB を超えるとディスクに退避する。block_disk_writes がそれを捕まえる。

    当日の判定の道は、写真をこの退避に頼らずメモリで読む (.claude/rules/privacy.md)。
    """
    app = FastAPI()

    @app.post("/upload")
    async def upload(image: UploadFile) -> dict[str, int]:
        return {"size": len(await image.read())}

    big = photos.jpeg() + b"\x00" * (2 * 1024 * 1024)
    with block_disk_writes(monkeypatch) as attempts:
        response = TestClient(app).post("/upload", files=photos.multipart(big))

    # 止めた例外は FastAPI が 400 に変える。記録には退避しようとしたことが残る
    assert response.status_code == 400
    assert any("tempfile.TemporaryFile" in attempt for attempt in attempts)


def test_small_upload_stays_in_memory(monkeypatch: pytest.MonkeyPatch) -> None:
    app = FastAPI()

    @app.post("/upload")
    async def upload(image: UploadFile) -> dict[str, int]:
        return {"size": len(await image.read())}

    with block_disk_writes(monkeypatch) as attempts:
        response = TestClient(app).post("/upload", files=photos.multipart(photos.jpeg()))

    assert response.json() == {"size": len(photos.jpeg())}
    assert attempts == []


def test_leaking_the_marker_in_the_response_is_caught() -> None:
    app = FastAPI()

    @app.get("/echo")
    def echo() -> dict[str, str]:
        return {"comment": "写真には leak-123 と書いてあった"}

    response = TestClient(app).get("/echo")
    with pytest.raises(AssertionError, match="応答"):
        assert_no_leak("leak-123", response)


def test_leaking_the_marker_in_the_logs_is_caught(caplog: pytest.LogCaptureFixture) -> None:
    app = FastAPI()

    @app.get("/quiet")
    def quiet() -> dict[str, str]:
        logging.getLogger("penguin_backend").warning("AI の答え: leak-456")
        return {"ok": "yes"}

    with caplog.at_level(logging.INFO):
        response = TestClient(app).get("/quiet")
    with pytest.raises(AssertionError, match="ログ"):
        assert_no_leak("leak-456", response, caplog)
