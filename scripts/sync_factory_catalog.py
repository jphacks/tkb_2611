"""Build Unity's array-based catalog from the documented proposal model."""

import argparse
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / "docs/domain/factory-economy/progression-model.json"
TARGET = ROOT / "unity/Assets/Resources/Factory/catalog.json"


def render() -> str:
    model = json.loads(SOURCE.read_text(encoding="utf-8"))
    catalog = {
        "version": model["version"],
        "status": model["status"],
        "growth": list(model["output_growth"].values()),
        "storage": model["warehouse"]["storage_per_resource_base"],
        "relay": model["warehouse"]["relay_per_second_base"],
        "warehouseRelay": model["warehouse"]["throughput_per_second_base"],
        "dailyBirds": model["encounter"]["daily_reward_limit"],
        "pityAfter": model["encounter"]["new_species_pity_after_misses"],
        "resources": [
            dict(id=key, **value) for key, value in model["resources"].items()
        ],
        "recipes": [
            {
                "id": r["id"],
                "label": r["label"],
                "tier": r["tier"],
                "durationMs": r["seconds"] * 1000,
                "inputs": [{"id": k, "amount": v} for k, v in r["inputs"].items()],
                "output": r["output"],
                "yieldAmount": r["yield"],
                "energy": r["energy"],
            }
            for r in model["recipes"]
        ],
        "penguins": [
            {
                "id": p["id"],
                "label": p["label"],
                "target": p["target"],
                "multiplicative": p["mode"] == "multiply",
                "percent": round(p["coefficient"] * 100),
                "stage": p["unlock_stage"],
                "weight": p["weight"],
            }
            for p in model["penguins"]
        ],
    }
    return json.dumps(catalog, ensure_ascii=False, indent=2) + "\n"


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--check", action="store_true")
    args = parser.parse_args()
    content = render()
    if args.check:
        if not TARGET.exists() or TARGET.read_text(encoding="utf-8") != content:
            raise SystemExit(
                "Factory catalog differs from the proposal; run scripts/sync_factory_catalog.py"
            )
        print("Factory catalog matches the documented proposal.")
    else:
        TARGET.parent.mkdir(parents=True, exist_ok=True)
        TARGET.write_text(content, encoding="utf-8")


if __name__ == "__main__":
    main()
