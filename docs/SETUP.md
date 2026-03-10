# FinFlow 開発環境セットアップ手順

## 必要なツール

| ツール | バージョン | 用途 |
|--------|-----------|------|
| .NET SDK | 8.0+ | バックエンド開発 |
| SQL Server | 2019+ / LocalDB | データベース |
| dotnet-ef | 8.0.0 | EF Coreマイグレーション |
| Node.js | 不要 | フロントエンドはビルドツール不使用 |
| MailHog | 最新 | 開発用モックSMTPサーバー |

## セットアップ手順

### 1. リポジトリのクローン

```bash
git clone <repository-url>
cd finflow
```

### 2. dotnet-ef のインストール

```bash
dotnet tool install --global dotnet-ef --version 8.0.0
```

### 3. データベースのセットアップ

SQL Server または LocalDB が起動していること確認後：

```bash
dotnet ef database update --project src/FinFlow.Infrastructure --startup-project src/FinFlow.Api
```

### 4. APIサーバーの起動

```bash
dotnet run --project src/FinFlow.Api
```

デフォルトURL: `http://localhost:5000`
Swagger UI: `http://localhost:5000/swagger`

### 5. フロントエンドの確認

フロントエンドはビルド不要。APIサーバーが静的ファイルを配信します。
または `src/frontend/index.html` を直接ブラウザで開いてもOK。

### 6. MailHog（メール通知開発用）

```bash
# Docker使用の場合
docker run -d -p 1025:1025 -p 8025:8025 mailhog/mailhog

# MailHog Web UI: http://localhost:8025
```

## 環境変数（開発時のみ）

`appsettings.Development.json` で上書き可能：

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

## よく使うコマンド

```bash
# ビルド
dotnet build

# テスト実行
dotnet test

# 特定テストクラスのみ
dotnet test --filter "ClassName=ExpensesControllerTests"

# マイグレーション追加
dotnet ef migrations add <MigrationName> \
  --project src/FinFlow.Infrastructure \
  --startup-project src/FinFlow.Api

# DB更新
dotnet ef database update \
  --project src/FinFlow.Infrastructure \
  --startup-project src/FinFlow.Api
```

## Git ブランチ戦略

```
main          ← リリースブランチ（直接push禁止）
develop       ← 統合ブランチ
feature/SE-A/ ← SE-A作業ブランチ
feature/SE-B/ ← SE-B作業ブランチ
feature/SE-C/ ← SE-C作業ブランチ
```

コミットメッセージ形式：
```
[SE-A] S1-A-001: 支出CRUD API実装
[SE-B] S1-B-001: サブスクリプションCRUD実装
[PL]   S0-PL-005: 共通基盤実装
```
