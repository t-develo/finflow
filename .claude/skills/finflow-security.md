# FinFlow セキュリティ必須チェック【全ロール共通】

FinFlow固有のセキュリティ要件と、実装・レビュー時の確認ポイント。

---

## 最重要セキュリティ要件（4点）

### 1. UserId分離（SE-1/SE-2 必須）

**全てのデータアクセスクエリに `UserId` フィルタを含める。**
これが抜けると、他ユーザーのデータが見えるセキュリティインシデントになる。

```csharp
// BAD: UserIdフィルタなし（他ユーザーのデータが取れてしまう）
var expenses = await _db.Expenses.ToListAsync();

// GOOD: 必ずUserIdでフィルタ
var expenses = await _db.Expenses
    .Where(e => e.UserId == userId)
    .ToListAsync();
```

**JWT から UserId を取得する方法:**
```csharp
var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
```

### 2. decimal型による金額計算（SE-2 特に重要）

**金額計算は必ず `decimal` 型を使用する。`float`/`double` は禁止。**

```csharp
// BAD: float/doubleは丸め誤差が発生する
double total = expenses.Sum(e => (double)e.Amount); // 誤差が蓄積

// GOOD: decimalは10進数を正確に表現できる
decimal total = expenses.Sum(e => e.Amount); // 正確

// パーセンテージの丸め
decimal percentage = Math.Round(value, 1, MidpointRounding.AwayFromZero);

// ゼロ除算防止
decimal average = count > 0 ? total / count : 0m;
```

### 3. CSVインジェクション防止（SE-1 必須）

**CSVセル値が表計算ソフトの数式として実行される脆弱性。**

```csharp
// セル値の先頭が数式トリガー文字の場合は検出・サニタイズ
private static readonly char[] CsvInjectionChars = { '=', '+', '-', '@', '\t', '\r' };

private string SanitizeCsvCell(string value)
{
    if (string.IsNullOrEmpty(value)) return value;
    if (CsvInjectionChars.Contains(value[0]))
    {
        // プレフィックスを付けて数式として認識されないようにする
        return "'" + value;
    }
    return value;
}
```

### 4. XSS防止（SE-3 必須）

**`innerHTML` にユーザー入力を直接埋め込まない。**

```javascript
// BAD: XSS脆弱性
element.innerHTML = `<span>${userInput}</span>`;

// GOOD: 必ずエスケープ
element.innerHTML = `<span>${this.escapeHtml(userInput)}</span>`;

// エスケープ関数（全コンポーネントで共通使用）
escapeHtml(text) {
    const div = document.createElement('div');
    div.textContent = text;
    return div.innerHTML;
}
```

---

## その他のセキュリティ要件

### バックエンド共通

| 項目 | 内容 |
|------|------|
| SQLインジェクション | EF CoreのLINQを使用。生SQLは原則禁止 |
| 入力バリデーション | サーバーサイドバリデーション必須（クライアントバリデーションは信頼しない） |
| エラーメッセージ | スタックトレースをAPIレスポンスに含めない |
| ファイルアップロード | CSVファイルは10MB以下に制限 |
| CSV行数制限 | 最大10,000行（それ以上はエラー） |
| 機密情報のログ出力 | パスワード・トークンをログやエラーメッセージに含めない |
| EF Core | 読み取り専用クエリには `AsNoTracking()` を使用 |

### フロントエンド固有

| 項目 | 内容 |
|------|------|
| JWT保管 | localStorage使用（HttpOnlyではない制約を理解した上で使用） |
| eval()禁止 | いかなる場合も使用しない |
| 二重送信防止 | 送信中はボタンを無効化 |
| PDF出力 | ユーザー入力値を含む場合はインジェクション対策 |
| メール送信 | ヘッダーインジェクション防止（SE-2 通知機能） |

---

## レビュー時のセキュリティチェック（優先度 Must Fix）

```
□ 全Expense/Category/Subscriptionクエリにusername/userIdフィルタがあるか
□ 金額フィールドがdecimal型か（floatを使っていないか）
□ CSVセル値のサニタイズロジックがあるか
□ innerHTMLに未エスケープのユーザー入力が埋め込まれていないか
□ ユーザーが他人のリソースIDを指定してアクセスできる抜け穴がないか
□ 500エラーのレスポンスにスタックトレースが含まれていないか
```
