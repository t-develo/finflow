using FinFlow.Domain.Entities;
using FinFlow.Domain.Exceptions;
using FinFlow.Infrastructure.Data;
using FinFlow.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace FinFlow.Tests.Subscriptions;

/// <summary>
/// SubscriptionService のユニットテスト。
/// InMemory DBを使い、サービス層のビジネスロジックを検証する。
/// </summary>
[Trait("Category", "SubscriptionService")]
public class SubscriptionServiceTests : IDisposable
{
    private readonly FinFlowDbContext _dbContext;
    private readonly SubscriptionService _service;
    private const string TestUserId = "test-user-se2";
    private const string OtherUserId = "other-user-se2";

    public SubscriptionServiceTests()
    {
        var options = new DbContextOptionsBuilder<FinFlowDbContext>()
            .UseInMemoryDatabase($"SubscriptionTest_{Guid.NewGuid()}")
            .Options;
        _dbContext = new FinFlowDbContext(options);
        _dbContext.Database.EnsureCreated();
        _service = new SubscriptionService(_dbContext);
    }

    public void Dispose() => _dbContext.Dispose();

    // =====================================================================
    // GetSubscriptionsAsync のテスト
    // =====================================================================

    [Fact]
    public async Task GetSubscriptionsAsync_WithUserHavingSubscriptions_ReturnsOnlyUserSubscriptions()
    {
        // Arrange: 別ユーザーのサブスクが混在する状況
        _dbContext.Subscriptions.AddRange(
            CreateSubscription(TestUserId, "Netflix", 1490m, new DateOnly(2026, 4, 1)),
            CreateSubscription(TestUserId, "Spotify", 980m, new DateOnly(2026, 4, 10)),
            CreateSubscription(OtherUserId, "Hulu", 1026m, new DateOnly(2026, 4, 5))
        );
        await _dbContext.SaveChangesAsync();

        // Act
        var subscriptions = await _service.GetSubscriptionsAsync(TestUserId);

        // Assert: 他ユーザーのデータは取得されない
        subscriptions.Should().HaveCount(2);
        subscriptions.Should().AllSatisfy(s => s.UserId.Should().Be(TestUserId));
    }

    [Fact]
    public async Task GetSubscriptionsAsync_ReturnsSubscriptionsOrderedByNextBillingDateAscending()
    {
        // Arrange: nextBillingDate が異なるサブスクを登録
        _dbContext.Subscriptions.AddRange(
            CreateSubscription(TestUserId, "Netflix", 1490m, new DateOnly(2026, 5, 1)),
            CreateSubscription(TestUserId, "Spotify", 980m, new DateOnly(2026, 4, 1)),
            CreateSubscription(TestUserId, "Amazon", 600m, new DateOnly(2026, 4, 15))
        );
        await _dbContext.SaveChangesAsync();

        // Act
        var subscriptions = (await _service.GetSubscriptionsAsync(TestUserId)).ToList();

        // Assert: nextBillingDate 昇順
        subscriptions[0].ServiceName.Should().Be("Spotify");
        subscriptions[1].ServiceName.Should().Be("Amazon");
        subscriptions[2].ServiceName.Should().Be("Netflix");
    }

    // =====================================================================
    // GetSubscriptionByIdAsync のテスト
    // =====================================================================

    [Fact]
    public async Task GetSubscriptionByIdAsync_WithValidIdAndUser_ReturnsSubscription()
    {
        // Arrange
        var subscription = CreateSubscription(TestUserId, "Netflix", 1490m, new DateOnly(2026, 4, 1));
        _dbContext.Subscriptions.Add(subscription);
        await _dbContext.SaveChangesAsync();

        // Act
        var found = await _service.GetSubscriptionByIdAsync(subscription.Id, TestUserId);

        // Assert
        found.Should().NotBeNull();
        found!.ServiceName.Should().Be("Netflix");
        found.Amount.Should().Be(1490m);
    }

    [Fact]
    public async Task GetSubscriptionByIdAsync_WithDifferentUser_ReturnsNull()
    {
        // Arrange: 別ユーザーのサブスクは取得できない（セキュリティ確認）
        var subscription = CreateSubscription(OtherUserId, "Netflix", 1490m, new DateOnly(2026, 4, 1));
        _dbContext.Subscriptions.Add(subscription);
        await _dbContext.SaveChangesAsync();

        // Act
        var found = await _service.GetSubscriptionByIdAsync(subscription.Id, TestUserId);

        // Assert
        found.Should().BeNull();
    }

    // =====================================================================
    // CreateSubscriptionAsync のテスト
    // =====================================================================

    [Fact]
    public async Task CreateSubscriptionAsync_WithValidData_ReturnsCreatedSubscription()
    {
        // Arrange: システムカテゴリ（食費: ID=1）はシードデータで存在する
        var subscription = new Subscription
        {
            UserId = TestUserId,
            ServiceName = "Netflix",
            Amount = 1490m,
            CategoryId = 1,
            BillingCycle = "monthly",
            NextBillingDate = new DateOnly(2026, 4, 1),
            IsActive = true
        };

        // Act
        var created = await _service.CreateSubscriptionAsync(subscription);

        // Assert
        created.Id.Should().BeGreaterThan(0);
        created.ServiceName.Should().Be("Netflix");
        created.Amount.Should().Be(1490m);
        created.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        created.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task CreateSubscriptionAsync_WithNullCategoryId_SucceedsWithoutCategory()
    {
        // Arrange: categoryId は省略可能
        var subscription = new Subscription
        {
            UserId = TestUserId,
            ServiceName = "Netflix",
            Amount = 1490m,
            CategoryId = null,
            BillingCycle = "monthly",
            NextBillingDate = new DateOnly(2026, 4, 1),
            IsActive = true
        };

        // Act
        var created = await _service.CreateSubscriptionAsync(subscription);

        // Assert: カテゴリなしでも正常に作成される
        created.Id.Should().BeGreaterThan(0);
        created.CategoryId.Should().BeNull();
    }

    [Fact]
    public async Task CreateSubscriptionAsync_WithNonExistentCategoryId_ThrowsValidationException()
    {
        // Arrange: 存在しないカテゴリID
        var subscription = new Subscription
        {
            UserId = TestUserId,
            ServiceName = "Netflix",
            Amount = 1490m,
            CategoryId = 99999,
            BillingCycle = "monthly",
            NextBillingDate = new DateOnly(2026, 4, 1),
            IsActive = true
        };

        // Act & Assert: 存在しないカテゴリIDはValidationExceptionをスローする
        await _service.Invoking(s => s.CreateSubscriptionAsync(subscription))
            .Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task CreateSubscriptionAsync_AmountIsDecimalType_PreservesExactValue()
    {
        // Arrange: decimal 型で正確な金額を保存する
        var exactAmount = 1490.50m;
        var subscription = new Subscription
        {
            UserId = TestUserId,
            ServiceName = "Netflix",
            Amount = exactAmount,
            BillingCycle = "monthly",
            NextBillingDate = new DateOnly(2026, 4, 1),
            IsActive = true
        };

        // Act
        var created = await _service.CreateSubscriptionAsync(subscription);

        // Assert: decimal 型で正確に保存されること（float/double では丸め誤差が生じる）
        created.Amount.Should().Be(exactAmount);
    }

    // =====================================================================
    // UpdateSubscriptionAsync のテスト
    // =====================================================================

    [Fact]
    public async Task UpdateSubscriptionAsync_WithValidData_ReturnsUpdatedSubscription()
    {
        // Arrange
        var subscription = CreateSubscription(TestUserId, "Netflix", 1490m, new DateOnly(2026, 4, 1));
        subscription.CategoryId = 1; // 食費
        _dbContext.Subscriptions.Add(subscription);
        await _dbContext.SaveChangesAsync();

        var updatedData = new Subscription
        {
            ServiceName = "Netflix Premium",
            Amount = 1980m,
            CategoryId = 3, // 娯楽
            BillingCycle = "monthly",
            NextBillingDate = new DateOnly(2026, 5, 1),
            IsActive = true
        };

        // Act
        var result = await _service.UpdateSubscriptionAsync(subscription.Id, TestUserId, updatedData);

        // Assert
        result.Should().NotBeNull();
        result!.ServiceName.Should().Be("Netflix Premium");
        result.Amount.Should().Be(1980m);
    }

    [Fact]
    public async Task UpdateSubscriptionAsync_WithNonExistentId_ReturnsNull()
    {
        // Arrange
        const int nonExistentId = 99999;
        var updatedData = new Subscription
        {
            ServiceName = "Netflix",
            Amount = 1490m,
            BillingCycle = "monthly",
            NextBillingDate = new DateOnly(2026, 4, 1),
            IsActive = true
        };

        // Act
        var result = await _service.UpdateSubscriptionAsync(nonExistentId, TestUserId, updatedData);

        // Assert
        result.Should().BeNull();
    }

    // =====================================================================
    // DeleteSubscriptionAsync のテスト
    // =====================================================================

    [Fact]
    public async Task DeleteSubscriptionAsync_WithValidId_ReturnsTrueAndRemovesSubscription()
    {
        // Arrange
        var subscription = CreateSubscription(TestUserId, "Netflix", 1490m, new DateOnly(2026, 4, 1));
        _dbContext.Subscriptions.Add(subscription);
        await _dbContext.SaveChangesAsync();

        // Act
        var deleted = await _service.DeleteSubscriptionAsync(subscription.Id, TestUserId);

        // Assert
        deleted.Should().BeTrue();
        var found = await _dbContext.Subscriptions.FindAsync(subscription.Id);
        found.Should().BeNull();
    }

    [Fact]
    public async Task DeleteSubscriptionAsync_WithNonExistentId_ReturnsFalse()
    {
        // Arrange
        const int nonExistentId = 99999;

        // Act
        var deleted = await _service.DeleteSubscriptionAsync(nonExistentId, TestUserId);

        // Assert
        deleted.Should().BeFalse();
    }

    [Fact]
    public async Task UpdateSubscriptionAsync_WithDifferentUser_ReturnsNull()
    {
        // Arrange: 別ユーザーのサブスクは更新できない（UserId分離の確認）
        var subscription = CreateSubscription(OtherUserId, "Netflix", 1490m, new DateOnly(2026, 4, 1));
        _dbContext.Subscriptions.Add(subscription);
        await _dbContext.SaveChangesAsync();

        var updatedData = new Subscription
        {
            ServiceName = "Netflix Premium",
            Amount = 1980m,
            BillingCycle = "monthly",
            NextBillingDate = new DateOnly(2026, 5, 1),
            IsActive = true
        };

        // Act
        var result = await _service.UpdateSubscriptionAsync(subscription.Id, TestUserId, updatedData);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task DeleteSubscriptionAsync_WithDifferentUser_ReturnsFalse()
    {
        // Arrange: 別ユーザーのサブスクは削除できない（UserId分離の確認）
        var subscription = CreateSubscription(OtherUserId, "Netflix", 1490m, new DateOnly(2026, 4, 1));
        _dbContext.Subscriptions.Add(subscription);
        await _dbContext.SaveChangesAsync();

        // Act
        var deleted = await _service.DeleteSubscriptionAsync(subscription.Id, TestUserId);

        // Assert
        deleted.Should().BeFalse();
        var found = await _dbContext.Subscriptions.FindAsync(subscription.Id);
        found.Should().NotBeNull();
    }

    // =====================================================================
    // GetUpcomingBillingsAsync のテスト
    // =====================================================================

    [Fact]
    public async Task GetUpcomingBillingsAsync_ReturnsOnlyActiveSubscriptionsWithinRange()
    {
        // Arrange
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        _dbContext.Subscriptions.AddRange(
            // 3日以内 + アクティブ → 取得される
            CreateSubscription(TestUserId, "Netflix", 1490m, today.AddDays(2)),
            // 3日以内 + 非アクティブ → 取得されない
            CreateInactiveSubscription(TestUserId, "Spotify", 980m, today.AddDays(1)),
            // 4日後 → 取得されない（デフォルト3日以内）
            CreateSubscription(TestUserId, "Amazon", 600m, today.AddDays(4))
        );
        await _dbContext.SaveChangesAsync();

        // Act
        var upcoming = await _service.GetUpcomingBillingsAsync(TestUserId, daysAhead: 3);

        // Assert
        upcoming.Should().HaveCount(1);
        upcoming.First().ServiceName.Should().Be("Netflix");
    }

    // =====================================================================
    // ヘルパー
    // =====================================================================

    private static Subscription CreateSubscription(string userId, string serviceName, decimal amount, DateOnly nextBillingDate) =>
        new()
        {
            UserId = userId,
            ServiceName = serviceName,
            Amount = amount,
            BillingCycle = "monthly",
            NextBillingDate = nextBillingDate,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

    private static Subscription CreateInactiveSubscription(string userId, string serviceName, decimal amount, DateOnly nextBillingDate) =>
        new()
        {
            UserId = userId,
            ServiceName = serviceName,
            Amount = amount,
            BillingCycle = "monthly",
            NextBillingDate = nextBillingDate,
            IsActive = false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
}
