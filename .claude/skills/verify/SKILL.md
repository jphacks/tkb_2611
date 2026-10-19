---
name: verify
description: コミット・PR の前に、CI が回すのと同じ検査をローカルで一括実行する。検査・チェック・確認・コミット前・PR 前・CI が落ちた・レビュー前と言われたら使う。
---

# 出す前の検査

`.github/workflows/ci.yml` が PR ごとに回すものと同じ内容を、ローカルで先に通す。上から順に実行し、**落ちたものがあっても最後まで走らせてから**まとめて報告する。

## 1. 秘密情報

```bash
python3 scripts/check_secrets.py
```

public リポジトリなので最優先。落ちたら直す前に、それが既にコミット済みかを確かめる。コミット済みならキーの失効が要る (履歴からは消せない前提で扱う)。

## 1.5 個人に関わる内容

```bash
python3 scripts/check_personal_info.py --all
python3 scripts/check_personal_info.py --range origin/main..HEAD
```

拒否リスト (名前など) が無いという注意が出たら、`docs/personal-info.md` の手順で作ってから回し直す。機械で拾えない個人の事情・チームの会話の中身が差分に入っていないかも目で確かめる (`CLAUDE.md` の絶対規則 0)。入っていたら push の前に直す (push した後は PR の履歴から消せない)。

## 2. 契約

```bash
uv run --no-project --with openapi-spec-validator==0.9.0 --with jsonschema==4.26.0 --with pyyaml==6.0.3 python scripts/check_api_contract.py
```

`api/` を変えたときに落ちる。project-penguin の clone が隣にあれば、そちらで `--against ../tkb_2611/api` も回し、Unity 側の写真の表・エラーの code と食い違っていないかを見る (写しは `--sync` で更新する)。

## 3. backend

```bash
cd backend
uv run ruff format --check .
uv run ruff check .
uv run mypy .
uv run pytest -q
```

整形だけの差分なら `uv run ruff format .` を先に流してよい。

## 4. 事前と当日の境界

開始タグ (`hackday-start`) より前なら、エンドポイントの中身 (写真判定・みんなの氷山・お題) が差分に入っていないかを `git diff --stat main` で見る (`docs/hackday.md`)。

## 報告のしかた

検査ごとに 通った / 落ちた と、落ちたものは原因と次の一手を書く。全部通ったら `CLAUDE.md` の規約に沿ったコミットメッセージ案まで出す (**commit は指示があるまで実行しない**)。
