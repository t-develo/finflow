# Security Guidelines — FinFlow

## コミット前の必須チェック（8項目）

- [ ] 認証情報・APIキー・シークレットのハードコードなし
- [ ] SQLインジェクション対策（EF Core パラメータ化クエリを使用）
- [ ] XSS対策（フロントエンドで入力をエスケープ）
- [ ] CSVインジェクション防止（`=`, `+`, `-`, `@` で始まるセルをエスケープ）
- [ ] JWT検証が全保護エンドポイントに適用されている
- [ ] ユーザーIDによるデータ分離（他ユーザーのデータにアクセス不可）
- [ ] エラーメッセージに機密情報を含まない
- [ ] レート制限が適用されている

## FinFlow固有の重点事項

**UserId分離（最重要）:**
```csharp
// NG: 全ユーザーのデータを返す
var expenses = await _context.Expenses.ToListAsync();

// OK: 認証済みユーザーのデータのみ返す
var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
var expenses = await _context.Expenses
    .Where(e => e.UserId == userId)
    .ToListAsync();
```

**CSVインジェクション防止:**
```csharp
// CSVエクスポート時、先頭文字を確認してエスケープ
private static string EscapeCsvField(string field)
{
    if (field.StartsWith("=") || field.StartsWith("+") ||
        field.StartsWith("-") || field.StartsWith("@"))
        return $"'{field}";
    return field;
}
```

**XSS防止（フロントエンド）:**
```javascript
// NG: innerHTML への直接代入
element.innerHTML = userInput;

// OK: textContent を使用
element.textContent = userInput;
// または DOMPurify でサニタイズ
element.innerHTML = DOMPurify.sanitize(userInput);
```

## セキュリティ問題発見時の対応

1. 作業を即停止
2. security-reviewer エージェントを呼び出す
3. 重大度の高い問題を優先修正
4. 露出したシークレットがあれば即時ローテーション
5. 類似パターンのコードベース全体調査
