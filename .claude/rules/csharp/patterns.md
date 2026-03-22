# C# Design Patterns — FinFlow

## レイヤー間の依存関係

```
FinFlow.Api (Controllers)
  ↓ depends on
FinFlow.Domain (Interfaces, Entities)
  ↑ implements
FinFlow.Infrastructure (Services, Repositories, DbContext)
```

ドメイン層はインフラ層に依存しない。

## EF Core のパターン

**UserId フィルタリング（必須）:**
```csharp
// グローバルクエリフィルターでデフォルト適用を検討
modelBuilder.Entity<Expense>().HasQueryFilter(e => e.UserId == _currentUserId);

// または各クエリで明示的に適用
.Where(e => e.UserId == userId)
```

**非同期LINQ:**
```csharp
// OK
var expenses = await _context.Expenses
    .Where(e => e.UserId == userId && e.Date >= startDate)
    .Include(e => e.Category)
    .OrderByDescending(e => e.Date)
    .ToListAsync();
```

## ファクトリーパターン（CSVパーサー）

```csharp
public class CsvParserFactory
{
    private readonly IEnumerable<ICsvParser> _parsers;

    public ICsvParser GetParser(string headerLine)
    {
        return _parsers.FirstOrDefault(p => p.CanParse(headerLine))
            ?? throw new UnsupportedCsvFormatException(headerLine);
    }
}
```

## コントローラーのスリム化

```csharp
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ExpensesController : ControllerBase
{
    private readonly IExpenseService _expenseService;

    // JWT からユーザーIDを取得
    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    [HttpGet]
    public async Task<ActionResult<IEnumerable<ExpenseDto>>> GetExpenses()
        => Ok(await _expenseService.GetExpensesAsync(UserId));

    [HttpPost]
    public async Task<ActionResult<ExpenseDto>> CreateExpense(CreateExpenseRequest request)
    {
        var expense = await _expenseService.CreateExpenseAsync(UserId, request);
        return CreatedAtAction(nameof(GetExpense), new { id = expense.Id }, expense);
    }
}
```

## バリデーション

Data Annotations と FluentValidation を組み合わせる:

```csharp
public record CreateExpenseRequest(
    [Required] [Range(0.01, 10_000_000)] decimal Amount,
    [Required] [MaxLength(500)] string Description,
    [Required] DateTime Date,
    int? CategoryId
);
```
