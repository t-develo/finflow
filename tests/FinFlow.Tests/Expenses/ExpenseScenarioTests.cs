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
/// 支出サービスE2Eシナリオテスト（S3-A-002）
/// 支出の登録→一覧取得→更新→削除の一連のフローと
/// フィルタ・ページング機能の動作確認を行う
/// </summary>
public class ExpenseScenarioTestFixture : WebApplicationFactory<Program>
{
    private readonly string _dbName = $"FinFlowScenarioTest_{Guid.NewGuid()}";

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

        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FinFlowDbContext>();
        db.Database.EnsureCreated();

        return host;
    }
}

[Trait("Category", "ExpenseScenario")]
public class ExpenseScenarioTests : IClassFixture<ExpenseScenarioTestFixture>
{
    private readonly ExpenseScenarioTestFixture _fixture;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public ExpenseScenarioTests(ExpenseScenarioTestFixture fixture)
    {
        _fixture = fixture;
    }

    private HttpClient CreateFreshClient() => _fixture.CreateClient();

    // =====================================================================
    // 支出登録 → 一覧取得 → 更新 → 削除の一連フローテスト
    // =====================================================================

    [Fact]
    public async Task ExpenseCrudFlow_CreateReadUpdateDelete_CompletesSuccessfully()
    {
        // Arrange: 認証済みクライアントとテストカテゴリを準備する
        var client = CreateFreshClient();
        await SetupAuthAsync(client, $"scenario_crud_{Guid.NewGuid():N}@example.com");
        var categoryId = await CreateTestCategoryAsync(client);

        // --- Step 1: 支出を登録する ---
        var createRequest = new CreateExpenseRequest
        {
            Amount = 2500m,
            CategoryId = categoryId,
            Date = new DateOnly(2026, 3, 1),
            Description = "シナリオテスト: 食料品"
        };
        var createResponse = await client.PostAsJsonAsync("/api/expenses", createRequest);
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created, "支出の登録が成功すること");

        var createdBody = await createResponse.Content.ReadAsStringAsync();
        var createdExpense = JsonSerializer.Deserialize<JsonElement>(createdBody, JsonOptions);
        var expenseId = createdExpense.GetProperty("id").GetInt32();
        createdExpense.GetProperty("amount").GetDecimal().Should().Be(2500m);
        createdExpense.GetProperty("description").GetString().Should().Be("シナリオテスト: 食料品");

        // --- Step 2: 一覧から登録した支出を確認する ---
        var listResponse = await client.GetAsync("/api/expenses");
        listResponse.StatusCode.Should().Be(HttpStatusCode.OK, "一覧取得が成功すること");

        var listBody = await listResponse.Content.ReadAsStringAsync();
        var listResult = JsonSerializer.Deserialize<JsonElement>(listBody, JsonOptions);
        var dataArray = listResult.GetProperty("data");
        dataArray.GetArrayLength().Should().BeGreaterThanOrEqualTo(1, "登録した支出が一覧に含まれること");

        // --- Step 3: 個別取得で詳細を確認する ---
        var getResponse = await client.GetAsync($"/api/expenses/{expenseId}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK, "個別取得が成功すること");

        var getBody = await getResponse.Content.ReadAsStringAsync();
        var fetchedExpense = JsonSerializer.Deserialize<JsonElement>(getBody, JsonOptions);
        fetchedExpense.GetProperty("id").GetInt32().Should().Be(expenseId);
        fetchedExpense.GetProperty("amount").GetDecimal().Should().Be(2500m);

        // --- Step 4: 支出を更新する ---
        var category2Id = await CreateTestCategoryAsync(client, "更新用カテゴリ");
        var updateRequest = new UpdateExpenseRequest
        {
            Amount = 3000m,
            CategoryId = category2Id,
            Date = new DateOnly(2026, 3, 15),
            Description = "シナリオテスト: 食料品（更新）"
        };
        var updateResponse = await client.PutAsJsonAsync($"/api/expenses/{expenseId}", updateRequest);
        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK, "支出の更新が成功すること");

        var updatedBody = await updateResponse.Content.ReadAsStringAsync();
        var updatedExpense = JsonSerializer.Deserialize<JsonElement>(updatedBody, JsonOptions);
        updatedExpense.GetProperty("amount").GetDecimal().Should().Be(3000m);
        updatedExpense.GetProperty("description").GetString().Should().Be("シナリオテスト: 食料品（更新）");

        // --- Step 5: 支出を削除する ---
        var deleteResponse = await client.DeleteAsync($"/api/expenses/{expenseId}");
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent, "支出の削除が成功すること");

        // --- Step 6: 削除後は取得できないことを確認する ---
        var getAfterDeleteResponse = await client.GetAsync($"/api/expenses/{expenseId}");
        getAfterDeleteResponse.StatusCode.Should().Be(HttpStatusCode.NotFound, "削除後は404が返ること");
    }

    [Fact]
    public async Task ExpenseCrudFlow_MultipleExpenses_IsolatedByUser()
    {
        // Arrange: 2人の異なるユーザーで支出を登録する
        var client1 = CreateFreshClient();
        var client2 = CreateFreshClient();
        await SetupAuthAsync(client1, $"scenario_user1_{Guid.NewGuid():N}@example.com");
        await SetupAuthAsync(client2, $"scenario_user2_{Guid.NewGuid():N}@example.com");

        var cat1Id = await CreateTestCategoryAsync(client1);
        var cat2Id = await CreateTestCategoryAsync(client2);

        // ユーザー1の支出を作成する
        var user1Request = new CreateExpenseRequest
        {
            Amount = 1000m,
            CategoryId = cat1Id,
            Date = new DateOnly(2026, 3, 1),
            Description = "ユーザー1の支出"
        };
        await client1.PostAsJsonAsync("/api/expenses", user1Request);
        await client1.PostAsJsonAsync("/api/expenses", user1Request);

        // ユーザー2の支出を作成する
        var user2Request = new CreateExpenseRequest
        {
            Amount = 5000m,
            CategoryId = cat2Id,
            Date = new DateOnly(2026, 3, 2),
            Description = "ユーザー2の支出"
        };
        await client2.PostAsJsonAsync("/api/expenses", user2Request);

        // Act: 各ユーザーが自分の支出のみ取得できることを確認する
        var list1Response = await client1.GetAsync("/api/expenses");
        var list2Response = await client2.GetAsync("/api/expenses");

        // Assert
        var list1Body = await list1Response.Content.ReadAsStringAsync();
        var list2Body = await list2Response.Content.ReadAsStringAsync();

        var list1 = JsonSerializer.Deserialize<JsonElement>(list1Body, JsonOptions);
        var list2 = JsonSerializer.Deserialize<JsonElement>(list2Body, JsonOptions);

        // ユーザー1は自分の2件のみ取得できること
        list1.GetProperty("data").GetArrayLength().Should().Be(2);
        // ユーザー2は自分の1件のみ取得できること
        list2.GetProperty("data").GetArrayLength().Should().Be(1);
    }

    // =====================================================================
    // フィルタ機能の動作確認テスト
    // =====================================================================

    [Fact]
    public async Task ExpenseFilter_ByDateRange_ReturnsOnlyMatchingExpenses()
    {
        // Arrange: 複数の支出を異なる日付で登録する
        var client = CreateFreshClient();
        await SetupAuthAsync(client, $"scenario_filter_date_{Guid.NewGuid():N}@example.com");
        var categoryId = await CreateTestCategoryAsync(client);

        // 1月・2月・3月の支出を登録する
        await CreateExpenseAsync(client, 1000m, categoryId, new DateOnly(2026, 1, 15), "1月の支出");
        await CreateExpenseAsync(client, 2000m, categoryId, new DateOnly(2026, 2, 10), "2月の支出");
        await CreateExpenseAsync(client, 3000m, categoryId, new DateOnly(2026, 3, 5), "3月の支出A");
        await CreateExpenseAsync(client, 4000m, categoryId, new DateOnly(2026, 3, 20), "3月の支出B");

        // Act: 3月のみをフィルタする
        var response = await client.GetAsync("/api/expenses?from=2026-03-01&to=2026-03-31");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<JsonElement>(body, JsonOptions);
        var data = result.GetProperty("data");

        // Assert: 3月の支出2件のみが返ること
        data.GetArrayLength().Should().Be(2);
    }

    [Fact]
    public async Task ExpenseFilter_ByCategory_ReturnsOnlyCategoryExpenses()
    {
        // Arrange: 2つのカテゴリの支出を登録する
        var client = CreateFreshClient();
        await SetupAuthAsync(client, $"scenario_filter_cat_{Guid.NewGuid():N}@example.com");
        var categoryAId = await CreateTestCategoryAsync(client, "カテゴリA");
        var categoryBId = await CreateTestCategoryAsync(client, "カテゴリB");

        await CreateExpenseAsync(client, 1000m, categoryAId, new DateOnly(2026, 3, 1), "カテゴリAの支出1");
        await CreateExpenseAsync(client, 2000m, categoryAId, new DateOnly(2026, 3, 2), "カテゴリAの支出2");
        await CreateExpenseAsync(client, 3000m, categoryBId, new DateOnly(2026, 3, 3), "カテゴリBの支出");

        // Act: カテゴリAのみをフィルタする
        var response = await client.GetAsync($"/api/expenses?categoryId={categoryAId}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<JsonElement>(body, JsonOptions);
        var data = result.GetProperty("data");

        // Assert: カテゴリAの支出2件のみが返ること
        data.GetArrayLength().Should().Be(2);
    }

    [Fact]
    public async Task ExpenseFilter_ByKeyword_ReturnsMatchingExpenses()
    {
        // Arrange: キーワード検索用の支出を登録する
        var client = CreateFreshClient();
        await SetupAuthAsync(client, $"scenario_filter_kw_{Guid.NewGuid():N}@example.com");
        var categoryId = await CreateTestCategoryAsync(client);

        await CreateExpenseAsync(client, 500m, categoryId, new DateOnly(2026, 3, 1), "コンビニ 昼食");
        await CreateExpenseAsync(client, 800m, categoryId, new DateOnly(2026, 3, 2), "スーパー 夕食");
        await CreateExpenseAsync(client, 300m, categoryId, new DateOnly(2026, 3, 3), "コンビニ 飲み物");

        // Act: 「コンビニ」でキーワード検索する
        var response = await client.GetAsync("/api/expenses?keyword=コンビニ");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<JsonElement>(body, JsonOptions);
        var data = result.GetProperty("data");

        // Assert: コンビニを含む支出2件のみが返ること
        data.GetArrayLength().Should().Be(2);
    }

    // =====================================================================
    // ページング機能の動作確認テスト
    // =====================================================================

    [Fact]
    public async Task ExpensePaging_WithMultiplePages_ReturnsCorrectPageData()
    {
        // Arrange: ページング確認のため6件の支出を登録する
        var client = CreateFreshClient();
        await SetupAuthAsync(client, $"scenario_paging_{Guid.NewGuid():N}@example.com");
        var categoryId = await CreateTestCategoryAsync(client);

        for (var i = 1; i <= 6; i++)
        {
            await CreateExpenseAsync(client, i * 100m, categoryId,
                new DateOnly(2026, 3, i), $"支出 {i:D2}");
        }

        // Act: ページサイズ3でページ1を取得する
        var page1Response = await client.GetAsync("/api/expenses?page=1&pageSize=3");
        var page2Response = await client.GetAsync("/api/expenses?page=2&pageSize=3");

        page1Response.StatusCode.Should().Be(HttpStatusCode.OK);
        page2Response.StatusCode.Should().Be(HttpStatusCode.OK);

        var page1Body = await page1Response.Content.ReadAsStringAsync();
        var page2Body = await page2Response.Content.ReadAsStringAsync();

        var page1Result = JsonSerializer.Deserialize<JsonElement>(page1Body, JsonOptions);
        var page2Result = JsonSerializer.Deserialize<JsonElement>(page2Body, JsonOptions);

        // Assert: 各ページに3件ずつ返ること
        page1Result.GetProperty("data").GetArrayLength().Should().Be(3,
            "ページ1には3件の支出が返ること");
        page2Result.GetProperty("data").GetArrayLength().Should().Be(3,
            "ページ2には3件の支出が返ること");

        // ページネーション情報の確認
        var pagination1 = page1Result.GetProperty("pagination");
        pagination1.GetProperty("page").GetInt32().Should().Be(1);
        pagination1.GetProperty("pageSize").GetInt32().Should().Be(3);
        pagination1.GetProperty("totalCount").GetInt32().Should().BeGreaterThanOrEqualTo(6);
    }

    [Fact]
    public async Task ExpensePaging_WithLastPage_ReturnsRemainingItems()
    {
        // Arrange: ページング確認のため5件の支出を登録する
        var client = CreateFreshClient();
        await SetupAuthAsync(client, $"scenario_paging_last_{Guid.NewGuid():N}@example.com");
        var categoryId = await CreateTestCategoryAsync(client);

        for (var i = 1; i <= 5; i++)
        {
            await CreateExpenseAsync(client, i * 200m, categoryId,
                new DateOnly(2026, 3, i), $"ページング支出 {i:D2}");
        }

        // Act: ページサイズ3でページ2（最終ページ）を取得する
        var response = await client.GetAsync("/api/expenses?page=2&pageSize=3");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<JsonElement>(body, JsonOptions);

        // Assert: 最終ページには残り2件が返ること
        result.GetProperty("data").GetArrayLength().Should().Be(2,
            "最終ページには残りの2件が返ること");
    }

    // =====================================================================
    // 複合フィルタ＆ページングのシナリオテスト
    // =====================================================================

    [Fact]
    public async Task ExpenseFilter_CombinedFiltersWithPaging_ReturnsCorrectSubset()
    {
        // Arrange: 異なる日付とカテゴリを組み合わせた支出を登録する
        var client = CreateFreshClient();
        await SetupAuthAsync(client, $"scenario_combined_{Guid.NewGuid():N}@example.com");
        var foodCategoryId = await CreateTestCategoryAsync(client, "食費複合");
        var transportCategoryId = await CreateTestCategoryAsync(client, "交通費複合");

        // 食費（3月）
        await CreateExpenseAsync(client, 500m, foodCategoryId, new DateOnly(2026, 3, 1), "食費1");
        await CreateExpenseAsync(client, 700m, foodCategoryId, new DateOnly(2026, 3, 5), "食費2");
        await CreateExpenseAsync(client, 900m, foodCategoryId, new DateOnly(2026, 3, 10), "食費3");
        // 交通費（3月）
        await CreateExpenseAsync(client, 300m, transportCategoryId, new DateOnly(2026, 3, 3), "交通費1");
        // 食費（4月 - フィルタ対象外）
        await CreateExpenseAsync(client, 1500m, foodCategoryId, new DateOnly(2026, 4, 1), "食費4月");

        // Act: 3月の食費のみをフィルタし、ページサイズ2でページ1を取得する
        var response = await client.GetAsync(
            $"/api/expenses?from=2026-03-01&to=2026-03-31&categoryId={foodCategoryId}&page=1&pageSize=2");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<JsonElement>(body, JsonOptions);
        var data = result.GetProperty("data");
        var pagination = result.GetProperty("pagination");

        // Assert: 3月の食費3件のうち、ページ1の2件が返ること
        data.GetArrayLength().Should().Be(2,
            "3月の食費3件のうち、ページサイズ2でページ1なので2件が返ること");
        pagination.GetProperty("totalCount").GetInt32().Should().Be(3,
            "3月の食費の合計は3件であること");
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
            Color = "#FF5722"
        };
        var response = await client.PostAsJsonAsync("/api/categories", request);
        var body = await response.Content.ReadAsStringAsync();
        var category = JsonSerializer.Deserialize<JsonElement>(body, JsonOptions);
        return category.GetProperty("id").GetInt32();
    }

    private static async Task<JsonElement> CreateExpenseAsync(
        HttpClient client,
        decimal amount,
        int categoryId,
        DateOnly date,
        string description)
    {
        var request = new CreateExpenseRequest
        {
            Amount = amount,
            CategoryId = categoryId,
            Date = date,
            Description = description
        };
        var response = await client.PostAsJsonAsync("/api/expenses", request);
        response.StatusCode.Should().Be(HttpStatusCode.Created, $"支出「{description}」の登録に成功すること");
        var body = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<JsonElement>(body, JsonOptions);
    }
}
