# C#/.NET バックエンド実装ガイド【汎用】

C#/.NET 8 / ASP.NET Core Web API開発で適用すべき規約とパターン。

---

## 命名規則

```csharp
// PascalCase: 公開メンバー、プロパティ、メソッド、クラス名
public class UserService { }
public decimal TotalAmount { get; set; }
public async Task<User> GetUserAsync(int id) { }

// camelCase: プライベートフィールド（_プレフィックス）
private readonly AppDbContext _db;
private readonly ILogger<UserService> _logger;

// 非同期メソッドには必ずAsyncサフィックス
GetUsersAsync()          // OK
CreateExpenseAsync()     // OK
GetUsers()               // NG（asyncなのにサフィックスなし）

// インターフェースはIプレフィックス
IUserService, IRepository<T>, IEmailSender

// 汎用的すぎる名前は禁止
data / temp / result / obj   // NG
userList / createdExpense / parsedRow   // OK（文脈が分かる名前）
```

### 説明変数で複雑な条件式に名前を付ける

```csharp
// BAD: 条件式の意図が読めない
if (item.Date >= startDate && item.Date <= endDate && item.UserId == userId && !item.IsDeleted)

// GOOD: 説明変数で意図を表現
var isWithinDateRange = item.Date >= startDate && item.Date <= endDate;
var belongsToUser = item.UserId == userId;
var isActive = !item.IsDeleted;
if (isWithinDateRange && belongsToUser && isActive)
```

### マジックナンバー禁止

```csharp
// BAD
if (rows.Count > 10000) throw new ...

// GOOD
private const int MaxImportRows = 10_000;
if (rows.Count > MaxImportRows) throw new ...
```

---

## アーキテクチャ規約

### Controllerは薄く保つ

```csharp
// BAD: Controllerにビジネスロジック
[HttpPost]
public async Task<IActionResult> Create(CreateDto dto)
{
    // ここでバリデーション・ビジネスロジック・DB操作...
}

// GOOD: ServiceにロジックをI委譲
[HttpPost]
public async Task<IActionResult> Create(CreateDto dto)
{
    var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    var result = await _service.CreateAsync(userId, dto);
    return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
}
```

### エラーハンドリング

```csharp
// Controllerでのtry-catchは禁止（グローバルミドルウェアに委譲）
// BAD
try { ... } catch (Exception ex) { return StatusCode(500, ex.Message); }

// GOOD: カスタム例外をスロー → ミドルウェアが処理
throw new NotFoundException($"Item {id} not found");
throw new ValidationException("Amount must be greater than 0");
```

### バリデーション分離

```csharp
// DTOレベル: DataAnnotations（形式チェック）
public class CreateItemDto
{
    [Required]
    [StringLength(200)]
    public string Name { get; set; }

    [Range(0.01, 9_999_999.99)]
    public decimal Amount { get; set; }
}

// ビジネスルール: サービス層で実施
// エンティティの不変条件: エンティティ自身で保護
```

---

## EF Core のベストプラクティス

### N+1問題を避ける

```csharp
// BAD: ループ内でクエリが発生（N+1）
foreach (var item in items)
    item.Category = await _db.Categories.FindAsync(item.CategoryId);

// GOOD: Includeで一括取得
var items = await _db.Items
    .Include(i => i.Category)
    .Where(i => i.UserId == userId)
    .ToListAsync();
```

### 読み取り専用クエリ

```csharp
// 読み取り専用（集計・表示用）クエリはAsNoTrackingを使用
var items = await _db.Items
    .AsNoTracking()
    .Where(i => i.UserId == userId)
    .ToListAsync();
```

### async/await の正しい使い方

```csharp
// BAD: デッドロックの危険
var result = _service.GetAsync(id).Result;  // NG
_service.GetAsync(id).Wait();               // NG

// GOOD
var result = await _service.GetAsync(id);
```

---

## HTTP ステータスコードの使い分け

| 状況 | コード | ASP.NET Core |
|------|--------|--------------|
| 作成成功 | 201 | `CreatedAtAction(...)` |
| 更新・削除成功 | 204 | `NoContent()` |
| バリデーションエラー | 400 | `BadRequest(...)` |
| 未認証 | 401 | `Unauthorized()` |
| 権限なし | 403 | `Forbid()` |
| 存在しない | 404 | `NotFound(...)` |

---

## 禁止事項

- テストを書かずにコードをコミットしない
- `float` / `double` で金額・価格を計算しない（`decimal` を使う）
- `var result = ...` のような意味のない変数名を使わない
- 例外を握り潰さない（`catch (Exception) { }` は禁止）
- TODOコメントを期限とIssue IDなしで放置しない
- `Task.Result` / `.Wait()` を使わない
- ControllerにIFのビジネスロジックを書かない
- `new ConcreteService()` でサービスをインスタンス化しない（DIを使う）
