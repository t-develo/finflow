using FinFlow.Domain.Entities;
using FinFlow.Domain.Interfaces;
using FinFlow.Infrastructure.Data;
using FinFlow.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace FinFlow.Tests.Dashboard;

/// <summary>
/// DashboardService のユニットテスト。
/// 既存の ReportService と SubscriptionService を組み合わせた集約結果を検証する。
/// </summary>
public class DashboardServiceTests : IDisposable
{
    private readonly FinFlowDbContext _context;
    private readonly DashboardService _dashboardService;
    private readonly ReportService _reportService;
    private readonly SubscriptionService _subscriptionService;
    private readonly ExpenseService _expenseService;
    private const string TestUserId = "user-dashboard-test";

    public DashboardServiceTests()
    {
        var options = new DbContextOptionsBuilder<FinFlowDbContext>()
            .UseInMemoryDatabase($"DashboardServiceTest_{Guid.NewGuid()}")
            .Options;
        _context = new FinFlowDbContext(options);
        _reportService = new ReportService(_context);
        _subscriptionService = new SubscriptionService(_context);
        _expenseService = new ExpenseService(_context);
        _dashboardService = new DashboardService(_reportService, _subscriptionService, _expenseService);
    }

    public void Dispose() => _context.Dispose();

    private static Expense BuildExpense(int id, string userId, decimal amount, DateOnly date, int? categoryId = null) =>
        new()
        {
            Id = id,
            UserId = userId,
            Amount = amount,
            Date = date,
            CategoryId = categoryId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

    private static Subscription BuildSubscription(int id, string userId, string serviceName, decimal amount, DateOnly nextBillingDate) =>
        new()
        {
            Id = id,
            UserId = userId,
            ServiceName = serviceName,
            Amount = amount,
            BillingCycle = "monthly",
            NextBillingDate = nextBillingDate,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

    [Fact]
    public async Task GetDashboardSummaryAsync_WithCurrentMonthData_ReturnsCorrectSummary()
    {
        // Arrange: 現在日時（2026-03-10）の当月データをDBに挿入する
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var currentYear = today.Year;
        var currentMonth = today.Month;

        var expenses = new List<Expense>
        {
            BuildExpense(1, TestUserId, 10000m, new DateOnly(currentYear, currentMonth, 1)),
            BuildExpense(2, TestUserId, 5000m, new DateOnly(currentYear, currentMonth, 5)),
            BuildExpense(3, TestUserId, 3000m, new DateOnly(currentYear, currentMonth, 10)),
        };
        _context.Expenses.AddRange(expenses);
        await _context.SaveChangesAsync();

        // Act
        var result = await _dashboardService.GetDashboardSummaryAsync(TestUserId);

        // Assert: 当月データが正しく集計されていること
        result.CurrentMonth.TotalAmount.Should().Be(18000m);
        result.CurrentMonth.TotalCount.Should().Be(3);
        result.CurrentMonth.Year.Should().Be(currentYear);
        result.CurrentMonth.Month.Should().Be(currentMonth);
    }

    [Fact]
    public async Task GetDashboardSummaryAsync_WithNoPreviousMonthData_ReturnsPreviousMonthAsNull()
    {
        // Arrange: 当月データのみ存在し、前月データは存在しない
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var expense = BuildExpense(10, TestUserId, 5000m, new DateOnly(today.Year, today.Month, 1));
        _context.Expenses.Add(expense);
        await _context.SaveChangesAsync();

        // Act
        var result = await _dashboardService.GetDashboardSummaryAsync(TestUserId);

        // Assert: 前月データなしの場合、previousMonth は null で返ること
        result.PreviousMonth.Should().BeNull();
        result.MonthOverMonthChange.Should().BeNull();
    }

    [Fact]
    public async Task GetDashboardSummaryAsync_WithUpcomingSubscriptions_ReturnsWithin30Days()
    {
        // Arrange: 30日以内と30日超のサブスクリプションを両方設定する
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var within30Days = today.AddDays(15);
        var beyond30Days = today.AddDays(45);

        var subscriptions = new List<Subscription>
        {
            BuildSubscription(1, TestUserId, "Netflix", 1490m, within30Days),
            BuildSubscription(2, TestUserId, "AdobeCC", 6000m, beyond30Days),
        };
        _context.Subscriptions.AddRange(subscriptions);
        await _context.SaveChangesAsync();

        // Act
        var result = await _dashboardService.GetDashboardSummaryAsync(TestUserId);

        // Assert: 30日以内のサブスクのみが含まれること
        result.UpcomingSubscriptions.Should().HaveCount(1);
        result.UpcomingSubscriptions.First().ServiceName.Should().Be("Netflix");
    }

    [Fact]
    public async Task GetDashboardSummaryAsync_RecentExpenses_ReturnsLatest5()
    {
        // Arrange: 6件の支出を登録し、直近5件のみ返ることを確認する
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var expenses = Enumerable.Range(1, 6)
            .Select(i => BuildExpense(i + 100, TestUserId, i * 1000m, today.AddDays(-i)))
            .ToList();
        _context.Expenses.AddRange(expenses);
        await _context.SaveChangesAsync();

        // Act
        var result = await _dashboardService.GetDashboardSummaryAsync(TestUserId);

        // Assert: 直近5件のみが返ること
        result.RecentExpenses.Should().HaveCount(5);
    }

    [Fact]
    public async Task GetDashboardSummaryAsync_WithNoData_ReturnsEmptySummary()
    {
        // Arrange: データなし

        // Act
        var result = await _dashboardService.GetDashboardSummaryAsync(TestUserId);

        // Assert: データなしでも正常なレスポンスが返ること（エラーではない）
        result.CurrentMonth.TotalAmount.Should().Be(0m);
        result.CurrentMonth.TotalCount.Should().Be(0);
        result.PreviousMonth.Should().BeNull();
        result.MonthOverMonthChange.Should().BeNull();
        result.TopCategories.Should().BeEmpty();
        result.RecentExpenses.Should().BeEmpty();
        result.UpcomingSubscriptions.Should().BeEmpty();
    }

    [Fact]
    public async Task GetDashboardSummaryAsync_WithPreviousMonthData_CalculatesMonthOverMonthChange()
    {
        // Arrange: 当月と前月の両方にデータを設定する
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var currentYear = today.Year;
        var currentMonth = today.Month;
        var (previousYear, previousMonth) = currentMonth == 1
            ? (currentYear - 1, 12)
            : (currentYear, currentMonth - 1);

        var expenses = new List<Expense>
        {
            // 当月: 110,000円
            BuildExpense(200, TestUserId, 110000m, new DateOnly(currentYear, currentMonth, 1)),
            // 前月: 100,000円
            BuildExpense(201, TestUserId, 100000m, new DateOnly(previousYear, previousMonth, 1)),
        };
        _context.Expenses.AddRange(expenses);
        await _context.SaveChangesAsync();

        // Act
        var result = await _dashboardService.GetDashboardSummaryAsync(TestUserId);

        // Assert: 前月比の計算が正確であること
        // 手計算: (110000 - 100000) / 100000 * 100 = 10.0%
        result.PreviousMonth.Should().NotBeNull();
        result.PreviousMonth!.TotalAmount.Should().Be(100000m);
        result.MonthOverMonthChange.Should().NotBeNull();
        result.MonthOverMonthChange!.AmountDiff.Should().Be(10000m);
        result.MonthOverMonthChange.PercentageChange.Should().Be(10.0m);
    }
}
