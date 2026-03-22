# C# Security — FinFlow

## JWT認証

```csharp
// 全コントローラーに [Authorize] を適用
[ApiController]
[Authorize]
public class ExpensesController : ControllerBase { }

// 認証不要のエンドポイントには明示的に [AllowAnonymous]
[HttpPost("login")]
[AllowAnonymous]
public async Task<IActionResult> Login(LoginRequest request) { }
```

## UserId の取得（必須パターン）

```csharp
// NG: ヘッダーやボディからユーザーIDを受け取る
public async Task<IActionResult> GetExpenses([FromQuery] string userId)

// OK: JWT クレームから取得
private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;
```

## SQLインジェクション防止

EF Core のパラメータ化クエリを使用する。生SQLは必要な場合のみ:

```csharp
// NG: 文字列連結でSQL構築
var expenses = await _context.Expenses
    .FromSqlRaw($"SELECT * FROM Expenses WHERE UserId = '{userId}'")
    .ToListAsync();

// OK: EF Core LINQ（自動でパラメータ化）
var expenses = await _context.Expenses
    .Where(e => e.UserId == userId)
    .ToListAsync();

// OK: 生SQL使用時はパラメータ化
var expenses = await _context.Expenses
    .FromSqlRaw("SELECT * FROM Expenses WHERE UserId = {0}", userId)
    .ToListAsync();
```

## シークレット管理

```csharp
// NG: appsettings.json にシークレットをハードコード
"JwtSettings": { "SecretKey": "my-super-secret-key-12345" }

// OK: 環境変数または Azure Key Vault
builder.Configuration.AddEnvironmentVariables();
// JwtSettings:SecretKey 環境変数から読み込む
```

## CSVインジェクション防止

```csharp
private static string SanitizeCsvField(string? value)
{
    if (string.IsNullOrEmpty(value)) return string.Empty;
    // スプレッドシートのフォーミュラ注入を防ぐ
    if (value[0] is '=' or '+' or '-' or '@' or '\t' or '\r')
        return $"'{value}";
    return value;
}
```

## パスワードハッシュ

ASP.NET Identity の組み込みハッシュ機能を使用（BCrypt相当）。独自実装禁止。

## レート制限

ログイン・登録エンドポイントにはレート制限を適用:
```csharp
builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("auth", o =>
    {
        o.PermitLimit = 5;
        o.Window = TimeSpan.FromMinutes(1);
    });
});
```
