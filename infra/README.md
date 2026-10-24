# infra (backend の AWS 資源)

`backend.yaml` は、backend が使う AWS 側の土台を作る CloudFormation のテンプレート。

| 資源 | 用途 |
|---|---|
| `penguin-backend-community` (DynamoDB) | 本番のみんなの氷山。提出の記録を条件付き Put で 1 回だけ通し、通ったときだけ 10 分割のカウンタに ADD する |
| `penguin-backend-community-dev` (DynamoDB) | 手元の開発と Vercel の preview 用 |
| Nova 2 Lite の呼び出しの管理ポリシー | Bedrock は `jp.amazon.nova-2-lite-v1:0` だけ (写真を国内で処理する) |
| Vercel 用のロール (production・preview) と OIDC プロバイダー | Vercel Functions が `x-vercel-oidc-token` でロールを借りる。長く使えるアクセスキーは作らない。preview のロールは開発用のテーブルにしか触れない |
| アラーム (SNS でメール) | Nova の呼び出しが 1 時間の目安を超えた・絞られた、本番のテーブルが上限で絞られた、のどれかでオーナーに届く |

選んだ理由と検証の結果は、project-penguin の `docs/adr/0001`・`0002`・`0003` にある。

## デプロイできるのはオーナーだけ

チームメイトには CloudFormation と IAM の権限が無い。テンプレートを変えたいときは PR を出し、オーナーがレビューして変更セットを確かめてから実行する。

project-penguin の `infra/account.yaml` (開発者グループ) は、このスタックが出す管理ポリシーを import する。そのため**このスタックを先に作り**、Export の名前 (`penguin-backend-*`) を変えるときは、先に `account.yaml` 側を直す。

## 手順 (オーナー)

1. 検査を通す。

   ```bash
   uv run --no-project --with cfn-lint==1.58.0 cfn-lint --regions ap-northeast-1 -- infra/*.yaml
   ```

   安全性のルールでも見る (cfn-guard、`brew install cloudformation-guard`)。ルールは AWS Guard Rules Registry のリリース (https://github.com/aws-cloudformation/aws-guard-rules-registry/releases、`ruleset-build-*.zip` の `output/`) から取る。`wa-Security-Pillar`・`wa-Reliability-Pillar`・`cis-aws-benchmark-level-2` で通すこと。

   ```bash
   cfn-guard validate --rules <展開先>/output/wa-Security-Pillar.guard --data infra/<テンプレート>.yaml
   ```

2. パラメータのファイルを手元に作る。`infra/*.local.json` は gitignore 済み。Vercel のチーム名と通知先のメールアドレスはこのリポジトリ (public) に書かない。

   ```json
   [
     {"ParameterKey": "VercelTeamSlug", "ParameterValue": "<Vercel のチームの slug>"},
     {"ParameterKey": "VercelProject", "ParameterValue": "project-penguin"},
     {"ParameterKey": "AlertEmail", "ParameterValue": "<アラームを受け取るメールアドレス>"}
   ]
   ```

   これを `infra/backend.local.json` として保存する。テーブルの上限 (`ProdMaxReadRequestUnits` など) と呼び出しの目安 (`JudgeInvocationsPerHourAlarm`) は既定値のままでよく、変えるときだけ足す。

3. 変更セットを作り、中身を確かめてから実行する。

   ```bash
   aws cloudformation create-change-set --profile penguin --region ap-northeast-1 \
     --stack-name penguin-backend --change-set-name "cs-$(date +%s)" \
     --change-set-type CREATE \
     --template-body file://infra/backend.yaml \
     --parameters file://infra/backend.local.json \
     --capabilities CAPABILITY_IAM \
     --tags Key=project,Value=penguin Key=managed-by,Value=cloudformation
   ```

   - 2 回目からは `--change-set-type UPDATE` にする。
   - `describe-change-set` で中身を見る。**本番のテーブルに `Replacement: True` が出たら実行しない** (件数が消える)。
   - テーブルの上限 (`OnDemandThroughput`) の変更は置き換えにならない。
   - 確かめたら `execute-change-set` で実行する。
   - 実行後に届く SNS の購読の確認メールのリンクを開く。開くまでアラームは届かない。

4. Outputs の値を Vercel の環境変数に入れる。

   | 変数 | production | preview |
   |---|---|---|
   | `PENGUIN_AWS_ROLE_ARN` | `VercelBackendRoleArn` | `VercelPreviewRoleArn` |
   | `PENGUIN_COMMUNITY_TABLE` | `CommunityTableName` | `CommunityDevTableName` |

   あわせて、Vercel のプロジェクトの設定で OIDC (Secure Backend Access with OIDC Federation、発行者は Team) を有効にする。

## 片付け

本番のテーブルは `DeletionPolicy: Retain` なので、スタックを消しても残る。完全に消すときは、スタックを消したあとにテーブルを手で消す。JPHACKS の後も製品で使うので、消す予定は無い。
