import json
from pathlib import Path

import pytest
from fastapi.testclient import TestClient

from penguin_backend import __version__
from penguin_backend.app import SCHEMA_VERSION, create_app
from penguin_backend.settings import Settings

LOCAL_ORIGIN = "http://localhost:8090"
CONTRACT = Path(__file__).resolve().parents[2] / "api"


def client(origins: list[str] | None = None) -> TestClient:
    app = create_app(Settings(cors_origins=origins or []))
    return TestClient(app, raise_server_exceptions=False)


@pytest.mark.parametrize("path", ["/api/v1/health", "/api/healthz"])
def test_health_answers_ok_without_caching(path: str) -> None:
    response = client().get(path)

    assert response.status_code == 200
    assert response.json() == {
        "status": "ok",
        "version": __version__,
        "schema_version": SCHEMA_VERSION,
    }
    assert response.headers["cache-control"] == "no-store"


def test_health_matches_the_contract_example() -> None:
    example = json.loads((CONTRACT / "examples" / "health.200.ok.json").read_text(encoding="utf-8"))
    body = client().get("/api/v1/health").json()

    assert body.keys() == example.keys()
    assert body["schema_version"] == example["schema_version"]


def test_healthz_answers_head_for_uptime_monitors() -> None:
    response = client().head("/api/healthz")

    assert response.status_code == 200
    assert response.content == b""


def test_api_listing_is_not_published() -> None:
    for path in ("/docs", "/redoc", "/openapi.json"):
        assert client().get(path).status_code == 404


def test_cors_is_closed_by_default() -> None:
    response = client().get("/api/healthz", headers={"Origin": LOCAL_ORIGIN})

    assert "access-control-allow-origin" not in response.headers


def test_allowed_origin_can_read_the_date_header() -> None:
    response = client([LOCAL_ORIGIN]).get("/api/healthz", headers={"Origin": LOCAL_ORIGIN})

    assert response.headers["access-control-allow-origin"] == LOCAL_ORIGIN
    assert response.headers["access-control-expose-headers"] == "Date"


def test_other_origins_are_not_allowed() -> None:
    response = client([LOCAL_ORIGIN]).get("/api/healthz", headers={"Origin": "https://example.com"})

    assert "access-control-allow-origin" not in response.headers


def test_unhandled_error_keeps_cors_headers_and_hides_details() -> None:
    app = create_app(Settings(cors_origins=[LOCAL_ORIGIN]))

    @app.get("/api/boom")
    def boom() -> None:
        raise RuntimeError("写真の中身が入った例外")

    response = TestClient(app, raise_server_exceptions=False).get(
        "/api/boom", headers={"Origin": LOCAL_ORIGIN}
    )

    assert response.status_code == 500
    assert response.json() == {"error": "internal"}
    assert response.headers["access-control-allow-origin"] == LOCAL_ORIGIN
    assert "写真" not in response.text


def test_origins_are_read_from_a_comma_separated_variable(
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    monkeypatch.setenv("PENGUIN_CORS_ORIGINS", f"{LOCAL_ORIGIN}, http://127.0.0.1:8080 ,")

    assert Settings().cors_origins == [LOCAL_ORIGIN, "http://127.0.0.1:8080"]
