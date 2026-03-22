# C# Hooks & Build Automation — FinFlow

## ビルド確認コマンド

```bash
# フルビルド
dotnet build

# テスト実行
dotnet test

# 特定テストクラス
dotnet test --filter "ClassName=ExpensesControllerTests"

# ビルド成果物クリーン
dotnet clean && dotnet build
```

## マイグレーション管理

```bash
# 新しいマイグレーション作成
dotnet ef migrations add <MigrationName> \
  --project src/FinFlow.Infrastructure \
  --startup-project src/FinFlow.Api

# マイグレーション適用
dotnet ef database update \
  --project src/FinFlow.Infrastructure \
  --startup-project src/FinFlow.Api

# マイグレーション一覧
dotnet ef migrations list \
  --project src/FinFlow.Infrastructure \
  --startup-project src/FinFlow.Api
```

## コミット前チェックリスト

- [ ] `dotnet build` が成功する
- [ ] `dotnet test` が全て通る
- [ ] 新しいコードにテストが書かれている
- [ ] `decimal` を使用している（`float`/`double` なし）
- [ ] UserId フィルタリングが適用されている
- [ ] エンドポイントに `[Authorize]` が付いている

## よくあるビルドエラーへの対処

| エラー | 対処 |
|--------|------|
| `CS0246: 型または名前空間が見つかりません` | using 文の追加 or NuGet パッケージ確認 |
| `EF Core migration pending` | `dotnet ef database update` を実行 |
| `Nullable reference type` 警告 | null チェックまたは `!` オペレーターを追加 |
| DI 登録エラー | `Program.cs` の `AddScoped`/`AddSingleton` 確認 |

## PostToolUse での自動チェック（参考）

実装ファイル保存後に以下を確認することを推奨:
1. `dotnet build` でコンパイルエラーなし
2. 変更に対応するテストが存在する
3. `decimal` 型の使用（金額フィールド）
