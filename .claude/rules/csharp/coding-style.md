# C# Coding Style — FinFlow

## 命名規則

```csharp
// クラス・インターフェース・メソッド: PascalCase
public class ExpenseService : IExpenseService { }
public interface ICsvParser { }
public async Task<Expense> GetExpenseByIdAsync(int id) { }

// プライベートフィールド: camelCase
private readonly IExpenseRepository _expenseRepository;
private readonly ILogger<ExpenseService> _logger;

// ローカル変数・パラメータ: camelCase
var expenseDto = new ExpenseDto();
decimal totalAmount = 0m;
```

## 必須規則

**金額フィールドは必ず `decimal`:**
```csharp
// NG
public float Amount { get; set; }
public double Amount { get; set; }

// OK
public decimal Amount { get; set; }
```

**非同期メソッドには `Async` サフィックス:**
```csharp
// NG
public Task<List<Expense>> GetExpenses()

// OK
public Task<List<Expense>> GetExpensesAsync()
```

**`var` の使用は型が明確な場合のみ:**
```csharp
// OK: 右辺から型が分かる
var expense = new Expense();
var expenses = new List<Expense>();

// NG: 型が不明確
var result = await _service.ProcessAsync(data);
```

## DTOパターン

エンティティをAPIレスポンスに直接使わず、DTOに変換する:

```csharp
public record ExpenseDto(
    int Id,
    decimal Amount,
    string Description,
    DateTime Date,
    string CategoryName
);
```

## DIコンテナへの登録

```csharp
// Program.cs でスコープを明示
builder.Services.AddScoped<IExpenseService, ExpenseService>();
builder.Services.AddScoped<ICsvParser, GenericCsvParser>();
```

## エラーハンドリング

グローバルミドルウェアが統一処理するため、Controllerでは try-catch を多用しない。
独自の例外クラスを投げ、ミドルウェアがHTTPステータスに変換する:

```csharp
// NG: Controller内で全部処理
try { ... } catch (Exception ex) { return StatusCode(500, ex.Message); }

// OK: 例外を投げてミドルウェアに任せる
throw new NotFoundException($"Expense {id} not found");
```
