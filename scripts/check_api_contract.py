"""Unity と backend の契約 (api/openapi.yaml と api/examples) を検査する。

Hack Day には backend だけを変え、Unity は作り直さない (docs/decisions.md の 2026-10-19)。
そのため契約とクライアントの食い違いは当日まで見つからない。ここで機械的に止める。

- OpenAPI 3.1 として正しいか (openapi-spec-validator)
- api/examples/*.json がすべて契約から参照され、対応するスキーマに合うか
- 例の schema_version が info.version と同じか、エラーの例の HTTP 状態が code の表と合うか
- 写真の表の ID (EcoActions.cs の EcoSubjects) と、エラーの code (BackendError.cs) が契約と一致するか
- --against を付けると、正本 (jphacks/tkb_2611 の api/) と写しが同じか

    uv run --no-project --with openapi-spec-validator==0.9.0 --with jsonschema==4.26.0 \\
        --with pyyaml==6.0.3 python scripts/check_api_contract.py [--against ../tkb_2611/api]
"""

from __future__ import annotations

import argparse
import filecmp
import json
import re
import shutil
import sys
from pathlib import Path
from typing import Any

import yaml
from jsonschema import Draft202012Validator
from openapi_spec_validator import validate as validate_spec

ROOT = Path(__file__).resolve().parent.parent
API = ROOT / "api"
# Unity のプロジェクトは、提出用リポジトリでは unity/ に、project-penguin では直下にある。
ASSETS = ROOT / "unity" / "Assets" if (ROOT / "unity" / "Assets").exists() else ROOT / "Assets"
SUBJECTS_CS = ASSETS / "Scripts" / "Domain" / "Island" / "EcoActions.cs"
ERRORS_CS = ASSETS / "Scripts" / "Backend" / "BackendError.cs"

# 例のファイル名 `<対象>.<HTTP 状態>.<場合>.json` の対象 → 本文のスキーマ。
SCHEMA_OF = {
    "health": "Health",
    "judge": "JudgeResult",
    "community-goal": "CommunityGoal",
    "quests": "QuestList",
    "redeem": "RedeemResult",
    "water-estimate": "WaterEstimate",
    "encounter": "EncounterResult",
    "error": "ErrorResponse",
}

# エラーの code ごとに許す HTTP 状態と retryable (openapi.yaml の ErrorCode の表と同じ)。
ERROR_TABLE: dict[str, tuple[set[int], bool]] = {
    "image_missing": ({400}, False),
    "image_invalid": ({400, 415}, False),
    "image_too_large": ({413}, False),
    "code_invalid": ({400}, False),
    "quest_closed": ({404}, False),
    "already_redeemed": ({409}, False),
    "token_invalid": ({400}, False),
    "day_invalid": ({400}, False),
    "rate_limited": ({429}, True),
    "upstream_unavailable": ({503}, True),
    "internal": ({500}, True),
}

CLIENT_ONLY_CODES = {"network"}

EXAMPLE_NAME = re.compile(r"^(?P<target>[a-z-]+)\.(?P<status>\d{3})\.(?P<case>[a-z0-9_]+)\.json$")


def load_spec(api: Path) -> dict[str, Any]:
    with (api / "openapi.yaml").open(encoding="utf-8") as f:
        spec: dict[str, Any] = yaml.safe_load(f)
    return spec


def validator_for(spec: dict[str, Any], schema_name: str) -> Draft202012Validator:
    # components を根に置き、#/components/schemas/... の参照をそのまま解けるようにする。
    schema = {"$ref": f"#/components/schemas/{schema_name}", "components": spec["components"]}
    return Draft202012Validator(schema, format_checker=Draft202012Validator.FORMAT_CHECKER)


def referenced_examples(node: object) -> set[str]:
    found: set[str] = set()
    if isinstance(node, dict):
        for key, value in node.items():
            if key == "externalValue" and isinstance(value, str):
                found.add(value)
            else:
                found |= referenced_examples(value)
    elif isinstance(node, list):
        for item in node:
            found |= referenced_examples(item)
    return found


def check_examples(spec: dict[str, Any], api: Path) -> list[str]:
    errors: list[str] = []
    version = spec["info"]["version"]
    examples = api / "examples"
    files = {f"examples/{p.name}" for p in examples.glob("*.json")}
    refs = referenced_examples(spec["paths"]) | referenced_examples(spec["components"])

    for missing in sorted(refs - files):
        errors.append(f"契約が参照している {missing} がありません")
    for orphan in sorted(files - refs):
        errors.append(f"{orphan} がどこからも参照されていません (openapi.yaml の examples に足す)")

    validators = {name: validator_for(spec, name) for name in SCHEMA_OF.values()}
    for path in sorted(examples.glob("*.json")):
        match = EXAMPLE_NAME.match(path.name)
        if match is None or match["target"] not in SCHEMA_OF:
            errors.append(f"{path.name}: 名前が <対象>.<HTTP 状態>.<場合>.json の形ではありません")
            continue
        body = json.loads(path.read_text(encoding="utf-8"))
        for problem in validators[SCHEMA_OF[match["target"]]].iter_errors(body):
            where = "/".join(str(p) for p in problem.absolute_path) or "(本文)"
            errors.append(f"{path.name}: {where}: {problem.message}")

        status = int(match["status"])
        if match["target"] == "error":
            code = body.get("error", {}).get("code")
            allowed = ERROR_TABLE.get(code)
            if allowed is None:
                continue  # 知らない code はスキーマの検査で落ちている
            statuses, retryable = allowed
            if status not in statuses:
                errors.append(f"{path.name}: {code} は HTTP {sorted(statuses)} で返す")
            if body["error"].get("retryable") is not retryable:
                errors.append(f"{path.name}: {code} の retryable は {retryable}")
        else:
            if status != 200:
                errors.append(f"{path.name}: エラー以外の例は 200 にする")
            if body.get("schema_version") != version:
                errors.append(f"{path.name}: schema_version が info.version ({version}) と違います")
    return errors


def check_error_table(spec: dict[str, Any]) -> list[str]:
    codes = set(spec["components"]["schemas"]["ErrorCode"]["enum"])
    if codes == set(ERROR_TABLE):
        return []
    return [f"ErrorCode の enum と、このスクリプトの ERROR_TABLE が違います: {sorted(codes ^ set(ERROR_TABLE))}"]


def subjects_in_csharp() -> set[str]:
    text = SUBJECTS_CS.read_text(encoding="utf-8")
    table = text[text.index("class EcoSubjects") :]
    return set(re.findall(r"new EcoSubjectDef\(EcoSubject\.\w+,\s*\"([a-z_]+)\"", table))


def error_codes_in_csharp() -> set[str]:
    text = ERRORS_CS.read_text(encoding="utf-8")
    codes = set(re.findall(r"public const string \w+ = \"([a-z_]+)\";", text))
    # 接続できなかったときの code はクライアントだけが使う (サーバーは返さない)。
    return codes - CLIENT_ONLY_CODES


def check_client(spec: dict[str, Any]) -> list[str]:
    if not SUBJECTS_CS.parent.parent.exists():
        # 提出用リポジトリ (backend だけ) には Unity のファイルが無い。突き合わせは project-penguin で
        # --against を付けて行う。
        print("Unity のファイルが無いので、写真の表とエラーの code の突き合わせは飛ばします")
        return []
    errors: list[str] = []
    schemas = spec["components"]["schemas"]

    contract_subjects = set(schemas["EcoSubjectId"]["enum"]) - {"unknown"}
    client_subjects = subjects_in_csharp() if SUBJECTS_CS.exists() else set()
    if not client_subjects:
        errors.append(f"{SUBJECTS_CS.relative_to(ROOT)} から写真の表の ID を読めません")
    elif contract_subjects != client_subjects:
        errors.append(
            "写真の表の ID が契約と EcoSubjects で違います "
            f"(契約だけ: {sorted(contract_subjects - client_subjects)}, "
            f"表だけ: {sorted(client_subjects - contract_subjects)})"
        )

    if not ERRORS_CS.exists():
        errors.append(f"{ERRORS_CS.relative_to(ROOT)} がありません")
    else:
        contract_codes = set(schemas["ErrorCode"]["enum"])
        client_codes = error_codes_in_csharp()
        if contract_codes != client_codes:
            errors.append(
                "エラーの code が契約と BackendError.cs で違います "
                f"(契約だけ: {sorted(contract_codes - client_codes)}, "
                f"クライアントだけ: {sorted(client_codes - contract_codes)})"
            )
    return errors


def check_against(canonical: Path) -> list[str]:
    if not (canonical / "openapi.yaml").exists():
        return [f"{canonical} に openapi.yaml がありません (tkb_2611 の api/ を指す)"]
    errors: list[str] = []
    compare = filecmp.dircmp(canonical, API)
    pending = [(compare, Path())]
    while pending:
        current, rel = pending.pop()
        for name in current.left_only:
            errors.append(f"正本にだけある: {rel / name} (--sync で写す)")
        for name in current.right_only:
            errors.append(f"写しにだけある: {rel / name}")
        for name in current.diff_files:
            errors.append(f"正本と違う: {rel / name}")
        pending.extend((sub, rel / name) for name, sub in current.subdirs.items())
    return errors


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description="Unity と backend の契約を検査する。")
    parser.add_argument(
        "--against", type=Path, help="正本の api/ (jphacks/tkb_2611 の clone の api)。写しと比べる"
    )
    parser.add_argument(
        "--sync", action="store_true", help="--against の正本で api/ を置き換えてから検査する"
    )
    args = parser.parse_args(argv)
    if args.sync:
        if args.against is None:
            parser.error("--sync には --against が要ります")
        shutil.rmtree(API)
        shutil.copytree(args.against.resolve(), API)

    spec = load_spec(API)
    errors: list[str] = []
    try:
        validate_spec(spec)
    except Exception as error:  # 検査の失敗はまとめて報告する
        errors.append(f"openapi.yaml が OpenAPI 3.1 として正しくありません: {error}")
    errors += check_error_table(spec)
    errors += check_examples(spec, API)
    errors += check_client(spec)
    if args.against is not None:
        errors += check_against(args.against.resolve())

    if errors:
        print("契約の検査に失敗しました:", file=sys.stderr)
        for line in errors:
            print(f"  {line}", file=sys.stderr)
        return 1
    print(f"契約 {spec['info']['version']} の検査が通りました")
    return 0


if __name__ == "__main__":
    sys.exit(main())
