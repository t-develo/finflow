# FinFlow Azure デプロイ手順書

## 目次

1. [概要・アーキテクチャ](#概要アーキテクチャ)
2. [前提条件](#前提条件)
3. [初回セットアップ](#初回セットアップ)
4. [GitHub Secrets の設定](#github-secrets-の設定)
5. [自動デプロイ（GitHub Actions）](#自動デプロイgithub-actions)
6. [手動デプロイ](#手動デプロイ)
7. [環境変数一覧](#環境変数一覧)
8. [コスト見積もり](#コスト見積もり)
9. [トラブルシューティング](#トラブルシューティング)
10. [ロールバック手順](#ロールバック手順)

---

## 概要・アーキテクチャ

### デプロイ構成

```
GitHub リポジトリ
  │
  ├── develop ブランチへ push
  │       └─→ GitHub Actions (deploy-dev.yml)
  │               └─→ Azure Dev 環境
  │                     ├── App Service (F1 Free)
  │                     └── Azure SQL Database (Serverless)
  │
  └── main ブランチへ push
          └─→ GitHub Actions (deploy-prod.yml)
                  └─→ Azure Prod 環境
                        ├── App Service (B1 Basic)
                        └── Azure SQL Database (Basic 5DTU)
```

### Azure リソース構成

| リソース | Dev 環境 | Prod 環境 |
|---------|---------|----------|
| リソースグループ | `finflow-dev-rg` | `finflow-prod-rg` |
| App Service Plan | F1 Free (0円) | F1 Free (0円) |
| Web App | `finflow-dev-xxxxxx` | `finflow-prod-xxxxxx` |
| SQL Server | `finflow-dev-sql-xxxxxx` | `finflow-prod-sql-xxxxxx` |
| SQL Database | Serverless (自動停止) | Serverless (自動停止) |

> `xxxxxx` は Azure 内でグローバル一意となる自動生成サフィックスです。

### アプリケーション構成

- **バックエンド**: ASP.NET Core 8 Web API (Linux)
- **フロントエンド**: Vanilla JS SPA (静的ファイルとして API から配信)
- **データベース**: Azure SQL Database (EF Core マイグレーション自動実行)
- **認証**: JWT Bearer トークン

---

## 前提条件

初回セットアップを行う PC に以下をインストールしてください。

| ツール | バージョン | インストール方法 |
|-------|----------|----------------|
| Azure CLI | 2.50 以上 | https://learn.microsoft.com/cli/azure/install-azure-cli |
| .NET SDK | 8.0 以上 | https://dotnet.microsoft.com/download |
| jq | 1.6 以上 | `brew install jq` / `apt install jq` / https://jqlang.github.io/jq/download/ |
| openssl | 任意 | OS 標準で通常インストール済み |
| zip | 任意 | OS 標準で通常インストール済み |

GitHub Actions で自動デプロイする場合は、上記ツールが CI 環境 (ubuntu-latest) に自動でセットアップされるため、**ローカルに必要なのは初回セットアップ時のみ**です。

---

## 初回セットアップ

### ステップ 1: セットアップスクリプトの実行

```bash
# リポジトリのルートで実行
chmod +x infra/scripts/setup.sh
./infra/scripts/setup.sh
```

スクリプトが以下を自動で行います:

1. Azure ログイン（ブラウザが開きます）
2. サブスクリプションの選択
3. リソースグループの作成
   - `finflow-dev-rg`
   - `finflow-prod-rg`
4. GitHub Actions 用サービスプリンシパルの作成
5. JWT キーおよび SQL パスワードの自動生成
6. 設定すべき GitHub Secrets の一覧を画面出力
7. 生成値を `infra/scripts/.env.setup` に保存（gitignore 対象）

### ステップ 2: GitHub Secrets の設定

[次のセクション](#github-secrets-の設定) を参照してください。

---

## GitHub Secrets の設定

GitHub リポジトリの `Settings > Secrets and variables > Actions` で以下の Secrets を設定してください。

`setup.sh` を実行すると値が画面に出力されます。それをコピーして設定してください。

### 必須 Secrets

| Secret 名 | 説明 | 取得方法 |
|-----------|------|---------|
| `AZURE_CREDENTIALS` | サービスプリンシパルの JSON | `setup.sh` の出力 |
| `AZURE_SUBSCRIPTION_ID` | Azure サブスクリプション ID | `setup.sh` の出力 |
| `DEV_JWT_KEY` | Dev 環境の JWT 署名キー | `setup.sh` の出力 |
| `PROD_JWT_KEY` | Prod 環境の JWT 署名キー | `setup.sh` の出力 |
| `DEV_SQL_ADMIN_PASSWORD` | Dev 環境の SQL 管理者パスワード | `setup.sh` の出力 |
| `PROD_SQL_ADMIN_PASSWORD` | Prod 環境の SQL 管理者パスワード | `setup.sh` の出力 |

### 任意 Secrets（メール通知を使う場合）

| Secret 名 | 説明 | 例 |
|-----------|------|---|
| `SMTP_HOST` | SMTP サーバーホスト | `smtp.sendgrid.net` |
| `SMTP_USERNAME` | SMTP ユーザー名 | `apikey` |
| `SMTP_PASSWORD` | SMTP パスワード / API キー | SendGrid の API キー |
| `SMTP_FROM_ADDRESS` | 送信元メールアドレス | `noreply@yourdomain.com` |

> SMTP を設定しない場合、メール通知機能はエラーになりますが、他の機能は正常に動作します。

### `AZURE_CREDENTIALS` の形式

```json
{
  "clientId": "xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx",
  "clientSecret": "xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx",
  "subscriptionId": "xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx",
  "tenantId": "xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx"
}
```

---

## 自動デプロイ（GitHub Actions）

### ブランチ戦略

```
feature/* ─→ develop ─→ main
                │            │
                ↓            ↓
            Dev 環境    Prod 環境
```

| ブランチ | デプロイ先 | ワークフロー |
|---------|----------|------------|
| `develop` | Dev 環境 | `.github/workflows/deploy-dev.yml` |
| `main` | Prod 環境 | `.github/workflows/deploy-prod.yml` |

### ワークフローの流れ

```
push to develop/main
  │
  ├── [1] テスト実行 (dotnet test)
  │       └── 失敗時: デプロイ中断
  │
  └── [2] デプロイ (テスト成功時のみ)
          ├── dotnet publish (Release ビルド)
          ├── フロントエンドを wwwroot にコピー
          ├── ZIP パッケージ作成
          ├── Azure ログイン
          ├── ARM テンプレートでインフラデプロイ（冪等）
          └── App Service に ZIP デプロイ
```

### 本番環境への承認ゲートの追加（任意）

main ブランチへのデプロイ前に承認を必須にするには:

1. GitHub リポジトリの `Settings > Environments > production`
2. `Required reviewers` に承認者を追加

これにより、承認者が OK するまでデプロイが待機します。

### ワークフロー手動実行

GitHub Actions タブから `workflow_dispatch` で手動実行も可能です。

---

## 手動デプロイ

GitHub Actions を使わずにローカルから直接デプロイする場合:

### 前提

- `infra/scripts/.env.setup` が存在すること（`setup.sh` 実行済み）
- または環境変数が設定されていること

### 実行手順

```bash
# Azure にログイン
az login

# Dev 環境にデプロイ
./infra/scripts/deploy.sh dev

# Prod 環境にデプロイ
./infra/scripts/deploy.sh prod
```

### 環境変数で直接指定する場合

`.env.setup` ファイルがない環境では環境変数で渡せます:

```bash
DEV_JWT_KEY="your-jwt-key" \
DEV_SQL_ADMIN_PASSWORD="your-sql-password" \
DEV_RESOURCE_GROUP="finflow-dev-rg" \
./infra/scripts/deploy.sh dev
```

---

## 環境変数一覧

App Service に設定される環境変数の一覧です。ARM テンプレートが自動的に設定します。

| 変数名 | 説明 | Dev | Prod |
|-------|------|-----|------|
| `ASPNETCORE_ENVIRONMENT` | 実行環境 | `Development` | `Production` |
| `ASPNETCORE_URLS` | リッスンポート | `http://0.0.0.0:8080` | 同左 |
| `ConnectionStrings__DefaultConnection` | SQL 接続文字列 | 自動生成 | 自動生成 |
| `Jwt__Key` | JWT 署名キー (32字以上) | GitHub Secret | GitHub Secret |
| `Jwt__Issuer` | JWT 発行者 | `FinFlowApi` | `FinFlowApi` |
| `Jwt__Audience` | JWT 対象者 | `FinFlowClient` | `FinFlowClient` |
| `Jwt__ExpiryHours` | JWT 有効期限 | `24` | `24` |
| `Cors__AllowedOrigins__0` | CORS 許可オリジン | App Service URL | App Service URL |
| `Smtp__Host` | SMTP ホスト | GitHub Secret | GitHub Secret |
| `Smtp__Port` | SMTP ポート | `587` | `587` |
| `Smtp__EnableSsl` | SMTP SSL | `true` | `true` |
| `Smtp__Username` | SMTP ユーザー名 | GitHub Secret | GitHub Secret |
| `Smtp__Password` | SMTP パスワード | GitHub Secret | GitHub Secret |
| `Smtp__FromAddress` | 送信元アドレス | GitHub Secret | GitHub Secret |

---

## コスト見積もり

### 月額費用（概算）

| リソース | Dev 環境 | Prod 環境 |
|---------|---------|----------|
| App Service Plan | **無料 (F1)** | **無料 (F1)** |
| Azure SQL Database | **~¥0〜¥300 (Serverless)** | **~¥0〜¥300 (Serverless)** |
| SQL Server (論理サーバー) | 無料 | 無料 |
| **合計** | **~¥0〜¥300/月** | **~¥0〜¥300/月** |

> 為替レートにより変動します (1 USD ≈ 150 JPY 想定)

### 注意事項

- **F1 (Free)**: CPU 60分/日の制限あり。個人利用では十分な範囲。カスタムドメイン・SSL バインドは非対応（`*.azurewebsites.net` のみ）。
- **Azure SQL Serverless**: 1時間無操作で自動停止。再起動時に~30秒かかる場合あり。
- スケールアップが必要になった場合は `prod.parameters.json` の `appServicePlanSku` を `B1`、`sqlDatabaseTier` を `Basic` に変更して再デプロイするだけで適用されます。
- **環境の削除**: 不要な場合は `az group delete --name finflow-dev-rg` でリソースグループごと削除できます。

---

## トラブルシューティング

### デプロイ後にアプリが起動しない

**症状**: アクセスすると 503 エラーが返る

**確認方法**:
```bash
# App Service のログを確認
az webapp log tail \
  --resource-group finflow-dev-rg \
  --name <web-app-name>
```

**よくある原因**:
- JWT キーがデフォルト値のまま (Prod 環境では起動エラーになる)
- SQL 接続文字列が間違っている
- SQL ファイアウォールが未設定

### データベース接続エラー

**症状**: ログに `Cannot open server 'xxx' requested by the login` が出る

**確認方法**:
```bash
# Azure SQL ファイアウォールルールを確認
az sql server firewall-rule list \
  --resource-group finflow-dev-rg \
  --server <sql-server-name>
```

**解決方法**:
ARM テンプレートの `AllowAzureServices` ルールが `0.0.0.0` → `0.0.0.0` で設定されていることを確認。
なければ手動で追加:
```bash
az sql server firewall-rule create \
  --resource-group finflow-dev-rg \
  --server <sql-server-name> \
  --name AllowAzureServices \
  --start-ip-address 0.0.0.0 \
  --end-ip-address 0.0.0.0
```

### マイグレーション失敗

**症状**: 起動ログに `Migration failed` が出る

**確認方法**:
```bash
az webapp log tail --resource-group finflow-dev-rg --name <web-app-name>
```

**解決方法**: SQL パスワードを確認し、接続文字列が正しいことを確認してください。
ローカルから直接マイグレーションを実行することも可能です:
```bash
ConnectionStrings__DefaultConnection="Server=<fqdn>;..." \
dotnet ef database update \
  --project src/FinFlow.Infrastructure \
  --startup-project src/FinFlow.Api
```

### GitHub Actions が失敗する (`AZURE_CREDENTIALS` 関連)

**症状**: `az login` ステップで認証エラー

**解決方法**:
1. `AZURE_CREDENTIALS` Secret の JSON 形式が正しいか確認
2. サービスプリンシパルのシークレットが期限切れでないか確認:
```bash
az ad sp show --id <clientId>
```
3. 必要であれば `setup.sh` を再実行して SP を作り直す

### F1 プランで `AlwaysOn` エラー

**症状**: ARM デプロイで `The site is a free site and 'alwaysOn' cannot be enabled.` エラー

**確認**: ARM テンプレートは F1 の場合 `alwaysOn: false` を自動設定する。
パラメータファイルで `appServicePlanSku` が `F1` になっているか確認。

### Azure SQL Serverless が接続できない

**症状**: Dev 環境でしばらく経つと接続が遅い・タイムアウトする

**原因**: Serverless DB が自動停止している（正常動作）

**解決方法**: 数十秒待って再試行してください。防ぐには:
```json
// dev.parameters.json で Serverless から Basic に変更
"sqlDatabaseTier": { "value": "Basic" }
```

---

## ロールバック手順

### アプリケーションのロールバック

前のコミットに戻して再デプロイ:

```bash
# Git でリバートして push (自動デプロイが走る)
git revert HEAD
git push origin develop  # または main
```

特定のバージョンを直接デプロイ:
```bash
git checkout <commit-hash>
./infra/scripts/deploy.sh dev
git checkout -
```

### データベースのロールバック

```bash
# ローカルから接続して特定マイグレーションに戻す
ConnectionStrings__DefaultConnection="Server=<fqdn>;..." \
dotnet ef database update <MigrationName> \
  --project src/FinFlow.Infrastructure \
  --startup-project src/FinFlow.Api
```

> **注意**: データが削除される可能性があります。必ずバックアップを取ってから実行してください。

### Azure SQL のバックアップ

Azure SQL は自動バックアップが有効です。Azure ポータルから復元可能です:

1. Azure ポータル → SQL データベース → バックアップ
2. 復元したいポイントを選択

---

## 参考リンク

- [Azure App Service ドキュメント](https://learn.microsoft.com/azure/app-service/)
- [Azure SQL Database ドキュメント](https://learn.microsoft.com/azure/azure-sql/)
- [ARM テンプレートリファレンス](https://learn.microsoft.com/azure/templates/)
- [GitHub Actions azure/login](https://github.com/azure/login)
- [GitHub Actions azure/arm-deploy](https://github.com/azure/arm-deploy)
- [GitHub Actions azure/webapps-deploy](https://github.com/azure/webapps-deploy)
