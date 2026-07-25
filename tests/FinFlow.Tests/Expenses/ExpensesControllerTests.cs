using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FinFlow.Api.Models;
using FinFlow.Infrastructure.Data;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FinFlow.Tests.Expenses;

/// <summary>
/// InMemory DBを使用したWebApplicationFactory（支出テスト専用）
/// シードデータはEnsureCreatedで適用される（InMemoryDBはHasDataを反映する）
/// </summary>
public class ExpensesTestFixture : WebApplicationFactory<Program>
{
    // フィクスチャインスタンスごとに一意なDB名（リクエスト間でデータを共有するため）
    private readonly string _dbName = $"FinFlowExpensesTest_{Guid.NewGuid()}";

    protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            var descriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<FinFlowDbContext>));
            if (descriptor != null) services.Remove(descriptor);

            services.AddDbContext<FinFlowDbContext>(options =>
                options.UseInMemoryDatabase(_dbName));
        });
    }

    protected override Microsoft.Extensions.Hosting.IHost CreateHost(Microsoft.Extensions.Hosting.IHostBuilder builder)
    {
        var host = base.CreateHost(builder);

        // ホスト起動後にDBを初期化（シードデータを適用）
        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FinFlowDbContext>();
        db.Database.EnsureCreated();

        return host;
    }
}

[Trait("Category", "ExpensesController")]
public class ExpensesControllerTests : IClassFixture<ExpensesTestFixture>
{
    private readonly ExpensesTestFixture _fixture;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public ExpensesControllerTests(ExpensesTestFixture fixture)
    {
        _fixture = fixture;
    }

    // テストごとに独立したクライアントとDBを使用する
    private HttpClient CreateFreshClient() => _fixture.CreateClient();

    // =====================================================================
    // 認証テスト
    // =====================================================================

    [Fact]
    public async Task GetExpenses_WithoutAuth_Returns401()
    {
        // Arrange
        var client = CreateFreshClient();

        // Act: 認証トークンなしでアクセス
        var response = await client.GetAsync("/api/expenses");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // =====================================================================
    // 支出一覧取得テスト
    // =====================================================================

    [Fact]
    public async Task GetExpenses_WithValidAuth_Returns200WithList()
    {
        // Arrange
        var client = CreateFreshClient();
        await SetupAuthAsync(client, $"expenses_list_{Guid.NewGuid():N}@example.com");

        // Act
        var response = await client.GetAsync("/api/expenses");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        // S2-A-004: レスポンス形式が { data: [...], pagination: {...} } に変更された
        var result = JsonSerializer.Deserialize<JsonElement>(body, JsonOptions);
        result.TryGetProperty("data", out var dataElement).Should().BeTrue("response should have 'data' property");
        dataElement.ValueKind.Should().Be(JsonValueKind.Array);
        result.TryGetProperty("pagination", out var paginationElement).Should().BeTrue("response should have 'pagination' property");
        result.TryGetProperty("totalAmount", out var totalAmountElement).Should().BeTrue("response should have 'totalAmount' property");
        totalAmountElement.ValueKind.Should().Be(JsonValueKind.Number);
    }

    [Fact]
    public async Task GetExpenses_WithNoExpenses_ReturnsZeroTotalAmount()
    {
        // Arrange: 支出を1件も登録していない新規ユーザー
        var client = CreateFreshClient();
        await SetupAuthAsync(client, $"expenses_empty_total_{Guid.NewGuid():N}@example.com");

        // Act
        var response = await client.GetAsync("/api/expenses");

        // Assert: totalAmount は null ではなく 0 であること
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<JsonElement>(body, JsonOptions);
        result.GetProperty("totalAmount").GetDecimal().Should().Be(0m);
    }

    [Fact]
    public async Task GetExpenses_WithMultipleExpenses_ReturnsSumOfAllMatchingAmounts()
    {
        // Arrange
        var client = CreateFreshClient();
        await SetupAuthAsync(client, $"expenses_total_{Guid.NewGuid():N}@example.com");
        var categoryId = await CreateTestCategoryAsync(client);

        foreach (var amount in new[] { 1000m, 2500.50m, 999.50m })
        {
            var request = new CreateExpenseRequest
            {
                Amount = amount,
                CategoryId = categoryId,
                Date = new DateOnly(2026, 3, 8),
                Description = "合計金額テスト"
            };
            await client.PostAsJsonAsync("/api/expenses", request);
        }

        // Act
        var response = await client.GetAsync("/api/expenses");

        // Assert: 全件の合計（現在ページ分だけではない）が返ること
        var body = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<JsonElement>(body, JsonOptions);
        result.GetProperty("totalAmount").GetDecimal().Should().Be(4500.00m);
    }

    // =====================================================================
    // 支出作成テスト
    // =====================================================================

    [Fact]
    public async Task CreateExpense_WithValidData_Returns201WithCreatedExpense()
    {
        // Arrange
        var client = CreateFreshClient();
        await SetupAuthAsync(client, $"expenses_create_{Guid.NewGuid():N}@example.com");

        // カテゴリをテスト内で作成する（シードデータに依存しない）
        var categoryId = await CreateTestCategoryAsync(client);

        var request = new CreateExpenseRequest
        {
            Amount = 1500m,
            CategoryId = categoryId,
            Date = new DateOnly(2026, 3, 8),
            Description = "コンビニ 昼食"
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/expenses", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadAsStringAsync();
        var created = JsonSerializer.Deserialize<JsonElement>(body, JsonOptions);
        created.GetProperty("amount").GetDecimal().Should().Be(1500m);
        created.GetProperty("description").GetString().Should().Be("コンビニ 昼食");
    }

    [Fact]
    public async Task CreateExpense_WithNegativeAmount_Returns400()
    {
        // Arrange
        var client = CreateFreshClient();
        await SetupAuthAsync(client, $"expenses_neg_{Guid.NewGuid():N}@example.com");

        var request = new CreateExpenseRequest
        {
            Amount = -100m,
            CategoryId = 1,
            Date = new DateOnly(2026, 3, 8),
            Description = "不正な金額"
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/expenses", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateExpense_WithEmptyDescription_Returns400()
    {
        // Arrange
        var client = CreateFreshClient();
        await SetupAuthAsync(client, $"expenses_desc_{Guid.NewGuid():N}@example.com");

        var request = new CreateExpenseRequest
        {
            Amount = 1000m,
            CategoryId = 1,
            Date = new DateOnly(2026, 3, 8),
            Description = "" // 必須フィールドが空
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/expenses", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateExpense_WithNonExistentCategory_Returns400()
    {
        // Arrange
        var client = CreateFreshClient();
        await SetupAuthAsync(client, $"expenses_cat_{Guid.NewGuid():N}@example.com");

        var request = new CreateExpenseRequest
        {
            Amount = 1000m,
            CategoryId = 99999, // 存在しないカテゴリ
            Date = new DateOnly(2026, 3, 8),
            Description = "存在しないカテゴリ"
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/expenses", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // =====================================================================
    // 支出取得（個別）テスト
    // =====================================================================

    [Fact]
    public async Task GetExpense_WithNonExistentId_Returns404()
    {
        // Arrange
        var client = CreateFreshClient();
        await SetupAuthAsync(client, $"expenses_404_{Guid.NewGuid():N}@example.com");

        // Act
        var response = await client.GetAsync("/api/expenses/99999");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetExpense_WithCategorySet_ReturnsCategoryColorInResponse()
    {
        // Arrange: カテゴリバッジの色分けに使うため、カテゴリの色がレスポンスに含まれる必要がある
        var client = CreateFreshClient();
        await SetupAuthAsync(client, $"expenses_catcolor_{Guid.NewGuid():N}@example.com");

        var categoryId = await CreateTestCategoryAsync(client, color: "#FF5733");

        var createRequest = new CreateExpenseRequest
        {
            Amount = 1200m,
            CategoryId = categoryId,
            Date = new DateOnly(2026, 3, 12),
            Description = "カテゴリ色確認用"
        };
        var createResponse = await client.PostAsJsonAsync("/api/expenses", createRequest);
        var createdBody = await createResponse.Content.ReadAsStringAsync();
        var createdExpense = JsonSerializer.Deserialize<JsonElement>(createdBody, JsonOptions);
        var expenseId = createdExpense.GetProperty("id").GetInt32();

        // Act
        var response = await client.GetAsync($"/api/expenses/{expenseId}");

        // Assert: レスポンスに categoryColor が含まれ、カテゴリ作成時の色と一致すること
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<JsonElement>(body, JsonOptions);
        result.TryGetProperty("categoryColor", out var categoryColorElement).Should().BeTrue("response should have 'categoryColor' property");
        categoryColorElement.GetString().Should().Be("#FF5733");
    }

    [Fact]
    public async Task GetExpense_WithNoCategoryAssigned_ReturnsNullCategoryColorWithoutError()
    {
        // Arrange: CSV自動取込などでカテゴリが未分類（CategoryId = null）のまま
        // 登録される支出を想定する。API経由ではCategoryIdが必須のため、
        // DbContextを直接操作して未分類状態を再現する。
        var client = CreateFreshClient();
        await SetupAuthAsync(client, $"expenses_nocat_{Guid.NewGuid():N}@example.com");

        var categoryId = await CreateTestCategoryAsync(client);

        var createRequest = new CreateExpenseRequest
        {
            Amount = 800m,
            CategoryId = categoryId,
            Date = new DateOnly(2026, 3, 13),
            Description = "未分類化テスト用"
        };
        var createResponse = await client.PostAsJsonAsync("/api/expenses", createRequest);
        var createdBody = await createResponse.Content.ReadAsStringAsync();
        var createdExpense = JsonSerializer.Deserialize<JsonElement>(createdBody, JsonOptions);
        var expenseId = createdExpense.GetProperty("id").GetInt32();

        using (var scope = _fixture.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<FinFlowDbContext>();
            var expense = await dbContext.Expenses.FirstAsync(e => e.Id == expenseId);
            expense.CategoryId = null;
            await dbContext.SaveChangesAsync();
        }

        // Act
        var response = await client.GetAsync($"/api/expenses/{expenseId}");

        // Assert: 例外にならず200が返り、categoryColor/categoryName はnullであること
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<JsonElement>(body, JsonOptions);
        result.TryGetProperty("categoryColor", out var categoryColorElement).Should().BeTrue("response should have 'categoryColor' property");
        categoryColorElement.ValueKind.Should().Be(JsonValueKind.Null);
        result.GetProperty("categoryName").ValueKind.Should().Be(JsonValueKind.Null);
    }

    // =====================================================================
    // 支出更新テスト
    // =====================================================================

    [Fact]
    public async Task UpdateExpense_WithValidData_Returns200WithUpdatedExpense()
    {
        // Arrange: まず支出を作成してからIDを取得
        var client = CreateFreshClient();
        await SetupAuthAsync(client, $"expenses_update_{Guid.NewGuid():N}@example.com");

        var categoryId = await CreateTestCategoryAsync(client);
        var category2Id = await CreateTestCategoryAsync(client, "テストカテゴリ2");

        var createRequest = new CreateExpenseRequest
        {
            Amount = 1000m,
            CategoryId = categoryId,
            Date = new DateOnly(2026, 3, 1),
            Description = "元の説明"
        };
        var createResponse = await client.PostAsJsonAsync("/api/expenses", createRequest);
        var createdBody = await createResponse.Content.ReadAsStringAsync();
        var createdExpense = JsonSerializer.Deserialize<JsonElement>(createdBody, JsonOptions);
        var expenseId = createdExpense.GetProperty("id").GetInt32();

        var updateRequest = new UpdateExpenseRequest
        {
            Amount = 2000m,
            CategoryId = category2Id,
            Date = new DateOnly(2026, 3, 10),
            Description = "更新後の説明"
        };

        // Act
        var response = await client.PutAsJsonAsync($"/api/expenses/{expenseId}", updateRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        var updated = JsonSerializer.Deserialize<JsonElement>(body, JsonOptions);
        updated.GetProperty("amount").GetDecimal().Should().Be(2000m);
        updated.GetProperty("description").GetString().Should().Be("更新後の説明");
    }

    // =====================================================================
    // 支出削除テスト
    // =====================================================================

    [Fact]
    public async Task DeleteExpense_WithValidId_Returns204()
    {
        // Arrange
        var client = CreateFreshClient();
        await SetupAuthAsync(client, $"expenses_delete_{Guid.NewGuid():N}@example.com");

        var categoryId = await CreateTestCategoryAsync(client);

        var createRequest = new CreateExpenseRequest
        {
            Amount = 1000m,
            CategoryId = categoryId,
            Date = new DateOnly(2026, 3, 1),
            Description = "削除対象"
        };
        var createResponse = await client.PostAsJsonAsync("/api/expenses", createRequest);
        var createdBody = await createResponse.Content.ReadAsStringAsync();
        var createdExpense = JsonSerializer.Deserialize<JsonElement>(createdBody, JsonOptions);
        var expenseId = createdExpense.GetProperty("id").GetInt32();

        // Act
        var response = await client.DeleteAsync($"/api/expenses/{expenseId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task DeleteExpense_WithNonExistentId_Returns404()
    {
        // Arrange
        var client = CreateFreshClient();
        await SetupAuthAsync(client, $"expenses_del404_{Guid.NewGuid():N}@example.com");

        // Act
        var response = await client.DeleteAsync("/api/expenses/99999");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // =====================================================================
    // ヘルパー
    // =====================================================================

    private static async Task SetupAuthAsync(HttpClient client, string email)
    {
        var request = new RegisterRequest { Email = email, Password = "Password123" };
        var response = await client.PostAsJsonAsync("/api/auth/register", request);
        var body = await response.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions);
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", body!.Token);
    }

    private static async Task<int> CreateTestCategoryAsync(HttpClient client, string? name = null, string color = "#123456")
    {
        var request = new CreateCategoryRequest
        {
            Name = name ?? $"テストカテゴリ_{Guid.NewGuid():N}",
            Color = color
        };
        var response = await client.PostAsJsonAsync("/api/categories", request);
        var body = await response.Content.ReadAsStringAsync();
        var category = JsonSerializer.Deserialize<JsonElement>(body, JsonOptions);
        return category.GetProperty("id").GetInt32();
    }
}
