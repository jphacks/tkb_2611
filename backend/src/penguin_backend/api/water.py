"""POST /api/v1/water/estimate (契約の estimateWater)。"""

from fastapi import Request
from fastapi.concurrency import run_in_threadpool
from fastapi.responses import JSONResponse

from penguin_backend.api import clients_of, settings_of
from penguin_backend.uploads import read_jpeg
from penguin_backend.water.decide import estimate
from penguin_backend.water.model import NovaWaterModel

PATH = "/api/v1/water/estimate"


async def estimate_water(request: Request) -> JSONResponse:
    # 本文は FastAPI の UploadFile ではなく read_jpeg で読む (一時ファイルに退避させない)
    image = await read_jpeg(request)
    # ロールを借りる STS の呼び出しは同期なので、イベントループの外で行う
    clients = await run_in_threadpool(clients_of, request)
    model = NovaWaterModel(clients.bedrock, settings_of(request).nova_model_id)
    reading = await run_in_threadpool(model.read, image)
    result = estimate(reading)
    return JSONResponse(result.model_dump(exclude_none=True), headers={"Cache-Control": "no-store"})
