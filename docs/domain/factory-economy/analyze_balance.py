"""設計用の静的な生産フロー計算。ゲーム実装ではない。

実行: python -X utf8 docs/domain/factory-economy/analyze_balance.py
任意のLP計算: 同コマンドに --optimize を付ける（SciPyが必要）。
原料・エネルギーの保存則と施設の処理能力を使う上限計算。整数バッチも比較する。
加工の順序待ち・配送・建築費・解放・日常の換算は含めない。
"""

from __future__ import annotations

import json
import sys
from fractions import Fraction as F
from functools import cache
from pathlib import Path


ROOT = Path(__file__).resolve().parent
MODEL = json.loads((ROOT / "balance-model.json").read_text(encoding="utf-8"))
RESOURCES = MODEL["resources"]
RECIPES = MODEL["recipes"]
BY_OUTPUT = {r["output"]: r for r in RECIPES}


@cache
def cumulative(item: str) -> tuple[tuple[F, ...], tuple[F, ...]]:
    """品物1個を作る累積エネルギーと、各施設の必要加工秒数。"""
    recipe = BY_OUTPUT[item]
    energy = [F(v) for v in recipe["energy"]]
    seconds = [F(0) for _ in RECIPES]
    seconds[RECIPES.index(recipe)] = F(recipe["seconds"])
    for source, amount in recipe["inputs"].items():
        source_energy, source_seconds = cumulative(source)
        energy = [a + amount * b for a, b in zip(energy, source_energy)]
        seconds = [a + amount * b for a, b in zip(seconds, source_seconds)]
    return (
        tuple(v / recipe["yield"] for v in energy),
        tuple(v / recipe["yield"] for v in seconds),
    )


def validate() -> None:
    if len(BY_OUTPUT) != len(RECIPES):
        raise ValueError("このモデルは品物ごとに一つのレシピを前提とする")
    for recipe in RECIPES:
        item = recipe["output"]
        tier = 1 + max(
            (RESOURCES[k]["tier"] for k in recipe["inputs"]), default=0
        )
        if RESOURCES[item]["tier"] != tier or recipe["facility_tier"] != tier:
            raise ValueError(f"次数と依存関係が一致しない: {item}")
        if recipe["seconds"] <= 0 or recipe["yield"] <= 0:
            raise ValueError(f"加工時間・生産数は正であること: {item}")
        if any(v < 0 for v in recipe["energy"]) or not any(recipe["energy"]):
            raise ValueError(f"生産レシピはエネルギーを消費すること: {item}")
        if any(v <= 0 for v in recipe["inputs"].values()):
            raise ValueError(f"投入数は正であること: {item}")
    # 3経路のどれかが、全エネルギーの点数効率で他を支配していないか。
    targets = ["gift", "research", "oden"]
    for item in targets:
        own = [v / RESOURCES[item]["points"] for v in cumulative(item)[0]]
        for other in targets:
            if other == item:
                continue
            rival = [v / RESOURCES[other]["points"] for v in cumulative(other)[0]]
            if all(a <= b for a, b in zip(rival, own)) and any(
                a < b for a, b in zip(rival, own)
            ):
                raise ValueError(f"エネルギー効率で支配される経路: {item}")


def pure_max(item: str, budget: list[int]) -> float:
    energy, seconds = cumulative(item)
    limits = [F(b) / e for b, e in zip(budget, energy) if e]
    limits += [F(MODEL["window_seconds"]) / t for t in seconds if t]
    return float(min(limits))


def optimize(budget: list[int], target: str | None = None, integer: bool = False) -> dict:
    from scipy.optimize import linprog

    items = list(RESOURCES)
    count = len(RECIPES)
    objective = [0.0] * count + [-RESOURCES[k]["points"] for k in items]
    constraints, rhs = [], []
    for item in items:
        # 消費＋納品−生産 <= 0。同じ中間品を消費と納品に重複計上しない。
        row = []
        for recipe in RECIPES:
            row.append(
                recipe["inputs"].get(item, 0)
                - (recipe["yield"] if recipe["output"] == item else 0)
            )
        row += [int(k == item) for k in items]
        constraints.append(row)
        rhs.append(0)
    for index, available in enumerate(budget):
        constraints.append([r["energy"][index] for r in RECIPES] + [0] * len(items))
        rhs.append(available)
    bounds = [(0, MODEL["window_seconds"] / r["seconds"]) for r in RECIPES]
    bounds += [(0, None if target is None or k == target else 0) for k in items]
    if integer:
        from scipy.optimize import Bounds, LinearConstraint, milp

        result = milp(
            objective,
            integrality=[1] * len(objective),
            bounds=Bounds([0] * len(objective),
                          [float("inf") if upper is None else upper for _, upper in bounds]),
            constraints=LinearConstraint(constraints, -float("inf"), rhs),
            options={"time_limit": 10},
        )
    else:
        result = linprog(objective, A_ub=constraints, b_ub=rhs, bounds=bounds, method="highs")
    if not result.success:
        raise RuntimeError(result.message)
    if any(sum(a * b for a, b in zip(row, result.x)) > limit + 1e-7
           for row, limit in zip(constraints, rhs)):
        raise ArithmeticError("保存則または能力制約を満たさない")
    return {
        "points": max(0.0, round(-result.fun, 6)),
        "deliveries": {k: round(result.x[count + i], 6)
                       for i, k in enumerate(items) if result.x[count + i] > 1e-7},
        "energy_used": [round(sum(result.x[i] * r["energy"][j]
                                  for i, r in enumerate(RECIPES)), 6) for j in range(3)],
    }


def main() -> None:
    validate()
    result = {"assumptions": {
        "window_seconds": MODEL["window_seconds"], "one_building_per_recipe": True,
        "continuous_steady_state_upper_bound": True, "includes_startup_latency": False,
        "includes_delivery_limits": False, "includes_unlock_and_build_cost": False,
        "includes_daily_conversion": False,
    }, "items": {}}
    for item, info in RESOURCES.items():
        energy, seconds = cumulative(item)
        result["items"][item] = {
            "tier": info["tier"], "points": info["points"],
            "energy_per_unit": [float(v) for v in energy],
            "energy_fraction": [str(v) for v in energy],
            "machine_seconds_per_unit": float(sum(seconds)),
            "points_per_total_energy": float(F(info["points"]) / sum(energy)),
        }
    # 比例按分ではなく整数バッチで作れる4個分の例も検証する。
    for item in ("oden", "gift", "research"):
        _, times = cumulative(item)
        batches = [4 * t / r["seconds"] for t, r in zip(times, RECIPES)]
        if not all(v.denominator == 1 for v in batches):
            raise ArithmeticError(f"4個分の例に端数バッチがある: {item}")
    if "--optimize" in sys.argv:
        result["profiles"] = {}
        for name, budget in MODEL["profiles"].items():
            mixed = optimize(budget)
            integer = optimize(budget, integer=True)
            singles = {k: round(pure_max(k, budget) * v["points"], 6)
                       for k, v in RESOURCES.items()}
            if mixed["points"] + 1e-5 < max(singles.values()):
                raise ArithmeticError("混合生産の上限が単品生産より低い")
            if mixed["points"] + 1e-5 < integer["points"]:
                raise ArithmeticError("整数バッチが連続量の上限を超えている")
            result["profiles"][name] = {"budget": budget, "mixed": mixed,
                                       "integer_batches": integer,
                                       "single_product_points": singles}
        if abs(result["profiles"]["empty"]["mixed"]["points"]) > 1e-7:
            raise ArithmeticError("補給ゼロから品物が増えている")
    print(json.dumps(result, ensure_ascii=False, indent=2))


if __name__ == "__main__":
    main()
