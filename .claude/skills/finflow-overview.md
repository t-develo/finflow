# FinFlow プロジェクト概要【FinFlow固有】

FinFlow固有のプロジェクト構成・技術スタック・SE責務・APIルート。
汎用的な設計原則は `/architecture` / `/backend-csharp` / `/frontend-webcomponents` を参照。

---

## 技術スタック

| レイヤー | 技術 |
|---------|------|
| バックエンド | C# (.NET 8), ASP.NET Core Web API, EF Core, SQL Server |
| フロントエンド | Vanilla JS (ES2020+), Web Components, Chart.js |
| テスト | xUnit + FluentAssertions |
| 認証 | ASP.NET Identity + JWT |
| CSV処理 | CsvHelper |
| PDF出力 | QuestPDF または iText7（Sprint 1で確定） |
| 通知メール | SMTP / SendGrid（dev環境はMailHogモックSMTP） |

---

## ソリューション構成

```
src/
├── FinFlow.Api/             # Controllers, Program.cs, Middleware
├── FinFlow.Domain/          # Entities, Interfaces（外部依存ゼロ）
└── FinFlow.Infrastructure/  # DbContext, Services, Migrations
src/frontend/               # Vanilla JS SPA（ビルド不要, ES Modules）
tests/
└── FinFlow.Tests/           # xUnit tests
docs/
└── agents/                  # 各エージェント指示書
```

---

## SE責務マトリクス

| 担当 | 担当領域 | 所有テーブル |
|------|---------|-------------|
| **SE-1** | Expense CRUD、Category CRUD、CSV取込、支出自動分類 | Expense, Category, ClassificationRule |
| **SE-2** | Subscription CRUD、月次/カテゴリ別集計、Dashboard API、通知（Sprint 2）、PDF（Sprint 2） | Subscription |
| **SE-3** | 全フロントエンド（SPA基盤・全画面・UX） | — |
| **PL** | Auth基盤、共通Middleware、OpenAPI仕様、CI/CD | User（ASP.NET Identity） |

### SE-2のExpense/Categoryテーブルとの関係

- SE-2はExpense/Categoryを**読み取り専用**で集計に使用する
- SE-2がスキーマ変更を必要とする場合 → **必ずPLを通してSE-1に依頼**
- SE-2とSE-1が直接スキーマ変更を調整することは禁止

---

## 主要APIルート

| エンドポイント | 担当 | 説明 |
|--------------|------|------|
| `POST /api/auth/login` | PL | 認証 |
| `POST /api/auth/register` | PL | ユーザー登録 |
| `GET/POST/PUT/DELETE /api/expenses` | SE-1 | 支出CRUD |
| `GET/POST/PUT/DELETE /api/categories` | SE-1 | カテゴリCRUD |
| `POST /api/expenses/import` | SE-1 | CSV一括取込 |
| `GET/POST/PUT/DELETE /api/subscriptions` | SE-2 | サブスクリプションCRUD |
| `GET /api/reports/monthly` | SE-2 | 月次集計 |
| `GET /api/reports/by-category` | SE-2 | カテゴリ別集計 |
| `GET /api/dashboard/summary` | SE-2 | ダッシュボード集計 |
| `GET /api/reports/monthly/pdf` | SE-2 | PDFレポート出力 |

---

## FinFlow固有の設計パターン

### CSVパーサー（アダプタパターン）

```
ICsvParser (Domain/Interfaces/)
├── GenericCsvParser       ← Sprint 1（汎用形式）
├── MufgCsvParser          ← Sprint 2
└── RakutenCsvParser       ← Sprint 2

CsvParserFactory → CSV1行目（ヘッダー）から自動選択
```

新銀行フォーマット追加 = 新クラス追加のみ（OCP）。既存コード変更不要。

### フロントエンド構成

```
src/frontend/
├── index.html
├── js/
│   ├── app.js              # エントリーポイント
│   ├── router.js           # ルーティング
│   ├── pages/              # 各画面
│   ├── components/         # ff-プレフィックスのWeb Components
│   ├── utils/
│   │   ├── api-client.js   # バックエンドとの唯一の境界
│   │   └── auth.js         # JWT管理（localStorage）
│   └── mocks/              # Sprint 1で使用するモックAPI
└── css/
```

### Sprint別のAPI切り替え

- **Sprint 1:** `js/mocks/` のモックAPIを使用
- **Sprint 2:** `api-client.js` のベースURLを変更するだけで実APIに切り替わる（各画面のコード変更不要）

---

## ビルド & 実行コマンド

```bash
dotnet build
dotnet run --project src/FinFlow.Api
dotnet test
dotnet test --filter "ClassName=ExpensesControllerTests"

# EF Coreマイグレーション
dotnet ef migrations add <MigrationName> --project src/FinFlow.Infrastructure --startup-project src/FinFlow.Api
dotnet ef database update --project src/FinFlow.Infrastructure --startup-project src/FinFlow.Api
```
