using FinFlow.Domain.Entities;
using FinFlow.Infrastructure.Data;
using FinFlow.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace FinFlow.Tests.Dashboard;

/// <summary>
/// ダッシュボードAPIの統合テスト。
/// 複数の支出・サブスクデータが存在する場合のサマリ集計、
/// および前月データなしの場合のハンドリングを検証する。
/// </summary>
[Trait("Category", "DashboardIntegration")]
public class DashboardIntegrationTests : IDisposable
{
    private readonly FinFlowDbContext _context;
    private readonly DashboardService _dashboardService;
    private const string TestUserId = "user-dashboard-integration";
    private const string OtherUserId = "user-other-dashboard";

    public DashboardIntegrationTests()
    {
        var options = new DbContextOptionsBuilder<FinFlowDbContext>()
            .UseInMemoryDatabase($"DashboardIntegrationTest_{Guid.NewGuid()}")
            .Options;
        _context = new FinFlowDbContext(options);
        _context.Database.EnsureCreated();

        var reportService = new ReportService(_context);
        var subscriptionService = new SubscriptionService(_context);
        var expenseService = new ExpenseService(_context);
        _dashboardService = new DashboardService(reportService, subscriptionService, expenseService);
    }

    public void Dispose() => _context.Dispose();

    private static Expense BuildExpense(int id, string userId, decimal amount, DateOnly date, int? categoryId = null, string? description = null) =>
        new()
        {
            Id = id,
            UserId = userId,
            Amount = amount,
            Date = date,
            CategoryId = categoryId,
            Description = description,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

    private static Subscription BuildSubscription(int id, string userId, string serviceName, decimal amount, DateOnly nextBillingDate, bool isActive = true) =>
        new()
        {
            Id = id,
            UserId = userId,
            ServiceName = serviceName,
            Amount = amount,
            BillingCycle = "monthly",
            NextBillingDate = nextBillingDate,
            IsActive = isActive,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

    // =====================================================================
    // S3-B-003-1: 複数の支出・サブスクデータがある場合のダッシュボードサマリ
    // =====================================================================

    [Fact]
    public async Task Integration_WithMultipleExpensesAndSubscriptions_ReturnsCombinedSummary()
    {
        // Arrange: 当月に複数カテゴリの支出と複数のサブスクを登録する
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var currentYear = today.Year;
        var currentMonth = today.Month;

        // 複数支出（シードカテゴリ: 食費=1, 交通費=2, 娯楽=3）
        var expenses = new List<Expense>
        {
            BuildExpense(1, TestUserId, 15000m, new DateOnly(currentYear, currentMonth, 1), categoryId: 1, description: "スーパー"),
            BuildExpense(2, TestUserId, 8000m,  new DateOnly(currentYear, currentMonth, 5), categoryId: 2, description: "定期券"),
            BuildExpense(3, TestUserId, 12000m, new DateOnly(currentYear, currentMonth, 10), categoryId: 1, description: "外食"),
            BuildExpense(4, TestUserId, 5000m,  new DateOnly(currentYear, currentMonth, 15), categoryId: 3, description: "映画"),
            BuildExpense(5, TestUserId, 20000m, new DateOnly(currentYear, currentMonth, 20), categoryId: 1, description: "食材まとめ買い"),
        };

        // 30日以内のサブスク
        var subscriptions = new List<Subscription>
        {
            BuildSubscription(1, TestUserId, "Netflix",     1490m, today.AddDays(5)),
            BuildSubscription(2, TestUserId, "Spotify",     980m,  today.AddDays(10)),
            BuildSubscription(3, TestUserId, "Amazon Prime", 600m,  today.AddDays(25)),
            // 30日超は含まれない
            BuildSubscription(4, TestUserId, "Adobe CC",    6000m, today.AddDays(45)),
        };

        _context.Expenses.AddRange(expenses);
        _context.Subscriptions.AddRange(subscriptions);
        await _context.SaveChangesAsync();

        // Act
        var result = await _dashboardService.GetDashboardSummaryAsync(TestUserId);

        // Assert: 当月合計が正しいこと（15000+8000+12000+5000+20000 = 60000円）
        result.CurrentMonth.TotalAmount.Should().Be(60000m);
        result.CurrentMonth.TotalCount.Should().Be(5);

        // 30日以内のサブスクが3件含まれること
        result.UpcomingSubscriptions.Should().HaveCount(3);
        result.UpcomingSubscriptions.Select(s => s.ServiceName)
            .Should().Contain(new[] { "Netflix", "Spotify", "Amazon Prime" })
            .And.NotContain("Adobe CC");
    }

    [Fact]
    public async Task Integration_WithMultipleExpenses_TopCategoriesAreSortedByAmountDescending()
    {
        // Arrange: 異なる金額のカテゴリ支出を登録する
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var currentYear = today.Year;
        var currentMonth = today.Month;

        var expenses = new List<Expense>
        {
            // 食費(1): 3万円（最大）
            BuildExpense(10, TestUserId, 30000m, new DateOnly(currentYear, currentMonth, 1), categoryId: 1),
            // 交通費(2): 1万円（中間）
            BuildExpense(11, TestUserId, 10000m, new DateOnly(currentYear, currentMonth, 2), categoryId: 2),
            // 娯楽(3): 5千円（最小）
            BuildExpense(12, TestUserId, 5000m,  new DateOnly(currentYear, currentMonth, 3), categoryId: 3),
        };
        _context.Expenses.AddRange(expenses);
        await _context.SaveChangesAsync();

        // Act
        var result = await _dashboardService.GetDashboardSummaryAsync(TestUserId);

        // Assert: TopCategories が金額降順であること
        var topCategories = result.TopCategories.ToList();
        topCategories.Should().HaveCount(3);
        topCategories[0].Amount.Should().Be(30000m); // 食費
        topCategories[1].Amount.Should().Be(10000m); // 交通費
        topCategories[2].Amount.Should().Be(5000m);  // 娯楽
    }

    [Fact]
    public async Task Integration_WithMoreThan5Categories_TopCategoriesReturnsOnly5()
    {
        // Arrange: 6カテゴリ以上の支出を登録する（TopCategories は最大5件）
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var currentYear = today.Year;
        var currentMonth = today.Month;

        // シードカテゴリ ID 1~8 の全てに支出を設定する
        var expenses = Enumerable.Range(1, 8)
            .Select(catId => BuildExpense(catId + 100, TestUserId, catId * 10000m, new DateOnly(currentYear, currentMonth, catId), categoryId: catId))
            .ToList();
        _context.Expenses.AddRange(expenses);
        await _context.SaveChangesAsync();

        // Act
        var result = await _dashboardService.GetDashboardSummaryAsync(TestUserId);

        // Assert: TopCategories は上位5件のみ
        result.TopCategories.Should().HaveCount(5);
    }

    [Fact]
    public async Task Integration_RecentExpenses_AreReturnedInDateDescendingOrder()
    {
        // Arrange: 異なる日付の支出を登録する
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var expenses = new List<Expense>
        {
            BuildExpense(20, TestUserId, 1000m, today.AddDays(-4), description: "4日前"),
            BuildExpense(21, TestUserId, 2000m, today.AddDays(-3), description: "3日前"),
            BuildExpense(22, TestUserId, 3000m, today.AddDays(-2), description: "2日前"),
            BuildExpense(23, TestUserId, 4000m, today.AddDays(-1), description: "1日前"),
            BuildExpense(24, TestUserId, 5000m, today,             description: "本日"),
        };
        _context.Expenses.AddRange(expenses);
        await _context.SaveChangesAsync();

        // Act
        var result = await _dashboardService.GetDashboardSummaryAsync(TestUserId);

        // Assert: 直近5件が日付降順で返ること
        var recentExpenses = result.RecentExpenses.ToList();
        recentExpenses.Should().HaveCount(5);
        recentExpenses[0].Amount.Should().Be(5000m); // 本日
        recentExpenses[4].Amount.Should().Be(1000m); // 4日前
    }

    [Fact]
    public async Task Integration_WithUserIsolation_OtherUsersDataExcluded()
    {
        // Arrange: テストユーザーと別ユーザーにデータを設定する
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var currentYear = today.Year;
        var currentMonth = today.Month;

        var expenses = new List<Expense>
        {
            // テストユーザー: 30000円
            BuildExpense(30, TestUserId,  30000m, new DateOnly(currentYear, currentMonth, 1)),
            // 別ユーザー: 200000円（集計に混入しないこと）
            BuildExpense(31, OtherUserId, 200000m, new DateOnly(currentYear, currentMonth, 1)),
        };
        var subscriptions = new List<Subscription>
        {
            // テストユーザー: 30日以内のサブスク
            BuildSubscription(10, TestUserId,  "Netflix", 1490m, today.AddDays(5)),
            // 別ユーザー: 30日以内のサブスク（別ユーザーのため含まれない）
            BuildSubscription(11, OtherUserId, "Hulu",    1026m, today.AddDays(3)),
        };

        _context.Expenses.AddRange(expenses);
        _context.Subscriptions.AddRange(subscriptions);
        await _context.SaveChangesAsync();

        // Act
        var result = await _dashboardService.GetDashboardSummaryAsync(TestUserId);

        // Assert: テストユーザーのデータのみ
        result.CurrentMonth.TotalAmount.Should().Be(30000m);
        result.UpcomingSubscriptions.Should().HaveCount(1);
        result.UpcomingSubscriptions.First().ServiceName.Should().Be("Netflix");
    }

    [Fact]
    public async Task Integration_UpcomingSubscriptions_DaysUntilBillingIsCorrect()
    {
        // Arrange: 特定の日数後のサブスクを登録する
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var subscriptions = new List<Subscription>
        {
            BuildSubscription(20, TestUserId, "5日後サブスク", 1490m, today.AddDays(5)),
            BuildSubscription(21, TestUserId, "15日後サブスク", 980m, today.AddDays(15)),
        };
        _context.Subscriptions.AddRange(subscriptions);
        await _context.SaveChangesAsync();

        // Act
        var result = await _dashboardService.GetDashboardSummaryAsync(TestUserId);

        // Assert: DaysUntilBilling が正確に計算されていること
        var upcoming = result.UpcomingSubscriptions.OrderBy(s => s.DaysUntilBilling).ToList();
        upcoming.Should().HaveCount(2);
        upcoming[0].ServiceName.Should().Be("5日後サブスク");
        upcoming[0].DaysUntilBilling.Should().Be(5);
        upcoming[1].ServiceName.Should().Be("15日後サブスク");
        upcoming[1].DaysUntilBilling.Should().Be(15);
    }

    // =====================================================================
    // S3-B-003-2: 前月データなしの場合のハンドリング
    // =====================================================================

    [Fact]
    public async Task Integration_WithNoPreviousMonthData_PreviousMonthIsNull()
    {
        // Arrange: 当月データのみ存在し、前月データは存在しない
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var expense = BuildExpense(40, TestUserId, 10000m, new DateOnly(today.Year, today.Month, 1));
        _context.Expenses.Add(expense);
        await _context.SaveChangesAsync();

        // Act
        var result = await _dashboardService.GetDashboardSummaryAsync(TestUserId);

        // Assert: 前月データなしの場合 PreviousMonth と MonthOverMonthChange が null であること
        result.PreviousMonth.Should().BeNull();
        result.MonthOverMonthChange.Should().BeNull();
    }

    [Fact]
    public async Task Integration_WithNoPreviousMonthData_CurrentMonthStillReturnedCorrectly()
    {
        // Arrange: 前月データなし、当月のみ
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var currentYear = today.Year;
        var currentMonth = today.Month;

        var expenses = new List<Expense>
        {
            BuildExpense(50, TestUserId, 25000m, new DateOnly(currentYear, currentMonth, 5), categoryId: 1),
            BuildExpense(51, TestUserId, 15000m, new DateOnly(currentYear, currentMonth, 10), categoryId: 2),
        };
        _context.Expenses.AddRange(expenses);
        await _context.SaveChangesAsync();

        // Act
        var result = await _dashboardService.GetDashboardSummaryAsync(TestUserId);

        // Assert: 前月なしでも当月の集計は正常に返ること
        result.CurrentMonth.TotalAmount.Should().Be(40000m);
        result.CurrentMonth.TotalCount.Should().Be(2);
        result.CurrentMonth.Year.Should().Be(currentYear);
        result.CurrentMonth.Month.Should().Be(currentMonth);
        result.PreviousMonth.Should().BeNull();
        result.MonthOverMonthChange.Should().BeNull();
    }

    [Fact]
    public async Task Integration_WithBothCurrentAndPreviousMonthData_MonthOverMonthCalculatedCorrectly()
    {
        // Arrange: 前月と当月の両方にデータを設定する
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var currentYear = today.Year;
        var currentMonth = today.Month;
        var (previousYear, previousMonth) = currentMonth == 1
            ? (currentYear - 1, 12)
            : (currentYear, currentMonth - 1);

        var expenses = new List<Expense>
        {
            // 当月: 120000円
            BuildExpense(60, TestUserId, 120000m, new DateOnly(currentYear, currentMonth, 1)),
            // 前月: 100000円
            BuildExpense(61, TestUserId, 100000m, new DateOnly(previousYear, previousMonth, 1)),
        };
        _context.Expenses.AddRange(expenses);
        await _context.SaveChangesAsync();

        // Act
        var result = await _dashboardService.GetDashboardSummaryAsync(TestUserId);

        // Assert: 前月比の計算が正確であること
        // 手計算: (120000 - 100000) / 100000 * 100 = 20.0%
        result.PreviousMonth.Should().NotBeNull();
        result.PreviousMonth!.TotalAmount.Should().Be(100000m);
        result.MonthOverMonthChange.Should().NotBeNull();
        result.MonthOverMonthChange!.AmountDiff.Should().Be(20000m);
        result.MonthOverMonthChange.PercentageChange.Should().Be(20.0m);
    }

    [Fact]
    public async Task Integration_WithNoDataAtAll_ReturnsEmptySummaryWithNulls()
    {
        // Arrange: データなし（全フィールドのデフォルト値確認）

        // Act
        var result = await _dashboardService.GetDashboardSummaryAsync(TestUserId);

        // Assert: データなしでも正常なレスポンスが返ること（エラーではない）
        result.Should().NotBeNull();
        result.CurrentMonth.TotalAmount.Should().Be(0m);
        result.CurrentMonth.TotalCount.Should().Be(0);
        result.PreviousMonth.Should().BeNull();
        result.MonthOverMonthChange.Should().BeNull();
        result.TopCategories.Should().BeEmpty();
        result.RecentExpenses.Should().BeEmpty();
        result.UpcomingSubscriptions.Should().BeEmpty();
    }

    [Fact]
    public async Task Integration_WithOnlySubscriptionsNoCategoryExpenses_TopCategoriesEmpty()
    {
        // Arrange: 支出なし、サブスクのみ存在する
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var subscriptions = new List<Subscription>
        {
            BuildSubscription(30, TestUserId, "Netflix", 1490m, today.AddDays(3)),
            BuildSubscription(31, TestUserId, "Spotify", 980m,  today.AddDays(7)),
        };
        _context.Subscriptions.AddRange(subscriptions);
        await _context.SaveChangesAsync();

        // Act
        var result = await _dashboardService.GetDashboardSummaryAsync(TestUserId);

        // Assert: 支出がなければ TopCategories は空、サブスクは正常に返ること
        result.CurrentMonth.TotalAmount.Should().Be(0m);
        result.TopCategories.Should().BeEmpty();
        result.UpcomingSubscriptions.Should().HaveCount(2);
    }

    [Fact]
    public async Task Integration_PreviousMonthExpiredSubscriptions_NotIncludedInUpcoming()
    {
        // Arrange: 30日超のサブスクは UpcomingSubscriptions に含まれない
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var subscriptions = new List<Subscription>
        {
            // 30日以内（含まれる）
            BuildSubscription(40, TestUserId, "Netflix", 1490m, today.AddDays(29)),
            // 30日超（含まれない）
            BuildSubscription(41, TestUserId, "Spotify", 980m,  today.AddDays(31)),
            // 非アクティブ（含まれない）
            BuildSubscription(42, TestUserId, "Hulu",    1026m, today.AddDays(10), isActive: false),
        };
        _context.Subscriptions.AddRange(subscriptions);
        await _context.SaveChangesAsync();

        // Act
        var result = await _dashboardService.GetDashboardSummaryAsync(TestUserId);

        // Assert: 30日以内のアクティブなサブスクのみ含まれること
        result.UpcomingSubscriptions.Should().HaveCount(1);
        result.UpcomingSubscriptions.First().ServiceName.Should().Be("Netflix");
    }
}
