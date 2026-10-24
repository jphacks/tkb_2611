"""みんなの氷山のテーブルの偽物 (moto)。キーと TTL は infra/backend.yaml から読む。

テンプレートを正にするので、テーブルの形を変えたら、テストの表も同じに変わる (二重に持たない)。
moto は条件付き Put (attribute_not_exists) と ADD を本物と同じ規則で扱うので、
ADR 0002 の「条件付き Put → 通ったときだけ ADD」の数え方をそのまま試せる。
"""

from __future__ import annotations

import re
from dataclasses import dataclass
from pathlib import Path
from typing import Any

import yaml
from mypy_boto3_dynamodb import DynamoDBClient

TEMPLATE = Path(__file__).resolve().parents[3] / "infra" / "backend.yaml"
# infra/README.md の手順で作るスタックの名前。テーブル名は ${AWS::StackName}-community など。
STACK_NAME = "penguin-backend"


class _CloudFormationLoader(yaml.SafeLoader):
    """!Ref・!Sub・!GetAtt などの短い書き方を {"Fn::Sub": ...} の形で読む。"""


def _construct_tag(loader: yaml.SafeLoader, suffix: str, node: yaml.Node) -> dict[str, Any]:
    name = "Ref" if suffix == "Ref" else f"Fn::{suffix}"
    value: Any
    if isinstance(node, yaml.ScalarNode):
        value = loader.construct_scalar(node)
    elif isinstance(node, yaml.SequenceNode):
        value = loader.construct_sequence(node, deep=True)
    else:
        assert isinstance(node, yaml.MappingNode)
        value = loader.construct_mapping(node, deep=True)
    return {name: value}


_CloudFormationLoader.add_multi_constructor("!", _construct_tag)


@dataclass(frozen=True)
class TableSpec:
    logical_id: str
    name: str
    key_schema: list[dict[str, str]]
    attributes: list[dict[str, str]]
    ttl_attribute: str | None


def _resolve_name(value: Any) -> str:
    if isinstance(value, str):
        return value
    template = value["Fn::Sub"]
    assert isinstance(template, str), "テーブル名の !Sub は文字列の形だけを扱う"
    return re.sub(r"\$\{AWS::StackName\}", STACK_NAME, template)


def table_specs() -> dict[str, TableSpec]:
    """テンプレートの DynamoDB のテーブル (論理 ID → 形)。"""
    with TEMPLATE.open(encoding="utf-8") as f:
        template = yaml.load(f, Loader=_CloudFormationLoader)  # noqa: S506 (SafeLoader の派生)
    specs: dict[str, TableSpec] = {}
    for logical_id, resource in template["Resources"].items():
        if resource["Type"] != "AWS::DynamoDB::Table":
            continue
        props = resource["Properties"]
        ttl = props.get("TimeToLiveSpecification") or {}
        specs[logical_id] = TableSpec(
            logical_id=logical_id,
            name=_resolve_name(props["TableName"]),
            key_schema=props["KeySchema"],
            attributes=props["AttributeDefinitions"],
            ttl_attribute=ttl.get("AttributeName") if ttl.get("Enabled") else None,
        )
    return specs


def create_tables(client: DynamoDBClient) -> dict[str, str]:
    """moto の中にテンプレートと同じテーブルを作り、論理 ID → テーブル名を返す。"""
    names: dict[str, str] = {}
    for spec in table_specs().values():
        client.create_table(
            TableName=spec.name,
            BillingMode="PAY_PER_REQUEST",
            KeySchema=spec.key_schema,  # type: ignore[arg-type]
            AttributeDefinitions=spec.attributes,  # type: ignore[arg-type]
        )
        if spec.ttl_attribute:
            client.update_time_to_live(
                TableName=spec.name,
                TimeToLiveSpecification={"AttributeName": spec.ttl_attribute, "Enabled": True},
            )
        names[spec.logical_id] = spec.name
    return names
