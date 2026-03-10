# FinFlow バックエンドSE実装ガイド【SE-1 / SE-2 共通】

C#/.NET 8バックエンド開発者（SE-1/SE-2）が意識すべき実装規約と注意点。

---

## コーディング規約

### 命名規則

```csharp
// PascalCase: 公開メンバー、プロパティ、メソッド、クラス名
public class ExpenseService { }
public decimal TotalAmount { get; set; }
public async Task<Expense> GetExpenseAsync(int id) { }

// camelCase: プライベートフィールド（_プレフィックス）
private readonly FinFlowDbContext _db;
private readonly ILogger<ExpenseService> _logger;

// 非同期メソッドには必ずAsyncサフィックス
GetExpensesAsync()          // OK
GetMonthlySummaryAsync()    // OK
GetExpenses()               // NG（asyncなのにサフィックスなし）

// インターフェースはIプレフィックス
ICsvParser, IExpenseService, IPdfReportGenerator

// 意図を正確に表す名前
GetExpensesByUserAndMonthAsync()    // 文脈が必要なら長くてもOK
data / temp / result                 // 使用禁止（汎用的すぎる）
```

### 命名の原則

```csharp
// 説明変数で複雑な条件式に名前を付ける
// BAD
if (expense.Date >= startDate && expense.Date <= endDate && expense.UserId == userId && !expense.IsDeleted)

// GOOD
var isWithinDateRange = expense.Date >= startDate && expense.Date <= endDate;
var belongsToUser = expense.UserId == userId;
var isActive = !expense.IsDeleted;
if (isWithinDateRange && belongsToUser && isActive)

// マジックナンバー禁止
// BAD
if (rows.Count > 10000) throw new ...
// GOOD
private const int MaxCsvRows = 10_000;
if (rows.Count > MaxCsvRows) throw new ...
```

---

## アーキテクチャ規約

### Controllerは薄く

```csharp
// BAD: Controllerにビジネスロジック
[HttpPost]
public async Task<IActionResult> Create(CreateExpenseDto dto)
{
    var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    // ここにバリデーション・DB操作・ビジネスロジック...
}

// GOOD: ServiceにISTを委譲
[HttpPost]
public async Task<IActionResult> Create(CreateExpenseDto dto)
{
    var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    var result = await _service.CreateExpenseAsync(userId, dto);
    return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
}
```

### エラーハンドリング

```csharp
// Controllerでのtry-catchは禁止（グローバルミドルウェアに委譲）
// BAD
try { ... } catch (Exception ex) { return StatusCode(500, ex.Message); }

// GOOD: カスタム例外をスロー → ミドルウェアが処理
throw new NotFoundException($"Expense {id} not found for user {userId}");
throw new ValidationException("Amount must be greater than 0");
```

### バリデーション

```csharp
// DTOレベル: DataAnnotations
public class CreateExpenseDto
{
    [Required]
    [StringLength(200)]
    public string Description { get; set; }

    [Range(0.01, 9_999_999.99)]
    public decimal Amount { get; set; }
}

// ビジネスルール: サービス層で実施
// エンティティの不変条件: エンティティ自身で保護
```

---

## EF Core の注意点

```csharp
// N+1問題を避ける
// BAD: ループ内でクエリが発生
foreach (var sub in subscriptions)
    sub.Category = await _db.Categories.FindAsync(sub.CategoryId); // N+1!

// GOOD: Includeで一括取得
var subscriptions = await _db.Subscriptions
    .Include(s => s.Category)
    .Where(s => s.UserId == userId)
    .ToListAsync();

// 読み取り専用クエリはAsNoTrackingを使用（SE-2の集計クエリ）
var expenses = await _db.Expenses
    .AsNoTracking()
    .Where(e => e.UserId == userId && e.Date.Month == month)
    .ToListAsync();

// async/awaitを正しく使う（Task.Result / .Wait() は禁止）
// BAD
var expense = _service.GetExpenseAsync(id).Result; // デッドロック!
// GOOD
var expense = await _service.GetExpenseAsync(id);
```

---

## SE-1 固有事項（Expense CRUD / CSV / 分類）

### CSV取込の注意点

```csharp
// エンコーディング対応（UTF-8 と Shift_JIS 両方）
var encoding = DetectEncoding(stream);

// エラー行はスキップ（致命的エラーにしない）
foreach (var row in rows)
{
    try { importedExpenses.Add(ParseRow(row)); }
    catch (CsvParseException ex)
    {
        _logger.LogWarning("Skipping row {Row}: {Reason}", row, ex.Message);
        skippedRows.Add(row.LineNumber);
    }
}

// CSVインジェクション防止
private static readonly char[] CsvInjectionChars = { '=', '+', '-', '@', '\t', '\r' };
if (CsvInjectionChars.Contains(value[0])) value = "'" + value;
```

### SE-2への影響を意識する

- `Expense` / `Category` テーブルのスキーマ変更は**必ずPLに相談**
- SE-2が読み取り専用で使うカラムを削除・リネームしない
- マイグレーション追加のタイミングはPLを通して調整

---

## SE-2 固有事項（集計 / レポート / 通知）

### 集計コードの品質基準

```csharp
// decimal型で金額計算
decimal total = expenses.Sum(e => e.Amount);        // OK
double total = expenses.Sum(e => (double)e.Amount); // NG

// パーセンテージの丸め（小数点第1位、四捨五入）
Math.Round(percentage, 1, MidpointRounding.AwayFromZero)

// ゼロ除算の防止
decimal average = count > 0 ? total / count : 0m;

// 月末日の計算
int daysInMonth = DateTime.DaysInMonth(year, month);
```

### SE-1との連携ルール

- Expense/CategoryテーブルはSE-1が管理。**読み取り専用**で使用する
- 集計に必要な変更（インデックス追加等）はPLを通してSE-1に依頼
- **SE-1に直接変更を依頼しない**（必ずPLを通す）

### ダッシュボードは既存サービスを組み合わせる

```csharp
// 新しい集計クエリを書くのではなく、既存サービスを合成する
public class DashboardService : IDashboardService
{
    private readonly IReportService _reportService;        // 月次集計
    private readonly ISubscriptionService _subscriptionService; // サブスク情報

    public async Task<DashboardSummaryDto> GetSummaryAsync(string userId)
    {
        var monthly = await _reportService.GetMonthlySummaryAsync(...);
        var upcoming = await _subscriptionService.GetUpcomingDueAsync(...);
        return new DashboardSummaryDto { ... };
    }
}
```

---

## 共通禁止事項

- テストを書かずにコードをコミットしない
- `float` / `double` で金額計算しない（`decimal` を使う）
- `var result = ...` のような意味のない変数名を使わない
- 例外を握り潰さない（`catch (Exception) { }` は禁止）
- TODO コメントを期限とチケットIDなしで放置しない
- PLに相談せずにAPI仕様を変更しない
- コントローラーにビジネスロジックを書かない
