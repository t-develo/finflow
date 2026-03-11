using System.Security.Claims;
using FinFlow.Api.Controllers;
using FinFlow.Domain.Entities;
using FinFlow.Domain.Interfaces;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace FinFlow.Tests.Subscriptions;

/// <summary>
/// SubscriptionsController のユニットテスト。
/// ISubscriptionService をMockして、コントローラーのHTTP入出力変換のみを検証する。
/// </summary>
public class SubscriptionsControllerTests
{
    private const string TestUserId = "user-abc-123";

    private static SubscriptionsController CreateController(ISubscriptionService service)
    {
        var controller = new SubscriptionsController(service);

        // JWT ClaimsにUserIdを設定する
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, TestUserId)
        };
        var identity = new ClaimsIdentity(claims, "Test");
        var principal = new ClaimsPrincipal(identity);

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = principal }
        };

        return controller;
    }

    private static Subscription BuildSubscription(int id, string userId, string serviceName, decimal amount) =>
        new()
        {
            Id = id,
            UserId = userId,
            ServiceName = serviceName,
            Amount = amount,
            BillingCycle = "monthly",
            NextBillingDate = new DateOnly(2026, 4, 1),
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

    [Fact]
    public async Task GetSubscriptions_WithExistingData_Returns200WithList()
    {
        // Arrange
        var subscriptions = new List<Subscription>
        {
            BuildSubscription(1, TestUserId, "Netflix", 1490m),
            BuildSubscription(2, TestUserId, "Spotify", 980m)
        };
        var mockService = new Mock<ISubscriptionService>();
        mockService
            .Setup(s => s.GetSubscriptionsAsync(TestUserId))
            .ReturnsAsync(subscriptions);

        var controller = CreateController(mockService.Object);

        // Act
        var result = await controller.GetSubscriptions();

        // Assert
        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.StatusCode.Should().Be(200);
        mockService.Verify(s => s.GetSubscriptionsAsync(TestUserId), Times.Once);
    }

    [Fact]
    public async Task GetSubscription_WithValidId_Returns200WithSubscription()
    {
        // Arrange
        var subscription = BuildSubscription(1, TestUserId, "Netflix", 1490m);
        var mockService = new Mock<ISubscriptionService>();
        mockService
            .Setup(s => s.GetSubscriptionByIdAsync(1, TestUserId))
            .ReturnsAsync(subscription);

        var controller = CreateController(mockService.Object);

        // Act
        var result = await controller.GetSubscription(1);

        // Assert
        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.StatusCode.Should().Be(200);

        var response = ok.Value.Should().BeOfType<SubscriptionResponse>().Subject;
        response.ServiceName.Should().Be("Netflix");
        response.Amount.Should().Be(1490m);
    }

    [Fact]
    public async Task GetSubscription_WithNonExistentId_Returns404()
    {
        // Arrange
        var mockService = new Mock<ISubscriptionService>();
        mockService
            .Setup(s => s.GetSubscriptionByIdAsync(999, TestUserId))
            .ReturnsAsync((Subscription?)null);

        var controller = CreateController(mockService.Object);

        // Act
        var result = await controller.GetSubscription(999);

        // Assert
        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task CreateSubscription_WithValidRequest_Returns201()
    {
        // Arrange
        var request = new SubscriptionRequest(
            "Netflix",
            1490m,
            null,
            "monthly",
            new DateOnly(2026, 4, 1),
            "スタンダードプラン",
            true
        );
        var created = BuildSubscription(1, TestUserId, "Netflix", 1490m);

        var mockService = new Mock<ISubscriptionService>();
        mockService
            .Setup(s => s.CreateSubscriptionAsync(It.IsAny<Subscription>()))
            .ReturnsAsync(created);

        var controller = CreateController(mockService.Object);

        // Act
        var result = await controller.CreateSubscription(request);

        // Assert
        var created201 = result.Should().BeOfType<CreatedAtActionResult>().Subject;
        created201.StatusCode.Should().Be(201);

        var response = created201.Value.Should().BeOfType<SubscriptionResponse>().Subject;
        response.ServiceName.Should().Be("Netflix");
        response.Amount.Should().Be(1490m);

        // 金額がdecimal型で正確に渡されていることを確認する
        mockService.Verify(s => s.CreateSubscriptionAsync(
            It.Is<Subscription>(sub =>
                sub.Amount == 1490m &&
                sub.BillingCycle == "monthly" &&
                sub.UserId == TestUserId
            )), Times.Once);
    }

    [Fact]
    public async Task UpdateSubscription_WithValidData_Returns200()
    {
        // Arrange
        var request = new SubscriptionRequest(
            "Netflix Updated",
            1980m,
            null,
            "monthly",
            new DateOnly(2026, 5, 1),
            "プレミアムプラン",
            true
        );
        var updated = BuildSubscription(1, TestUserId, "Netflix Updated", 1980m);

        var mockService = new Mock<ISubscriptionService>();
        mockService
            .Setup(s => s.UpdateSubscriptionAsync(1, TestUserId, It.IsAny<Subscription>()))
            .ReturnsAsync(updated);

        var controller = CreateController(mockService.Object);

        // Act
        var result = await controller.UpdateSubscription(1, request);

        // Assert
        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.StatusCode.Should().Be(200);
    }

    [Fact]
    public async Task UpdateSubscription_WithNonExistentId_Returns404()
    {
        // Arrange
        var request = new SubscriptionRequest(
            "Netflix",
            1490m,
            null,
            "monthly",
            new DateOnly(2026, 4, 1),
            null,
            true
        );
        var mockService = new Mock<ISubscriptionService>();
        mockService
            .Setup(s => s.UpdateSubscriptionAsync(999, TestUserId, It.IsAny<Subscription>()))
            .ReturnsAsync((Subscription?)null);

        var controller = CreateController(mockService.Object);

        // Act
        var result = await controller.UpdateSubscription(999, request);

        // Assert
        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task DeleteSubscription_WithValidId_Returns204()
    {
        // Arrange
        var mockService = new Mock<ISubscriptionService>();
        mockService
            .Setup(s => s.DeleteSubscriptionAsync(1, TestUserId))
            .ReturnsAsync(true);

        var controller = CreateController(mockService.Object);

        // Act
        var result = await controller.DeleteSubscription(1);

        // Assert
        result.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public async Task DeleteSubscription_WithNonExistentId_Returns404()
    {
        // Arrange
        var mockService = new Mock<ISubscriptionService>();
        mockService
            .Setup(s => s.DeleteSubscriptionAsync(999, TestUserId))
            .ReturnsAsync(false);

        var controller = CreateController(mockService.Object);

        // Act
        var result = await controller.DeleteSubscription(999);

        // Assert
        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task GetSubscriptions_EnsuresUserIsolation_OnlyReturnsOwnData()
    {
        // Arrange: ユーザーA, Bが存在するが、コントローラーはユーザーAのIDのみでクエリする
        var userASubscriptions = new List<Subscription>
        {
            BuildSubscription(1, TestUserId, "Netflix", 1490m)
        };
        var mockService = new Mock<ISubscriptionService>();
        mockService
            .Setup(s => s.GetSubscriptionsAsync(TestUserId))
            .ReturnsAsync(userASubscriptions);
        mockService
            .Setup(s => s.GetSubscriptionsAsync("other-user-id"))
            .ReturnsAsync(new List<Subscription>
            {
                BuildSubscription(2, "other-user-id", "Hulu", 1026m)
            });

        var controller = CreateController(mockService.Object);

        // Act
        await controller.GetSubscriptions();

        // Assert: ユーザーAのIDでのみサービスが呼ばれることを確認
        mockService.Verify(s => s.GetSubscriptionsAsync(TestUserId), Times.Once);
        mockService.Verify(s => s.GetSubscriptionsAsync("other-user-id"), Times.Never);
    }
}
