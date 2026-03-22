# Development Workflow — FinFlow

## 実装前の必須ステップ（Research & Reuse）

コードを書く前に:
1. **既存コードを確認** — 同様の実装がすでにないか検索
2. **公式ドキュメント参照** — .NET 8, EF Core, ASP.NET Core の公式ドキュメント
3. **パッケージレジストリ確認** — NuGet で既存ライブラリを確認（自前実装の前に）

## 4つの開発フェーズ

### 1. Planning
複雑な機能・複数ファイルに影響する変更は `/plan` コマンドで計画を立ててから実装。

### 2. TDD（テスト駆動開発）
`/tdd` コマンドを使い RED-GREEN-REFACTOR サイクルで実装。
カバレッジ目標: 80%+（認証・金額計算は100%）

### 3. Code Review
実装後は `/code-review` コマンドで自己レビュー。セキュリティ・品質問題を解消してからコミット。

### 4. Git
conventional commit形式でコミット → PR作成。[git-workflow.md](./git-workflow.md) を参照。

## スプリント構成

| スプリント | 内容 |
|-----------|------|
| Sprint 1 | 基盤構築（認証、Expense/Category CRUD、フロントエンド基盤） |
| Sprint 2 | 応用機能（CSV取込、集計レポート、通知、PDF出力） |

## エージェントの活用

| 状況 | 使うエージェント |
|------|----------------|
| 複雑な機能の設計 | planner |
| コード実装後のレビュー | code-reviewer |
| バグ修正・新機能実装 | tdd-guide |
| アーキテクチャ判断 | architect |
| ビルドエラー | build-error-resolver |
