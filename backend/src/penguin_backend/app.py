"""FastAPI アプリの入口。

本番は Vercel に Web ビルドと同じ URL で置き、`/api/*` だけがここに届く (docs/web-deploy.md)。
写真判定とみんなの氷山の口は Hack Day に足す。いまは疎通の確認 (`/api/healthz`) だけを持つ。
"""

import logging
from collections.abc import Awaitable, Callable

from fastapi import FastAPI, Request, Response
from fastapi.middleware.cors import CORSMiddleware
from fastapi.responses import JSONResponse

from penguin_backend import __version__
from penguin_backend.settings import Settings

logger = logging.getLogger(__name__)

# 守っている契約の版 (api/openapi.yaml の info.version)。
SCHEMA_VERSION = "1.0.0"


def create_app(settings: Settings | None = None) -> FastAPI:
    settings = settings or Settings()

    # 一般公開する URL なので、API の一覧 (/docs など) は出さない。
    app = FastAPI(
        title="Project Penguin",
        version=__version__,
        docs_url=None,
        redoc_url=None,
        openapi_url=None,
    )

    @app.middleware("http")
    async def answer_unhandled_errors(
        request: Request, call_next: Callable[[Request], Awaitable[Response]]
    ) -> Response:
        # Starlette は処理されなかった例外の 500 を CORS の外側で作るため、そのままでは CORS の
        # ヘッダーが付かず、Web 版からは状態コードも Date も読めなくなる (#87)。CORS の内側で
        # JSON の 500 にして返す。写真の中身を出さないよう、記録するのは例外の型と経路だけにする。
        try:
            return await call_next(request)
        except Exception as error:
            logger.error("unhandled %s on %s", type(error).__name__, request.url.path)
            return JSONResponse({"error": "internal"}, status_code=500)

    if settings.cors_origins:
        # 後から足したミドルウェアほど外側になる。CORS を最も外に置く。
        # Date は CORS の既定では JS から読めないので明示して公開する (ServerTrustedClock が使う)。
        app.add_middleware(
            CORSMiddleware,
            allow_origins=settings.cors_origins,
            allow_methods=["GET", "POST"],
            allow_headers=["Content-Type"],
            expose_headers=["Date"],
        )

    # 外形監視 (docs/web-deploy.md) には HEAD で確かめるものもあるので、HEAD にも答える。
    # /api/v1/health が契約 (api/openapi.yaml) の口。/api/healthz は既存の外形監視のために残す。
    @app.api_route("/api/v1/health", methods=["GET", "HEAD"])
    @app.api_route("/api/healthz", methods=["GET", "HEAD"])
    def healthz(response: Response) -> dict[str, str]:
        # 応答の Date を端末の時計合わせに使うので、CDN に溜めさせない。
        response.headers["Cache-Control"] = "no-store"
        return {"status": "ok", "version": __version__, "schema_version": SCHEMA_VERSION}

    return app


app = create_app()
