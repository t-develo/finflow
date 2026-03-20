# FinFlow

家計・サブスクリプション管理アプリ。支出の記録・分類、サブスク管理、月次レポート生成をワンストップで提供します。

## 機能概要

- **支出管理** — 支出の登録・編集・削除、キーワードによる自動カテゴリ分類
- **CSV 取込** — 汎用フォーマット / 三菱UFJ / 楽天カードの明細を一括インポート
- **サブスクリプション管理** — 月次/年次課金の登録・管理、更新日アラート通知
- **レポート** — 月別集計、カテゴリ別内訳、ダッシュボード、PDF ダウンロード
- **認証** — メール/パスワード登録・ログイン（JWT Bearer トークン）

## 技術スタック

| レイヤー | 技術 |
|---------|------|
| バックエンド | C# / .NET 8, ASP.NET Core Web API |
| データベース | SQL Server / Azure SQL Database, Entity Framework Core |
| フロントエンド | Vanilla JS (ES2020+), Web Components, Chart.js |
| 認証 | ASP.NET Identity + JWT |
| テスト | xUnit, FluentAssertions, Moq |
| PDF 生成 | QuestPDF |
| CSV パース | CsvHelper |

## インフラ構成

```
GitHub リポジトリ
  │
  ├── develop ブランチ push
  │       └─→ GitHub Actions (deploy-dev.yml)
  │               └─→ Azure Dev 環境
  │                     ├── App Service (F1 Free)
  │                     └── Azure SQL Database (Serverless)
  │
  └── main ブランチ push
          └─→ GitHub Actions (deploy-prod.yml)
                  └─→ Azure Prod 環境
                        ├── App Service (F1 Free)
                        └── Azure SQL Database (Serverless)
```

### Azure リソース一覧

| リソース | Dev 環境 | Prod 環境 |
|---------|---------|----------|
| リソースグループ | `finflow-dev-rg` | `finflow-prod-rg` |
| App Service Plan | F1 Free | F1 Free |
| Web App | `finflow-dev-xxxxxx` | `finflow-prod-xxxxxx` |
| SQL Server | `finflow-dev-sql-xxxxxx` | `finflow-prod-sql-xxxxxx` |
| SQL Database | Serverless (自動停止) | Serverless (自動停止) |

> `xxxxxx` は Azure 内でグローバル一意となる自動生成サフィックスです。

インフラは ARM テンプレート (`infra/arm/main.json`) で管理されており、デプロイのたびに冪等に適用されます。

## ソリューション構成

```
src/
├── FinFlow.Api/             # ASP.NET Core Web API（コントローラー、Program.cs、ミドルウェア）
├── FinFlow.Domain/          # エンティティ、インターフェース、ドメインロジック
└── FinFlow.Infrastructure/  # EF Core DbContext、リポジトリ、サービス、マイグレーション
src/frontend/               # Vanilla JS SPA（ビルド不要、ES Modules）
tests/
└── FinFlow.Tests/           # xUnit テスト
infra/
├── arm/                     # ARM テンプレートとパラメータファイル
└── scripts/                 # セットアップ・デプロイスクリプト
.github/workflows/           # GitHub Actions CI/CD
docs/                        # 設計ドキュメント、API 仕様、WBS
```

---

## ローカル開発セットアップ

### 必要なツール

| ツール | バージョン |
|-------|----------|
| .NET SDK | 8.0 以上 |
| SQL Server / LocalDB | 2019 以上 |
| dotnet-ef | 8.0.0 |
| Docker (任意) | メール通知テスト用 MailHog |

### 手順

**1. リポジトリのクローン**

```bash
git clone <repository-url>
cd finflow
```

**2. dotnet-ef のインストール**

```bash
dotnet tool install --global dotnet-ef --version 8.0.0
```

**3. データベースのセットアップ**

```bash
dotnet ef database update \
  --project src/FinFlow.Infrastructure \
  --startup-project src/FinFlow.Api
```

**4. API サーバーの起動**

```bash
dotnet run --project src/FinFlow.Api
```

- API: `http://localhost:5000`
- Swagger UI: `http://localhost:5000/swagger`
- フロントエンド: `http://localhost:5000`

**5. メール通知テスト用 MailHog（任意）**

```bash
docker run -d -p 1025:1025 -p 8025:8025 mailhog/mailhog
# 管理 UI: http://localhost:8025
```

### 開発用設定

`src/FinFlow.Api/appsettings.Development.json` で接続文字列や JWT キーを上書きできます。

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Database=FinFlow_Dev;..."
  },
  "Jwt": {
    "Key": "開発用キー（本番は必ず変更）"
  }
}
```

---

## よく使うコマンド

```bash
# ビルド
dotnet build

# テスト
dotnet test

# 特定クラスのみ
dotnet test --filter "ClassName=ExpensesControllerTests"

# マイグレーション追加
dotnet ef migrations add <MigrationName> \
  --project src/FinFlow.Infrastructure \
  --startup-project src/FinFlow.Api

# DB 更新
dotnet ef database update \
  --project src/FinFlow.Infrastructure \
  --startup-project src/FinFlow.Api

# リリースビルド・発行
dotnet publish src/FinFlow.Api --configuration Release --output ./publish
```

---

## Azure デプロイ

詳細は [docs/AZURE_DEPLOYMENT.md](docs/AZURE_DEPLOYMENT.md) を参照してください。

### 初回セットアップ

Azure リソースの作成と GitHub Secrets の設定値生成を自動で行います。

```bash
chmod +x infra/scripts/setup.sh
./infra/scripts/setup.sh
```

スクリプトが行うこと:
1. Azure ログイン
2. サブスクリプションの選択
3. リソースグループ (`finflow-dev-rg`, `finflow-prod-rg`) の作成
4. GitHub Actions 用サービスプリンシパルの作成
5. JWT キー・SQL パスワードの自動生成
6. 設定すべき GitHub Secrets の一覧を出力（`infra/scripts/.env.setup` にも保存）

### GitHub Secrets の設定

`setup.sh` の出力をもとに、リポジトリの `Settings > Secrets and variables > Actions` へ以下を登録します。

| Secret 名 | 説明 |
|-----------|------|
| `AZURE_CREDENTIALS` | サービスプリンシパル JSON |
| `AZURE_SUBSCRIPTION_ID` | Azure サブスクリプション ID |
| `DEV_JWT_KEY` | Dev 環境の JWT 署名キー |
| `PROD_JWT_KEY` | Prod 環境の JWT 署名キー |
| `DEV_SQL_ADMIN_PASSWORD` | Dev 環境の SQL 管理者パスワード |
| `PROD_SQL_ADMIN_PASSWORD` | Prod 環境の SQL 管理者パスワード |

### 自動デプロイ（GitHub Actions）

| ブランチ | デプロイ先 | ワークフロー |
|---------|----------|------------|
| `develop` | Dev 環境 | `.github/workflows/deploy-dev.yml` |
| `main` | Prod 環境 | `.github/workflows/deploy-prod.yml` |

ワークフローはテスト → ビルド → ARM デプロイ → ZIP デプロイの順で実行されます。テスト失敗時はデプロイが中断されます。

### 手動デプロイ

```bash
az login

# Dev 環境
./infra/scripts/deploy.sh dev

# Prod 環境
./infra/scripts/deploy.sh prod
```

---

## API エンドポイント

| メソッド | パス | 説明 |
|---------|------|------|
| POST/GET/PUT/DELETE | `/api/expenses` | 支出 CRUD |
| POST/GET/PUT/DELETE | `/api/categories` | カテゴリ CRUD |
| POST/GET/PUT/DELETE | `/api/subscriptions` | サブスクリプション CRUD |
| GET | `/api/reports/monthly` | 月別集計 |
| GET | `/api/reports/by-category` | カテゴリ別集計 |
| GET | `/api/dashboard/summary` | ダッシュボード集計 |
| POST | `/api/expenses/import` | CSV 一括取込 |
| GET | `/api/reports/monthly/pdf` | PDF レポートダウンロード |
| POST | `/api/auth/register` | ユーザー登録 |
| POST | `/api/auth/login` | ログイン |

詳細は [docs/api-spec-final.md](docs/api-spec-final.md) を参照してください。

---

## ドキュメント

| ファイル | 内容 |
|---------|------|
| [docs/SETUP.md](docs/SETUP.md) | ローカル開発環境セットアップ詳細 |
| [docs/DEPLOYMENT.md](docs/DEPLOYMENT.md) | オンプレ/Linux デプロイ手順 |
| [docs/AZURE_DEPLOYMENT.md](docs/AZURE_DEPLOYMENT.md) | Azure デプロイ詳細・トラブルシューティング |
| [docs/CODING_STANDARDS.md](docs/CODING_STANDARDS.md) | コーディング規約 |
| [docs/schema/er-diagram.md](docs/schema/er-diagram.md) | ER 図・テーブル定義 |
| [docs/api-spec-final.md](docs/api-spec-final.md) | API 仕様 |
