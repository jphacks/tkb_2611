"""ルート。契約 (api/openapi.yaml) のパスごとに 1 ファイル。"""

from fastapi import Request

from penguin_backend.aws import Clients
from penguin_backend.settings import Settings


def settings_of(request: Request) -> Settings:
    settings: Settings = request.app.state.settings
    return settings


def clients_of(request: Request) -> Clients:
    clients: Clients = request.app.state.clients(request)
    return clients
