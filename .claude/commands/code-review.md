# /code-review — Code Review

変更したコードをセキュリティ・品質・保守性の観点でレビューする。

## 実行手順

```
1. git diff --name-only HEAD で変更ファイルを特定
2. 各ファイルを以下の優先順位でレビュー
3. 問題を重要度別に報告
4. CRITICAL/HIGH があればコミット前に修正
```

## レビュー優先順位

### 1. セキュリティ（CRITICAL）
- [ ] UserId によるデータ分離が実装されている
- [ ] JWT認証が保護エンドポイントに適用されている
- [ ] SQLインジェクションリスクがない（EF Coreのパラメータ化）
- [ ] XSS対策（フロントエンドで `innerHTML` に直接代入していない）
- [ ] CSVインジェクション防止（エクスポート時のエスケープ）
- [ ] ハードコードされたシークレットがない

### 2. 正しさ（HIGH）
- [ ] 金額フィールドに `decimal` を使用（`float`/`double` は NG）
- [ ] 非同期メソッドに `Async` サフィックスがある
- [ ] エラーハンドリングが適切
- [ ] null参照の可能性がない

### 3. テスト（HIGH）
- [ ] 新しいコードにテストが存在する
- [ ] UserId分離のテストがある
- [ ] エッジケースがカバーされている

### 4. 設計（MEDIUM）
- [ ] レイヤーの責務が正しい（Controller/Service/Repository）
- [ ] インターフェースを通じた依存
- [ ] 適切な例外クラスを使用

### 5. 可読性（LOW）
- [ ] 命名規則に従っている（PascalCase/camelCase）
- [ ] 関数が50行以内
- [ ] ファイルが800行以内

## FinFlow固有チェック

- **SE境界の遵守**: SE-1はExpense/Category, SE-2はSubscription/Reports を担当
- **decimal強制**: `Amount`, `Price`, `Total` などのフィールドは必ず `decimal`
- **全エンドポイントに `[Authorize]`**: 認証不要エンドポイントのみ `[AllowAnonymous]`

## 判定

- **APPROVE**: CRITICAL/HIGH なし、実装品質が良好
- **REQUEST_CHANGES**: CRITICAL または HIGH の問題あり → 修正してから再レビュー
