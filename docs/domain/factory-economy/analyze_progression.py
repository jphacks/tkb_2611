"""桁が増えるレシピ・所持効果・遭遇確率の設計用計算。ゲーム実装ではない。

python -X utf8 docs/domain/factory-economy/analyze_progression.py
標準ライブラリのみ。配置・配送・解放の過程・オフライン処理は含まない。
"""

from __future__ import annotations

import heapq
import json
import math
import sys
from collections import defaultdict
from fractions import Fraction as F
from functools import cache
from pathlib import Path

ROOT = Path(__file__).resolve().parent
MODEL = json.loads((ROOT / "progression-model.json").read_text(encoding="utf-8"))
RECIPES = MODEL["recipes"]
RESOURCES = MODEL["resources"]
PENGUINS = MODEL["penguins"]
ENERGIES = MODEL["energy_order"]
BY_OUTPUT = {r["output"]: r for r in RECIPES}


def boost(resource: str, owned: dict[str, int]) -> F:
    additive, multiplier = F(0), F(1)
    for p in PENGUINS:
        count = owned.get(p["id"], 0)
        if count < 0 or not isinstance(count, int):
            raise ValueError("所持数は0以上の整数")
        if p["target"] != resource:
            continue
        amount = F(str(p["coefficient"])) * count
        if p["mode"] == "add":
            additive += amount
        else:
            multiplier *= 1 + amount
    return (1 + additive) * multiplier


def output(recipe: dict, level: int, owned: dict[str, int]) -> F:
    return F(recipe["yield"] * MODEL["output_growth"][str(level)]) * boost(recipe["output"], owned)


def cold_batch(target: str, level: int, owned: dict[str, int], limited: bool = True) -> dict:
    """在庫0から最初の1バッチ。必要数を逆算し、各施設1棟で実行可能な順に加工。"""
    needs = defaultdict(F, {target: F(1)})
    jobs = {}
    for recipe in sorted(RECIPES, key=lambda r: r["tier"], reverse=True):
        count = math.ceil(needs[recipe["output"]] / output(recipe, level, owned))
        jobs[recipe["id"]] = count
        for item, amount in recipe["inputs"].items():
            needs[item] += amount * count
    remaining = jobs.copy()
    stock = defaultdict(F)
    consumed = defaultdict(F)
    produced = defaultdict(F)
    used = [F(0)] * 3
    budget = [F(n) * boost(k, owned) for n, k in zip(MODEL["base_supply_pack"], ENERGIES)]
    running = set()
    events = []
    clock = 0
    while any(remaining.values()) or events:
        for index, recipe in enumerate(RECIPES):
            if not remaining[recipe["id"]] or recipe["id"] in running:
                continue
            if any(stock[k] < v for k, v in recipe["inputs"].items()):
                continue
            if limited and any(a + b > cap for a, b, cap in zip(used, recipe["energy"], budget)):
                continue
            for k, v in recipe["inputs"].items():
                stock[k] -= v
                consumed[k] += v
            used = [a + b for a, b in zip(used, recipe["energy"])]
            remaining[recipe["id"]] -= 1
            running.add(recipe["id"])
            heapq.heappush(events, (clock + recipe["seconds"], index))
        if not events:
            return {"completed": False, "reason": "supply_exhausted", "energy_used": list(map(float, used))}
        clock = events[0][0]
        while events and events[0][0] == clock:
            _, index = heapq.heappop(events)
            recipe = RECIPES[index]
            amount = output(recipe, level, owned)
            stock[recipe["output"]] += amount
            produced[recipe["output"]] += amount
            running.remove(recipe["id"])
    for item in RESOURCES:
        if produced[item] - consumed[item] != stock[item] or stock[item] < 0:
            raise ArithmeticError("品物の保存則に違反")
    return {
        "completed": True, "seconds": clock,
        "output": float(stock[target]), "energy_used": list(map(float, used)),
        "jobs": {k: v for k, v in jobs.items() if v},
        "leftovers": {k: float(v) for k, v in stock.items() if v and k != target},
    }


def unit_costs(level: int, owned: dict[str, int]) -> dict:
    @cache
    def cost(item):
        recipe = BY_OUTPUT[item]
        energy = list(map(F, recipe["energy"]))
        for source, amount in recipe["inputs"].items():
            energy = [a + amount * b for a, b in zip(energy, cost(source))]
        return tuple(v / output(recipe, level, owned) for v in energy)
    return {k: {"energy": list(map(float, cost(k))),
                "points_per_total_energy": float(F(v["points"]) / sum(cost(k)))}
            for k, v in RESOURCES.items()}


def rare_target_cdf() -> dict:
    """S4解放直後、他17種を所持、R4の2種だけ未所持という条件での厳密DP。"""
    target = "erect_crested"
    new_ids = ["eastern_rockhopper", target]
    weights = {p["id"]: F(p["weight"], 10000) for p in PENGUINS}
    states = {(3, 0): F(1)}
    hit = F(0)
    cdf = []
    limit = MODEL["encounter"]["new_species_pity_after_misses"]
    for draw in range(1, 2 * (limit + 1) + 1):
        next_states = defaultdict(F)
        for (mask, misses), mass in states.items():
            unseen = [key for bit, key in enumerate(new_ids) if mask & (1 << bit)]
            if misses >= limit:
                total = sum(weights[k] for k in unseen)
                outcomes = [(key, weights[key] / total) for key in unseen]
            else:
                outcomes = list(weights.items())
            for key, probability in outcomes:
                flow = mass * probability
                if key == target:
                    hit += flow
                elif key in unseen:
                    next_mask = mask & ~(1 << new_ids.index(key))
                    next_states[(next_mask, 0)] += flow
                else:
                    next_states[(mask, misses + 1)] += flow
        states = next_states
        if hit + sum(states.values()) != 1:
            raise ArithmeticError("確率の総和が1でない")
        cdf.append({"encounters": draw, "probability": float(hit)})
    if hit != 1:
        raise ArithmeticError("新種保証が有限回で働かない")
    expected = 1 + sum(1 - row["probability"] for row in cdf[:-1])
    return {"assumption": "only_two_S4_species_unowned", "expected_encounters": expected,
            "median": next(r["encounters"] for r in cdf if r["probability"] >= .5),
            "p95": next(r["encounters"] for r in cdf if r["probability"] >= .95),
            "maximum": 2 * (limit + 1), "cdf": cdf}


def optimize_snapshot(stage: int, owned: dict[str, int], supply: list[int]) -> dict:
    """600秒の集計上限。時系列を含むcold_batchとは別の計算。"""
    from scipy.optimize import linprog

    level = MODEL["stage_level"][str(stage)]
    recipes = [r for r in RECIPES if r["tier"] <= stage]
    items = [k for k, v in RESOURCES.items() if v["tier"] <= stage]
    rows, limits = [], []
    for item in items:
        rows.append([
            r["inputs"].get(item, 0) - (float(output(r, level, owned)) if r["output"] == item else 0)
            for r in recipes
        ] + [int(k == item) for k in items])
        limits.append(0)
    for index, energy in enumerate(ENERGIES):
        rows.append([r["energy"][index] for r in recipes] + [0] * len(items))
        limits.append(float(F(supply[index]) * boost(energy, owned)))
    costs = [0] * len(recipes) + [-RESOURCES[k]["points"] for k in items]
    bounds = [(0, 600 / r["seconds"]) for r in recipes] + [(0, None)] * len(items)
    result = linprog(costs, A_ub=rows, b_ub=limits, bounds=bounds, method="highs")
    if not result.success:
        raise RuntimeError(result.message)
    if any(sum(a * b for a, b in zip(row, result.x)) > cap + 1e-5 for row, cap in zip(rows, limits)):
        raise ArithmeticError("集計モデルの保存則・補給・加工能力に違反")
    return {"points_upper_bound": max(0.0, round(-result.fun, 3)),
            "deliveries": {k: round(result.x[len(recipes) + i], 3)
                           for i, k in enumerate(items) if result.x[len(recipes) + i] > 1e-6}}


def main():
    assert len(PENGUINS) == len({p["id"] for p in PENGUINS}) == 19
    assert sum(p["weight"] for p in PENGUINS) == 10000
    assert {p["target"] for p in PENGUINS} == set(RESOURCES) | set(ENERGIES)
    assert set(MODEL["output_growth"]) == {"1", "2", "3", "4", "5"}
    for recipe in RECIPES:
        tier = 1 + max((RESOURCES[k]["tier"] for k in recipe["inputs"]), default=0)
        assert recipe["tier"] == RESOURCES[recipe["output"]]["tier"] == tier
        assert any(recipe["energy"]) and all(e >= 0 for e in recipe["energy"])
    pools = {}
    for stage in range(1, 5):
        pool = [p for p in PENGUINS if p["unlock_stage"] <= stage]
        total = sum(p["weight"] for p in pool)
        probabilities = {p["id"]: F(p["weight"], total) for p in pool}
        assert sum(probabilities.values()) == 1
        pools[str(stage)] = {"total_weight": total,
                             "probabilities": {k: float(v) for k, v in probabilities.items()}}
    none = {}
    one = {p["id"]: 1 for p in PENGUINS}
    ten = {p["id"]: 10 for p in PENGUINS}
    assert boost("oden", one) == F(12, 5)
    assert boost("oden", ten) == 33
    assert boost("fish", one) == F(33, 25)
    assert F(0) * boost("fish", ten) == 0
    result = {
        "assumptions": {"one_building_per_recipe": True, "empty_start_inventory": True,
                        "fixed_level_without_unlock_cost": True, "instant_logistics": True,
                        "no_storage_limit": True, "no_offline_mechanic": True},
        "stage_probabilities": pools,
        "normal_draw_waits": {p["id"]: {
            "probability": p["weight"] / 10000,
            "mean_without_pity": 10000 / p["weight"],
            "p95_without_pity": math.ceil(math.log(.05) / math.log(1 - p["weight"] / 10000))
        } for p in PENGUINS},
        "cold_batches": {
            "F4_level1_no_birds_unlimited_supply": cold_batch("oden", 1, none, False),
            "F4_level1_no_birds_100_pack": cold_batch("oden", 1, none),
            "F3_gift_level3_no_birds": cold_batch("gift", 3, none),
            "F3_research_level3_no_birds": cold_batch("research", 3, none),
            "F4_level4_no_birds": cold_batch("oden", 4, none),
            "F4_level4_one_of_each": cold_batch("oden", 4, one),
            "F4_level4_ten_of_each": cold_batch("oden", 4, ten)
        },
        "unit_costs_level4_no_birds": unit_costs(4, none),
        "unit_costs_level4_one_each": unit_costs(4, one),
        "rare_target_with_pity": rare_target_cdf(),
    }
    if not all(v["completed"] for k, v in result["cold_batches"].items() if k != "F4_level1_no_birds_100_pack"):
        raise ArithmeticError("想定した補給パックで初回生産に届かない")
    if "--optimize" in sys.argv:
        profiles = {"balanced": [100, 100, 100], "gas_scarce": [25, 100, 100],
                    "electric_scarce": [100, 25, 100], "water_scarce": [100, 100, 25],
                    "empty": [0, 0, 0]}
        result["stage4_aggregate_bounds"] = {
            name: {"no_birds": optimize_snapshot(4, none, budget),
                   "one_of_each": optimize_snapshot(4, one, budget)}
            for name, budget in profiles.items()
        }
        assert result["stage4_aggregate_bounds"]["empty"]["one_of_each"]["points_upper_bound"] == 0
    print(json.dumps(result, ensure_ascii=False, indent=2))


if __name__ == "__main__":
    main()
