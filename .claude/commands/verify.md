# /verify — Pre-commit Verification

コミット・PR前の総合チェックを行う。

## 実行順序（この順番で実行）

```bash
# 1. ビルド確認
dotnet build

# 2. テスト実行
dotnet test

# 3. 未コミットファイルの確認
git status
git diff --stat

# 4. コンソールログのチェック（フロントエンド）
grep -rn "console\.log" src/frontend/js/ --include="*.js"

# 5. シークレットチェック
grep -rn "password\|secret\|api.key\|token" src/ --include="*.cs" -i | grep -v "//.*password\|test\|config\|appsettings"
```

## レポート形式

```
=== FinFlow Verify ===
Build:   [PASS / FAIL]
Tests:   [PASS: XX/XX / FAIL: XX errors]
Lint:    [OK / X warnings]
Secrets: [OK / FOUND: X items]
Logs:    [OK / X console.log found]

Status: [READY FOR COMMIT / NEEDS FIXES]
```

## 実行モード

| モード | 内容 |
|--------|------|
| **quick** | ビルドのみ（`dotnet build`） |
| **full**（デフォルト） | 全チェック |
| **pre-commit** | ビルド + テスト + シークレット |
| **pre-pr** | 全チェック + コードレビュー |

## チェックが失敗した場合

- **Build FAIL** → `/build-fix` を実行
- **Tests FAIL** → 失敗したテストを修正（テスト自体は変更しない）
- **Secrets FOUND** → 即座にシークレットを削除して環境変数に移行
- **console.log** → デバッグ用ログを削除
