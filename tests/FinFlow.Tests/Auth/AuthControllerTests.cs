using System.Net;
using System.Net.Http.Json;
using FinFlow.Api.Models;
using FinFlow.Infrastructure.Data;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace FinFlow.Tests.Auth;

/// <summary>
/// InMemory DBを使用したWebApplicationFactory。
/// IClassFixtureで共有することでホスト構築コストを1回に抑える。
/// </summary>
public class AuthTestFixture : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            var descriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<FinFlowDbContext>));
            if (descriptor != null) services.Remove(descriptor);

            services.AddDbContext<FinFlowDbContext>(options =>
                options.UseInMemoryDatabase("FinFlowAuthTest"));
        });
    }
}

/// <remarks>
/// 注意: このテストはWebApplicationFactoryでホストを起動するため、
/// WSL環境では起動タイムアウトが発生する場合があります。
/// CI/CD環境での実行を推奨します。ユニットテストは AuthControllerUnitTests を参照してください。
/// </remarks>
[Trait("Category", "Integration")]
public class AuthControllerTests : IClassFixture<AuthTestFixture>
{
    private readonly HttpClient _client;

    public AuthControllerTests(AuthTestFixture fixture)
    {
        _client = fixture.CreateClient();
    }

    [Fact]
    public async Task Register_WithValidCredentials_Returns200WithToken()
    {
        // Arrange
        var request = new RegisterRequest
        {
            Email = $"valid_{Guid.NewGuid():N}@example.com",
            Password = "Password123"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/register", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<AuthResponse>();
        body!.Token.Should().NotBeNullOrEmpty();
        body.Email.Should().Be(request.Email);
        body.ExpiresAt.Should().BeAfter(DateTime.UtcNow);
    }

    [Fact]
    public async Task Register_WithDuplicateEmail_Returns400()
    {
        // Arrange
        var email = $"dup_{Guid.NewGuid():N}@example.com";
        var request = new RegisterRequest { Email = email, Password = "Password123" };
        await _client.PostAsJsonAsync("/api/auth/register", request);

        // Act - register same email again
        var response = await _client.PostAsJsonAsync("/api/auth/register", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Register_WithShortPassword_Returns400()
    {
        // Arrange: パスワードポリシー（8文字以上）に違反
        var request = new RegisterRequest
        {
            Email = $"short_{Guid.NewGuid():N}@example.com",
            Password = "abc"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/register", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Login_WithValidCredentials_Returns200WithToken()
    {
        // Arrange
        var email = $"login_{Guid.NewGuid():N}@example.com";
        const string password = "Password123";
        await _client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest { Email = email, Password = password });

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest { Email = email, Password = password });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<AuthResponse>();
        body!.Token.Should().NotBeNullOrEmpty();
        body.Email.Should().Be(email);
    }

    [Fact]
    public async Task Login_WithWrongPassword_Returns401()
    {
        // Arrange
        var email = $"wrongpass_{Guid.NewGuid():N}@example.com";
        await _client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest { Email = email, Password = "Password123" });

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest { Email = email, Password = "WrongPassword!" });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Login_WithNonExistentUser_Returns401()
    {
        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest { Email = "nobody@example.com", Password = "Password123" });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
