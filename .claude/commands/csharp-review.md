# /csharp-review — C# Code Review

C# (.NET 8) 固有の観点で変更ファイルをレビューする。

## 実行手順

```bash
# 変更された .cs ファイルを特定
git diff --name-only HEAD | grep "\.cs$"

# ビルド警告の確認
dotnet build --verbosity normal 2>&1 | grep -E "warning|error"
```

## CRITICAL チェック（ブロッカー）

- [ ] **UserId分離**: 全クエリに `.Where(e => e.UserId == userId)` がある
- [ ] **decimal型**: `Amount`, `Price`, `Total` フィールドに `decimal` を使用
- [ ] **認証**: 全コントローラーに `[Authorize]` がある
- [ ] **SQLインジェクション**: EF Coreのパラメータ化クエリのみ使用
- [ ] **シークレット非露出**: APIキー・パスワードをコードに書いていない

## HIGH チェック

- [ ] **Asyncサフィックス**: 非同期メソッドに `Async` が付いている
- [ ] **null安全**: null参照の可能性がある箇所に対処している
- [ ] **例外処理**: 適切な例外クラスを使用（`NotFoundException`, `ValidationException` 等）
- [ ] **DI**: コンストラクターインジェクションを使用している

## MEDIUM チェック

- [ ] **メソッド長**: 50行以内
- [ ] **ファイル長**: 800行以内
- [ ] **async/await**: `Task.Result` や `.Wait()` を使っていない（デッドロックリスク）
- [ ] **EF Core**: N+1クエリが発生していない（必要なら `.Include()` を使用）

## FinFlow固有チェック

```csharp
// NG: 型変換漏れ
public float Amount { get; set; }

// OK
public decimal Amount { get; set; }

// NG: UserId なしのクエリ
var all = await _context.Expenses.ToListAsync();

// OK
var mine = await _context.Expenses
    .Where(e => e.UserId == currentUserId)
    .ToListAsync();
```

## 判定

- **APPROVE**: CRITICAL/HIGH なし
- **REQUEST_CHANGES**: CRITICAL または HIGH あり
