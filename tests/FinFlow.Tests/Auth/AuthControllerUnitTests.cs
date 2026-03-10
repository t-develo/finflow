using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using FinFlow.Api.Controllers;
using FinFlow.Api.Models;
using FinFlow.Infrastructure.Identity;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Moq;
using Xunit;

namespace FinFlow.Tests.Auth;

/// <summary>
/// AuthControllerのユニットテスト。
/// WebApplicationFactoryを使わずUserManagerをモックして高速に実行できる。
/// </summary>
public class AuthControllerUnitTests
{
    private readonly Mock<UserManager<ApplicationUser>> _userManagerMock;
    private readonly IConfiguration _configuration;
    private readonly AuthController _controller;

    public AuthControllerUnitTests()
    {
        var store = new Mock<IUserStore<ApplicationUser>>();
        _userManagerMock = new Mock<UserManager<ApplicationUser>>(
            store.Object, null!, null!, null!, null!, null!, null!, null!, null!);

        var configData = new Dictionary<string, string?>
        {
            ["Jwt:Key"] = "TestSecretKey_AtLeast32Characters_ForHmacSha256!",
            ["Jwt:Issuer"] = "TestIssuer",
            ["Jwt:Audience"] = "TestAudience",
            ["Jwt:ExpiryHours"] = "24"
        };
        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();

        _controller = new AuthController(_userManagerMock.Object, _configuration);
    }

    [Fact]
    public async Task Register_WithValidCredentials_ReturnsOkWithToken()
    {
        // Arrange
        var user = new ApplicationUser { Id = "user-123", Email = "test@example.com", UserName = "test@example.com" };
        _userManagerMock
            .Setup(m => m.CreateAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()))
            .ReturnsAsync(IdentityResult.Success)
            .Callback<ApplicationUser, string>((u, _) =>
            {
                u.Id = user.Id;
                u.Email = user.Email;
            });

        var request = new RegisterRequest { Email = "test@example.com", Password = "Password123" };

        // Act
        var result = await _controller.Register(request);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<AuthResponse>().Subject;
        response.Token.Should().NotBeNullOrEmpty();
        response.Email.Should().Be("test@example.com");
        response.ExpiresAt.Should().BeAfter(DateTime.UtcNow);
    }

    [Fact]
    public async Task Register_WhenIdentityFails_ReturnsBadRequest()
    {
        // Arrange
        var identityError = new IdentityError { Code = "DuplicateEmail", Description = "Email already taken." };
        _userManagerMock
            .Setup(m => m.CreateAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()))
            .ReturnsAsync(IdentityResult.Failed(identityError));

        var request = new RegisterRequest { Email = "dup@example.com", Password = "Password123" };

        // Act
        var result = await _controller.Register(request);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task Login_WithValidCredentials_ReturnsOkWithToken()
    {
        // Arrange
        var user = new ApplicationUser { Id = "user-456", Email = "login@example.com", UserName = "login@example.com" };
        _userManagerMock
            .Setup(m => m.FindByEmailAsync("login@example.com"))
            .ReturnsAsync(user);
        _userManagerMock
            .Setup(m => m.CheckPasswordAsync(user, "Password123"))
            .ReturnsAsync(true);

        var request = new LoginRequest { Email = "login@example.com", Password = "Password123" };

        // Act
        var result = await _controller.Login(request);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<AuthResponse>().Subject;
        response.Token.Should().NotBeNullOrEmpty();
        response.Email.Should().Be("login@example.com");
    }

    [Fact]
    public async Task Login_WithWrongPassword_ReturnsUnauthorized()
    {
        // Arrange
        var user = new ApplicationUser { Id = "user-789", Email = "wrongpass@example.com" };
        _userManagerMock
            .Setup(m => m.FindByEmailAsync("wrongpass@example.com"))
            .ReturnsAsync(user);
        _userManagerMock
            .Setup(m => m.CheckPasswordAsync(user, "WrongPassword"))
            .ReturnsAsync(false);

        var request = new LoginRequest { Email = "wrongpass@example.com", Password = "WrongPassword" };

        // Act
        var result = await _controller.Login(request);

        // Assert
        result.Should().BeOfType<UnauthorizedObjectResult>();
    }

    [Fact]
    public async Task Login_WithNonExistentUser_ReturnsUnauthorized()
    {
        // Arrange
        _userManagerMock
            .Setup(m => m.FindByEmailAsync("nobody@example.com"))
            .ReturnsAsync((ApplicationUser?)null);

        var request = new LoginRequest { Email = "nobody@example.com", Password = "Password123" };

        // Act
        var result = await _controller.Login(request);

        // Assert
        result.Should().BeOfType<UnauthorizedObjectResult>();
    }

    [Fact]
    public async Task Register_GeneratedToken_ContainsCorrectClaims()
    {
        // Arrange
        var user = new ApplicationUser { Id = "user-claims-test", Email = "claims@example.com", UserName = "claims@example.com" };
        _userManagerMock
            .Setup(m => m.CreateAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()))
            .ReturnsAsync(IdentityResult.Success)
            .Callback<ApplicationUser, string>((u, _) =>
            {
                u.Id = user.Id;
                u.Email = user.Email;
            });

        // Act
        var result = await _controller.Register(
            new RegisterRequest { Email = "claims@example.com", Password = "Password123" });

        // Assert
        var okResult = (OkObjectResult)result;
        var response = (AuthResponse)okResult.Value!;
        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadJwtToken(response.Token);
        jwtToken.Claims.Should().Contain(c => c.Type == JwtRegisteredClaimNames.Email && c.Value == "claims@example.com");
        jwtToken.Issuer.Should().Be("TestIssuer");
    }
}
