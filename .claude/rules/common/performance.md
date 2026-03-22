# Performance — FinFlow

## Claude モデル選択指針

| モデル | 用途 |
|--------|------|
| **Haiku 4.5** | 軽量タスク（コメント追加、単純な変換）、Worker役割 |
| **Sonnet 4.6** | メイン開発作業、コード実装、オーケストレーション |
| **Opus 4.6** | アーキテクチャ判断、深い推論が必要な設計作業 |

## コンテキストウィンドウ管理

コンテキストウィンドウの残り**1/5を切ったら**、以下の作業は避ける:
- 大規模なリファクタリング
- 複数ファイルにまたがる機能実装
- 複雑なデバッグ

代わりに `SESSION_STATE.md` を更新して次のセッションに引き継ぐ。

## バックエンドのパフォーマンス原則

**N+1クエリ防止:**
```csharp
// NG: ループ内でDBアクセス
foreach (var expense in expenses)
    expense.Category = await _context.Categories.FindAsync(expense.CategoryId);

// OK: Include で事前ロード
var expenses = await _context.Expenses
    .Include(e => e.Category)
    .Where(e => e.UserId == userId)
    .ToListAsync();
```

**ページネーション:**
大量データ取得時は必ずページネーションを実装（最大100件/リクエスト目安）。

## CSV取込のパフォーマンス

- 最大10,000行をサポート
- エラー行はスキップ（処理中断しない）
- BulkInsert パターンを検討（EF Core の `AddRangeAsync`）

## ビルドエラー対応

ビルドが失敗したら `/build-fix` コマンドを使用。一度に1エラーずつ修正する。
