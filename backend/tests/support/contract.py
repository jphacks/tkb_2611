"""応答が契約 (api/openapi.yaml) どおりかを 1 行で確かめる。

SDD の「仕様」はこの契約と api/examples/。テストは、先に契約に例を足し、その形で答えることを
assert_matches_contract で確かめてから実装する。

契約の読み込み・スキーマの検証・エラーの code の表は scripts/check_api_contract.py のものを使い、
ここで二重に持たない (表を変えるときは scripts 側だけを直せばよい)。
"""

from __future__ import annotations

import importlib.util
import json
import re
from functools import cache
from pathlib import Path
from types import ModuleType
from typing import Any

import httpx2 as httpx

REPO = Path(__file__).resolve().parents[3]
API = REPO / "api"
# 契約の servers.url。Vercel では同じ URL に Web ビルドが載るので、backend の道はすべて /api の下。
PREFIX = "/api"


@cache
def _checker() -> ModuleType:
    path = REPO / "scripts" / "check_api_contract.py"
    spec = importlib.util.spec_from_file_location("check_api_contract", path)
    assert spec is not None and spec.loader is not None
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


@cache
def spec() -> dict[str, Any]:
    loaded: dict[str, Any] = _checker().load_spec(API)
    return loaded


def error_table() -> dict[str, tuple[set[int], bool]]:
    """エラーの code → (許す HTTP 状態, retryable)。"""
    table: dict[str, tuple[set[int], bool]] = _checker().ERROR_TABLE
    return table


def example(name: str) -> Any:
    """api/examples/ の例を読む (例: "judge.200.accept")。"""
    path = API / "examples" / f"{name}.json"
    return json.loads(path.read_text(encoding="utf-8"))


def operations() -> list[tuple[str, str]]:
    """契約にあるすべての (HTTP メソッド, 契約のパス)。パスは /v1/... (PREFIX を付ける前)。"""
    methods = {"get", "post", "put", "patch", "delete", "head"}
    return [
        (method.upper(), path)
        for path, item in spec()["paths"].items()
        for method in item
        if method in methods
    ]


def validate(schema_name: str, body: Any) -> list[str]:
    """本文を components/schemas/<schema_name> で検証し、合わない点を返す (合えば空)。"""
    validator = _checker().validator_for(spec(), schema_name)
    return [
        f"{'/'.join(map(str, error.absolute_path)) or '(本文)'}: {error.message}"
        for error in validator.iter_errors(body)
    ]


def _resolve(node: dict[str, Any]) -> dict[str, Any]:
    ref = node.get("$ref")
    if not isinstance(ref, str):
        return node
    target: Any = spec()
    for part in ref.removeprefix("#/").split("/"):
        target = target[part]
    resolved: dict[str, Any] = target
    return _resolve(resolved)


def _schema_name(media: dict[str, Any]) -> str | None:
    ref = media.get("schema", {}).get("$ref")
    if isinstance(ref, str) and ref.startswith("#/components/schemas/"):
        return ref.rsplit("/", 1)[1]
    return None


def _cache_control(definition: dict[str, Any]) -> str:
    """契約がその応答に決めた Cache-Control の値。決めていなければ既定の no-store。"""
    header = definition.get("headers", {}).get("Cache-Control")
    if header is None:
        return "no-store"
    value: str = _resolve(header)["schema"]["const"]
    return value


def _operation(method: str, path: str) -> dict[str, Any]:
    template = path.removeprefix(PREFIX)
    for contract_path, item in spec()["paths"].items():
        pattern = "^" + re.sub(r"\{[^/]+\}", "[^/]+", contract_path) + "$"
        if re.match(pattern, template) and method.lower() in item:
            operation: dict[str, Any] = item[method.lower()]
            return operation
    raise AssertionError(f"契約に無い道: {method} {path} (先に api/openapi.yaml に書く)")


def assert_matches_contract(response: httpx.Response, method: str | None = None) -> None:
    """応答が契約のその道の、その状態の定義どおりかを確かめる。

    見るもの: 状態が契約に載っているか・本文のスキーマ・Content-Type・Cache-Control
    (成功の応答。契約がその道に決めた値で、ふつうは no-store)・契約が求めるヘッダー
    (Retry-After など)。
    """
    request = response.request
    method = method or request.method
    operation = _operation(method, request.url.path)
    status = str(response.status_code)
    declared = operation.get("responses", {})
    assert status in declared, (
        f"{method} {request.url.path} の契約に状態 {status} が無い (ある: {sorted(declared)})"
    )
    definition = _resolve(declared[status])

    if response.status_code < 400:
        expected = _cache_control(definition)
        assert response.headers.get("cache-control") == expected, (
            f"この道の成功の応答には Cache-Control: {expected} を付ける (.claude/rules/backend.md)"
        )
    for header in definition.get("headers", {}):
        if header.lower() in ("date", "cache-control"):
            continue  # Date はサーバー (uvicorn・Vercel) が付ける。Cache-Control は上で見た
        assert header.lower() in response.headers, f"契約が求めるヘッダー {header} が無い"

    content = definition.get("content")
    if method == "HEAD" or not content:
        return
    media = content.get("application/json")
    assert media is not None, "契約の応答が JSON ではない"
    assert response.headers.get("content-type", "").startswith("application/json")
    name = _schema_name(media)
    if name is not None:
        problems = validate(name, response.json())
        assert not problems, f"{name} のスキーマに合わない: {problems}"


def assert_error(response: httpx.Response, code: str) -> None:
    """契約のエラーの形 {"error": {code, message, retryable}} で、code の表どおりに答えたか。"""
    table = error_table()
    assert code in table, f"契約に無いエラーの code: {code}"
    statuses, retryable = table[code]
    assert response.status_code in statuses, (
        f"{code} は {sorted(statuses)} で返す (返したのは {response.status_code})"
    )
    problems = validate("ErrorResponse", response.json())
    assert not problems, f"ErrorResponse のスキーマに合わない: {problems}"
    body = response.json()["error"]
    assert body["code"] == code
    assert body["retryable"] is retryable
    if response.status_code in (429, 503):
        assert "retry-after" in response.headers, (
            f"{response.status_code} には Retry-After を付ける"
        )
    assert_matches_contract(response)
