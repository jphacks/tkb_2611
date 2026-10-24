"""POST /api/v1/encounters (契約の reportEncounter)。"""

import json
from datetime import date

from fastapi import Request
from fastapi.concurrency import run_in_threadpool
from fastapi.responses import JSONResponse
from pydantic import ValidationError

from penguin_backend.api import clients_of, settings_of
from penguin_backend.encounters import EncounterStore, accepts_day, pair_id
from penguin_backend.errors import ApiError
from penguin_backend.schemas import EncounterReport, EncounterResult

PATH = "/api/v1/encounters"
# 本文は 3 つの短い項目だけ。これを超えたら読まずに断る。
_MAX_BODY_BYTES = 1024


async def _parse(request: Request) -> tuple[date, EncounterReport]:
    # FastAPI の本文の検証は 422 を返すので使わず、契約の 400 (token_invalid・day_invalid) に写す
    body = await request.body()
    if len(body) > _MAX_BODY_BYTES:
        raise ApiError("token_invalid", 400)
    try:
        report = EncounterReport.model_validate(json.loads(body))
    except (ValueError, ValidationError) as error:
        if isinstance(error, ValidationError) and all(
            problem["loc"][:1] == ("day",) for problem in error.errors()
        ):
            raise ApiError("day_invalid", 400) from None
        raise ApiError("token_invalid", 400) from None
    try:
        day = date.fromisoformat(report.day)
    except ValueError:
        raise ApiError("day_invalid", 400) from None
    if report.self_token == report.peer_token:
        raise ApiError("token_invalid", 400)
    return day, report


async def report_encounter(request: Request) -> JSONResponse:
    day, report = await _parse(request)
    clock = request.app.state.clock
    if not accepts_day(day, clock()):
        raise ApiError("day_invalid", 400)
    settings = settings_of(request)
    # ロールを借りる STS の呼び出しは同期なので、イベントループの外で行う
    clients = await run_in_threadpool(clients_of, request)
    store = EncounterStore(
        clients.dynamodb,
        settings.encounter_table_name,
        clock=clock,
    )
    matched = await run_in_threadpool(store.report, day, report.self_token, report.peer_token)
    result = EncounterResult(matched=matched, pair_id=pair_id(report.self_token, report.peer_token))
    return JSONResponse(result.model_dump(), headers={"Cache-Control": "no-store"})
