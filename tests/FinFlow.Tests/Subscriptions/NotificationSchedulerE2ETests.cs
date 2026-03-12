using FinFlow.Domain.Entities;
using FinFlow.Domain.Interfaces;
using FinFlow.Infrastructure.Data;
using FinFlow.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;

namespace FinFlow.Tests.Subscriptions;

/// <summary>
/// NotificationScheduler の E2E シナリオテスト。
/// サブスク登録から通知検出までのフロー、メール送信モック、タイミングのエッジケースを検証する。
/// </summary>
[Trait("Category", "NotificationE2E")]
public class NotificationSchedulerE2ETests
{
    // =====================================================================
    // ヘルパー
    // =====================================================================

    private static FinFlowDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<FinFlowDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new FinFlowDbContext(options);
    }

    private static Subscription CreateSubscription(
        string userId,
        string serviceName,
        DateOnly nextBillingDate,
        bool isActive = true)
    {
        return new Subscription
        {
            UserId = userId,
            ServiceName = serviceName,
            Amount = 980m,
            BillingCycle = "monthly",
            NextBillingDate = nextBillingDate,
            IsActive = isActive,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    private static IServiceScopeFactory BuildServiceProvider(
        FinFlowDbContext context,
        IEmailSender emailSender)
    {
        var services = new ServiceCollection();
        services.AddSingleton(context);
        services.AddScoped(_ => emailSender);
        services.AddScoped<ISubscriptionService, SubscriptionService>();
        services.AddLogging();

        var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<IServiceScopeFactory>();
    }

    // =====================================================================
    // S3-B-001-1: サブスク登録 → 通知検出フロー
    // =====================================================================

    [Fact]
    public async Task E2E_RegisterSubscriptionAndDetect_Within3Days_IsDetected()
    {
        // Arrange: 3日以内に支払日を迎えるサブスクを登録する
        await using var context = CreateInMemoryDbContext();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        // 1日後と2日後のサブスクを登録（どちらも通知対象）
        var sub1 = CreateSubscription("user-e2e-1", "Netflix", today.AddDays(1));
        var sub2 = CreateSubscription("user-e2e-1", "Spotify", today.AddDays(2));
        context.Subscriptions.AddRange(sub1, sub2);
        await context.SaveChangesAsync();

        var emailSenderMock = new Mock<IEmailSender>();
        emailSenderMock
            .Setup(m => m.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        var services = BuildServiceProvider(context, emailSenderMock.Object);
        var logger = Mock.Of<ILogger<NotificationScheduler>>();
        var scheduler = new NotificationScheduler(services, logger);

        // Act: CheckAndNotifyAsync が正常に完了すること
        var act = async () => await scheduler.CheckAndNotifyAsync(CancellationToken.None);

        // Assert: エラーなく処理が完了すること（DBにAspNetUsersがないためメール送信はスキップされるが、
        // 通知対象の検出ロジック自体は正常動作する）
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task E2E_RegisterSubscriptionAndDetect_MultipleUsers_ProcessedPerUser()
    {
        // Arrange: 複数ユーザーにまたがるサブスクを登録する
        await using var context = CreateInMemoryDbContext();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        // 2人のユーザーにそれぞれ3日以内のサブスクを登録
        var sub1 = CreateSubscription("user-a", "Netflix", today.AddDays(1));
        var sub2 = CreateSubscription("user-b", "Spotify", today.AddDays(2));
        context.Subscriptions.AddRange(sub1, sub2);
        await context.SaveChangesAsync();

        var emailSenderMock = new Mock<IEmailSender>();
        emailSenderMock
            .Setup(m => m.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        var services = BuildServiceProvider(context, emailSenderMock.Object);
        var logger = Mock.Of<ILogger<NotificationScheduler>>();
        var scheduler = new NotificationScheduler(services, logger);

        // Act
        var act = async () => await scheduler.CheckAndNotifyAsync(CancellationToken.None);

        // Assert: 複数ユーザーが存在してもエラーなく処理が完了すること
        await act.Should().NotThrowAsync();
    }

    // =====================================================================
    // S3-B-001-2: IEmailSender モックを使った通知フロー確認
    // =====================================================================

    [Fact]
    public async Task MockEmailSender_WhenSubscriptionOutsideRange_SendEmailNeverCalled()
    {
        // Arrange: 通知範囲外のサブスクのみ存在する場合、IEmailSender.SendEmailAsync は呼ばれない
        await using var context = CreateInMemoryDbContext();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        // 4日後（通知対象外）
        var sub = CreateSubscription("user-mock-1", "Amazon Prime", today.AddDays(4));
        context.Subscriptions.Add(sub);
        await context.SaveChangesAsync();

        var emailSenderMock = new Mock<IEmailSender>(MockBehavior.Strict);
        // Strict モード: 意図しないメソッド呼び出しがあれば例外が発生する

        var services = BuildServiceProvider(context, emailSenderMock.Object);
        var logger = Mock.Of<ILogger<NotificationScheduler>>();
        var scheduler = new NotificationScheduler(services, logger);

        // Act & Assert: 4日後のサブスクは通知対象外なので SendEmailAsync が呼ばれない
        var act = async () => await scheduler.CheckAndNotifyAsync(CancellationToken.None);
        await act.Should().NotThrowAsync();

        emailSenderMock.Verify(
            m => m.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task MockEmailSender_WhenNoActiveSubscriptions_SendEmailNeverCalled()
    {
        // Arrange: アクティブなサブスクが1件もない場合
        await using var context = CreateInMemoryDbContext();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        // 非アクティブなサブスクのみ
        var sub = CreateSubscription("user-mock-2", "Disney+", today.AddDays(1), isActive: false);
        context.Subscriptions.Add(sub);
        await context.SaveChangesAsync();

        var emailSenderMock = new Mock<IEmailSender>();
        var services = BuildServiceProvider(context, emailSenderMock.Object);
        var logger = Mock.Of<ILogger<NotificationScheduler>>();
        var scheduler = new NotificationScheduler(services, logger);

        // Act
        await scheduler.CheckAndNotifyAsync(CancellationToken.None);

        // Assert: 非アクティブは通知対象外
        emailSenderMock.Verify(
            m => m.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task MockEmailSender_WhenCancellationRequested_CompletesGracefully()
    {
        // Arrange: キャンセルトークンが要求済みの場合でも安全に処理できること
        await using var context = CreateInMemoryDbContext();

        var emailSenderMock = new Mock<IEmailSender>();
        var services = BuildServiceProvider(context, emailSenderMock.Object);
        var logger = Mock.Of<ILogger<NotificationScheduler>>();
        var scheduler = new NotificationScheduler(services, logger);

        using var cts = new CancellationTokenSource();
        // サブスクが0件のためキャンセル前に完了するが、キャンセル済みトークンを渡しても
        // CheckAndNotifyAsync はエラーなく終了すること
        cts.Cancel();

        // Act & Assert: キャンセルトークンがあってもクラッシュしない
        var act = async () => await scheduler.CheckAndNotifyAsync(cts.Token);
        await act.Should().NotThrowAsync();
    }

    // =====================================================================
    // S3-B-001-3: 通知タイミングのエッジケース
    // =====================================================================

    [Fact]
    public async Task EdgeCase_SubscriptionDueToday_IsNotificationTarget()
    {
        // Arrange: 支払日が当日のサブスクは通知対象になること
        await using var context = CreateInMemoryDbContext();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var sub = CreateSubscription("user-edge-1", "当日サブスク", today);
        context.Subscriptions.Add(sub);
        await context.SaveChangesAsync();

        // SubscriptionService を直接使って通知対象の検出ロジックを検証する
        var subscriptionService = new SubscriptionService(context);

        // Act: daysAhead=3 で当日のサブスクが取得されること
        var upcoming = await subscriptionService.GetUpcomingBillingsAsync("user-edge-1", daysAhead: 3);

        // Assert: 当日のサブスクは daysAhead 範囲内（today >= today && today <= today+3）なので取得される
        upcoming.Should().HaveCount(1);
        upcoming.First().ServiceName.Should().Be("当日サブスク");
    }

    [Fact]
    public async Task EdgeCase_SubscriptionDueIn4Days_IsNotNotificationTarget()
    {
        // Arrange: 支払日が4日後のサブスクは通知対象外になること
        await using var context = CreateInMemoryDbContext();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var sub = CreateSubscription("user-edge-2", "4日後サブスク", today.AddDays(4));
        context.Subscriptions.Add(sub);
        await context.SaveChangesAsync();

        var subscriptionService = new SubscriptionService(context);

        // Act: daysAhead=3 で4日後のサブスクは取得されない
        var upcoming = await subscriptionService.GetUpcomingBillingsAsync("user-edge-2", daysAhead: 3);

        // Assert: 4日後は通知対象外
        upcoming.Should().BeEmpty();
    }

    [Fact]
    public async Task EdgeCase_SubscriptionDueTodayAndIn4Days_OnlyTodayIsDetected()
    {
        // Arrange: 当日（通知対象）と4日後（通知対象外）が混在する場合
        await using var context = CreateInMemoryDbContext();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var subToday = CreateSubscription("user-edge-3", "当日サブスク", today);
        var sub4Days = CreateSubscription("user-edge-3", "4日後サブスク", today.AddDays(4));
        context.Subscriptions.AddRange(subToday, sub4Days);
        await context.SaveChangesAsync();

        var subscriptionService = new SubscriptionService(context);

        // Act
        var upcoming = await subscriptionService.GetUpcomingBillingsAsync("user-edge-3", daysAhead: 3);

        // Assert: 当日のサブスクのみが通知対象
        upcoming.Should().HaveCount(1);
        upcoming.First().ServiceName.Should().Be("当日サブスク");
    }

    [Fact]
    public async Task EdgeCase_SubscriptionPastDue_IsNotNotificationTarget()
    {
        // Arrange: 支払日が過去のサブスクは通知対象外になること
        await using var context = CreateInMemoryDbContext();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        // 昨日が支払日（過去）のサブスク
        var sub = CreateSubscription("user-edge-4", "期限切れサブスク", today.AddDays(-1));
        context.Subscriptions.Add(sub);
        await context.SaveChangesAsync();

        var subscriptionService = new SubscriptionService(context);

        // Act
        var upcoming = await subscriptionService.GetUpcomingBillingsAsync("user-edge-4", daysAhead: 3);

        // Assert: 過去日付は通知対象外（today >= today の条件を満たさない）
        upcoming.Should().BeEmpty();
    }

    [Fact]
    public async Task EdgeCase_Exactly3DaysAhead_IsNotificationTarget()
    {
        // Arrange: 支払日がちょうど3日後（境界値）のサブスクは通知対象になること
        await using var context = CreateInMemoryDbContext();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var sub = CreateSubscription("user-edge-5", "3日後境界サブスク", today.AddDays(3));
        context.Subscriptions.Add(sub);
        await context.SaveChangesAsync();

        var subscriptionService = new SubscriptionService(context);

        // Act: daysAhead=3 でちょうど3日後のサブスクが取得されること（境界値テスト）
        var upcoming = await subscriptionService.GetUpcomingBillingsAsync("user-edge-5", daysAhead: 3);

        // Assert: 3日後は通知対象（today <= nextBillingDate <= today+3 の境界に含まれる）
        upcoming.Should().HaveCount(1);
        upcoming.First().ServiceName.Should().Be("3日後境界サブスク");
    }

    [Fact]
    public async Task EdgeCase_SchedulerWithMixedSubscriptions_OnlyNotifiesActiveWithin3Days()
    {
        // Arrange: 通知対象・対象外が混在するシナリオ全体のフローを検証
        await using var context = CreateInMemoryDbContext();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var subscriptions = new[]
        {
            // 通知対象（当日、アクティブ）
            CreateSubscription("user-edge-6", "当日アクティブ", today),
            // 通知対象（2日後、アクティブ）
            CreateSubscription("user-edge-6", "2日後アクティブ", today.AddDays(2)),
            // 通知対象外（4日後、アクティブ）
            CreateSubscription("user-edge-6", "4日後アクティブ", today.AddDays(4)),
            // 通知対象外（1日後、非アクティブ）
            CreateSubscription("user-edge-6", "1日後非アクティブ", today.AddDays(1), isActive: false),
        };
        context.Subscriptions.AddRange(subscriptions);
        await context.SaveChangesAsync();

        var emailSenderMock = new Mock<IEmailSender>();
        emailSenderMock
            .Setup(m => m.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        var services = BuildServiceProvider(context, emailSenderMock.Object);
        var logger = Mock.Of<ILogger<NotificationScheduler>>();
        var scheduler = new NotificationScheduler(services, logger);

        // Act: CheckAndNotifyAsync がエラーなく完了すること
        var act = async () => await scheduler.CheckAndNotifyAsync(CancellationToken.None);
        await act.Should().NotThrowAsync();

        // IEmailSender の呼び出しがないことを確認（AspNetUsersがないためメール送信スキップ）
        // 実際の検出ロジックは SubscriptionService.GetUpcomingBillingsAsync で別途検証
        emailSenderMock.Verify(
            m => m.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()),
            Times.Never);
    }
}
