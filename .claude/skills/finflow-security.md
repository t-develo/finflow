# FinFlow セキュリティ必須チェック【FinFlow固有】

FinFlowプロジェクト固有のセキュリティ要件。
Web全般のセキュリティ原則は `/security-web` を参照。

---

## FinFlow固有の4大セキュリティ要件

### 1. UserId分離（SE-1 / SE-2 必須）

全Expense・Category・Subscription クエリに必ず `UserId` フィルタを含める。

```csharp
// JWTからUserIdを取得する
var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

// 全クエリでフィルタ必須
var expenses = await _db.Expenses
    .Where(e => e.UserId == userId)  // ← これが抜けると他ユーザーのデータが見える
    .ToListAsync();

// IDを指定するAPI（GET /expenses/{id}等）は取得後に所有者チェック必須
var expense = await _db.Expenses.FindAsync(id);
if (expense == null || expense.UserId != userId)
    throw new NotFoundException();
```

### 2. decimal型による金額計算（SE-2 特に重要）

FinFlowでは全ての金額を `decimal` 型で扱う。`float`/`double` は禁止。

```csharp
// BAD: 丸め誤差が家計集計に影響する
double total = expenses.Sum(e => (double)e.Amount);

// GOOD
decimal total = expenses.Sum(e => e.Amount);
decimal pct = Math.Round(value, 1, MidpointRounding.AwayFromZero);
decimal avg = count > 0 ? total / count : 0m;
int daysInMonth = DateTime.DaysInMonth(year, month); // 月末日はこれで計算
```

### 3. CSVインジェクション防止（SE-1 必須）

CSVインポート機能で、セル値が表計算ソフトの数式として実行される脆弱性。

```csharp
// セル値の先頭が数式トリガー文字の場合はサニタイズ
private static readonly char[] CsvInjectionChars = { '=', '+', '-', '@', '\t', '\r' };

private string SanitizeCsvCell(string value)
{
    if (string.IsNullOrEmpty(value)) return value;
    return CsvInjectionChars.Contains(value[0]) ? "'" + value : value;
}
```

### 4. XSS防止（SE-3 必須）

フロントエンドで全てのユーザー入力を `escapeHtml()` でサニタイズしてからレンダリングする。

```javascript
// 支出の説明・メモ等のユーザー入力は必ずエスケープ
this.shadowRoot.innerHTML = `<span>${this.escapeHtml(expense.description)}</span>`;

// BAD: ダイレクトに埋め込む
this.shadowRoot.innerHTML = `<span>${expense.description}</span>`;
```

---

## FinFlow固有の追加制約

| 項目 | 内容 |
|------|------|
| CSVファイルサイズ | 10MB以下に制限 |
| CSV行数制限 | 最大10,000行（超過はエラー） |
| JWT保管 | localStorageを使用（HttpOnlyではない制約を理解した上で採用） |
| メール送信 | ヘッダーインジェクション防止（SE-2の通知機能） |

---

## レビュー時のFinFlow固有セキュリティチェック（Must Fix判定）

```
□ Expense/Category/Subscriptionの全クエリにUserIdフィルタがあるか
□ IDを指定するAPI（GET /expenses/{id}等）で所有者チェックをしているか
□ 金額フィールドがdecimal型か（float/doubleは即Must Fix）
□ CSVセル値のサニタイズロジックがSE-1の実装に含まれているか
□ SE-3のinnerHTMLに未エスケープのユーザー入力が埋め込まれていないか
□ Sprint 2の実APIへの切り替えでapi-client.js以外を変更していないか
```
