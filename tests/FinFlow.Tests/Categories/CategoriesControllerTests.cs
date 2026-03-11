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

namespace FinFlow.Tests.Categories;

/// <summary>
/// InMemory DBを使用したWebApplicationFactory（カテゴリテスト専用）
/// </summary>
public class CategoriesTestFixture : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            var descriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<FinFlowDbContext>));
            if (descriptor != null) services.Remove(descriptor);

            services.AddDbContext<FinFlowDbContext>(options =>
                options.UseInMemoryDatabase("FinFlowCategoriesTest"));
        });
    }

    protected override Microsoft.Extensions.Hosting.IHost CreateHost(Microsoft.Extensions.Hosting.IHostBuilder builder)
    {
        var host = base.CreateHost(builder);

        // ホスト起動後にDBを初期化（システムカテゴリのシードデータを適用）
        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FinFlowDbContext>();
        db.Database.EnsureCreated();

        return host;
    }
}

[Trait("Category", "CategoriesController")]
public class CategoriesControllerTests : IClassFixture<CategoriesTestFixture>
{
    private readonly HttpClient _client;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public CategoriesControllerTests(CategoriesTestFixture fixture)
    {
        _client = fixture.CreateClient();
    }

    // =====================================================================
    // カテゴリ一覧取得テスト
    // =====================================================================

    [Fact]
    public async Task GetCategories_WithoutAuth_Returns401()
    {
        // Act
        var response = await _client.GetAsync("/api/categories");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetCategories_WithValidAuth_Returns200WithSystemCategories()
    {
        // Arrange
        var token = await RegisterAndGetTokenAsync($"cat_list_{Guid.NewGuid():N}@example.com");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await _client.GetAsync("/api/categories");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        var categories = JsonSerializer.Deserialize<List<JsonElement>>(body, JsonOptions);

        // シードデータのシステムカテゴリが存在する
        categories.Should().NotBeNullOrEmpty();
        var systemCategories = categories!.Where(c => c.GetProperty("isSystem").GetBoolean()).ToList();
        systemCategories.Should().NotBeEmpty();
    }

    // =====================================================================
    // カテゴリ作成テスト
    // =====================================================================

    [Fact]
    public async Task CreateCategory_WithValidData_Returns201WithCreatedCategory()
    {
        // Arrange
        var token = await RegisterAndGetTokenAsync($"cat_create_{Guid.NewGuid():N}@example.com");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var request = new CreateCategoryRequest
        {
            Name = $"テストカテゴリ_{Guid.NewGuid():N}",
            Color = "#FF5733"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/categories", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadAsStringAsync();
        var created = JsonSerializer.Deserialize<JsonElement>(body, JsonOptions);
        created.GetProperty("name").GetString().Should().Be(request.Name);
        created.GetProperty("color").GetString().Should().Be("#FF5733");
        created.GetProperty("isSystem").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task CreateCategory_WithEmptyName_Returns400()
    {
        // Arrange
        var token = await RegisterAndGetTokenAsync($"cat_empty_{Guid.NewGuid():N}@example.com");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var request = new CreateCategoryRequest
        {
            Name = "", // 必須フィールドが空
            Color = "#FF5733"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/categories", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // =====================================================================
    // カテゴリ取得（個別）テスト
    // =====================================================================

    [Fact]
    public async Task GetCategory_WithSystemCategoryId_Returns200()
    {
        // Arrange: ID=1は食費（シードデータ）
        var token = await RegisterAndGetTokenAsync($"cat_get_{Guid.NewGuid():N}@example.com");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await _client.GetAsync("/api/categories/1");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        var category = JsonSerializer.Deserialize<JsonElement>(body, JsonOptions);
        category.GetProperty("name").GetString().Should().Be("食費");
    }

    [Fact]
    public async Task GetCategory_WithNonExistentId_Returns404()
    {
        // Arrange
        var token = await RegisterAndGetTokenAsync($"cat_404_{Guid.NewGuid():N}@example.com");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await _client.GetAsync("/api/categories/99999");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // =====================================================================
    // カテゴリ削除テスト
    // =====================================================================

    [Fact]
    public async Task DeleteCategory_WithSystemCategory_Returns409Conflict()
    {
        // Arrange: ID=1 は食費（システムカテゴリ）
        var token = await RegisterAndGetTokenAsync($"cat_sys_del_{Guid.NewGuid():N}@example.com");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await _client.DeleteAsync("/api/categories/1");

        // Assert: システムカテゴリは削除不可（409 Conflict）
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task DeleteCategory_WithUserCategory_Returns204()
    {
        // Arrange: ユーザー独自カテゴリを作成して削除
        var token = await RegisterAndGetTokenAsync($"cat_user_del_{Guid.NewGuid():N}@example.com");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var createRequest = new CreateCategoryRequest
        {
            Name = $"削除テスト_{Guid.NewGuid():N}",
            Color = "#123456"
        };
        var createResponse = await _client.PostAsJsonAsync("/api/categories", createRequest);
        var createdBody = await createResponse.Content.ReadAsStringAsync();
        var created = JsonSerializer.Deserialize<JsonElement>(createdBody, JsonOptions);
        var categoryId = created.GetProperty("id").GetInt32();

        // Act
        var response = await _client.DeleteAsync($"/api/categories/{categoryId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task DeleteCategory_WithNonExistentId_Returns404()
    {
        // Arrange
        var token = await RegisterAndGetTokenAsync($"cat_del_404_{Guid.NewGuid():N}@example.com");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await _client.DeleteAsync("/api/categories/99999");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // =====================================================================
    // ヘルパー
    // =====================================================================

    private async Task<string> RegisterAndGetTokenAsync(string email)
    {
        var request = new RegisterRequest { Email = email, Password = "Password123" };
        var response = await _client.PostAsJsonAsync("/api/auth/register", request);
        var body = await response.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions);
        return body!.Token;
    }
}
