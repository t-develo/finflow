# Git Workflow — FinFlow

## コミットメッセージ形式

```
<type>: <概要>

<本文（任意）>
```

使用可能なtype: `feat`, `fix`, `refactor`, `docs`, `test`, `chore`, `perf`, `ci`

例:
```
feat: Expense CRUD エンドポイント実装
fix: CSV取込時のShift_JIS文字化けを修正
test: ExpensesControllerTests にバリデーションテストを追加
```

## PRの作成手順

1. コミット履歴全体を確認（最新コミットだけでなく全コミット）
2. `git diff [base-branch]...HEAD` で変更全体を把握
3. PR概要を作成（変更内容・理由・テスト計画）
4. `git push -u origin <branch-name>` でプッシュ

## ブランチ命名規則

- `feature/<機能名>` — 新機能
- `fix/<バグ内容>` — バグ修正
- `claude/<説明>` — Claude Codeによる自動作業ブランチ

## 注意事項

- `--no-verify` は使用禁止（pre-commitフックをスキップしない）
- `git push --force` は main/master への使用禁止
- 機密情報（.env, credentials）はコミットしない
