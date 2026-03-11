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
    protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            var descriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<FinFlowDbContext>));
            if (descriptor != null) services.Remove(descriptor);

            // テストごとに一意なDB名を使用してテスト間の干渉を防ぐ
            services.AddDbContext<FinFlowDbContext>(options =>
                options.UseInMemoryDatabase($"FinFlowExpensesTest_{Guid.NewGuid()}"));
        });

        // ホスト起動後にDBを初期化（シードデータを適用）
        builder.Configure(app =>
        {
            using var scope = app.ApplicationServices.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<FinFlowDbContext>();
            db.Database.EnsureCreated();
        });
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
        var expenses = JsonSerializer.Deserialize<List<JsonElement>>(body, JsonOptions);
        expenses.Should().NotBeNull();
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

    private static async Task<int> CreateTestCategoryAsync(HttpClient client, string? name = null)
    {
        var request = new CreateCategoryRequest
        {
            Name = name ?? $"テストカテゴリ_{Guid.NewGuid():N}",
            Color = "#123456"
        };
        var response = await client.PostAsJsonAsync("/api/categories", request);
        var body = await response.Content.ReadAsStringAsync();
        var category = JsonSerializer.Deserialize<JsonElement>(body, JsonOptions);
        return category.GetProperty("id").GetInt32();
    }
}
