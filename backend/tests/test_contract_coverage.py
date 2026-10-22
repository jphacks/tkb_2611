"""契約のどの道がまだ無いかの一覧。

`uv run pytest -rs` の skip の理由が、そのまま当日の TODO になる。

道を足すと、その操作の skip が消えて「道がある」ことの確認に変わる。
応答の形は、それぞれの機能のテストで
assert_matches_contract を使って確かめる (ここは道の有無だけを見る)。
"""

import re

import pytest
from fastapi.routing import APIRoute

from penguin_backend.app import create_app
from tests.support.contract import PREFIX, operations


def _routes() -> set[tuple[str, str]]:
    app = create_app()
    return {
        (method, route.path)
        for route in app.routes
        if isinstance(route, APIRoute)
        for method in route.methods or set()
    }


def _normalize(path: str) -> str:
    # 道の引数の名前は契約と実装で違ってよい ({quest_id} と {id})
    return re.sub(r"\{[^/]+\}", "{}", path)


@pytest.mark.parametrize(("method", "path"), operations(), ids=lambda value: str(value))
def test_contract_operation_has_a_route(method: str, path: str) -> None:
    routes = {(m, _normalize(p)) for m, p in _routes()}
    if (method, _normalize(PREFIX + path)) not in routes:
        pytest.skip(f"未実装: {method} {PREFIX}{path}")


def test_no_route_outside_the_contract() -> None:
    """契約に無い通信をコードに書かない (.claude/rules/api-contract.md)。

    /api/healthz だけは外形監視のために残している互換の道。
    """
    allowed = {(m, _normalize(PREFIX + p)) for m, p in operations()}
    allowed |= {("GET", "/api/healthz"), ("HEAD", "/api/healthz")}

    extra = {(m, _normalize(p)) for m, p in _routes()} - allowed

    assert not extra, f"契約に無い道がある (先に api/openapi.yaml に書く): {sorted(extra)}"
