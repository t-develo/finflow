using FinFlow.Domain.Interfaces;

namespace FinFlow.Infrastructure.Services;

/// <summary>
/// ダッシュボードサマリの集約を担当するサービス
/// </summary>
public class DashboardService : IDashboardService
{
    private readonly IReportService _reportService;
    private readonly ISubscriptionService _subscriptionService;
    private readonly IExpenseService _expenseService;

    public DashboardService(
        IReportService reportService,
        ISubscriptionService subscriptionService,
        IExpenseService expenseService)
    {
        _reportService = reportService;
        _subscriptionService = subscriptionService;
        _expenseService = expenseService;
    }

    public async Task<DashboardSummaryResponseDto> GetDashboardSummaryAsync(string userId)
    {
        var today = DateTime.UtcNow;
        var currentYear = today.Year;
        var currentMonth = today.Month;

        var (previousYear, previousMonth) = GetPreviousYearMonth(currentYear, currentMonth);

        // 既存のReportServiceを組み合わせてダッシュボードデータを集約する
        var currentMonthReportTask = _reportService.GetMonthlyReportAsync(userId, currentYear, currentMonth);
        var previousMonthReportTask = _reportService.GetMonthlyReportAsync(userId, previousYear, previousMonth);
        var recentExpensesTask = FetchRecentExpensesAsync(userId, count: 5);
        var upcomingSubscriptionsTask = FetchUpcomingSubscriptionsAsync(userId, daysAhead: 30);

        await Task.WhenAll(currentMonthReportTask, previousMonthReportTask, recentExpensesTask, upcomingSubscriptionsTask);

        var currentMonthReport = currentMonthReportTask.Result;
        var previousMonthReport = previousMonthReportTask.Result;
        var recentExpenses = recentExpensesTask.Result;
        var upcomingSubscriptions = upcomingSubscriptionsTask.Result;

        var currentMonthDto = new MonthSummaryDto(
            currentYear,
            currentMonth,
            currentMonthReport.TotalAmount,
            currentMonthReport.ExpenseCount
        );

        // 前月データが存在しない場合は null を返す
        var hasPreviousMonthData = previousMonthReport.ExpenseCount > 0;
        MonthSummaryDto? previousMonthDto = hasPreviousMonthData
            ? new MonthSummaryDto(previousYear, previousMonth, previousMonthReport.TotalAmount, previousMonthReport.ExpenseCount)
            : null;

        MonthOverMonthChangeDto? monthOverMonthChange = hasPreviousMonthData
            ? BuildMonthOverMonthChange(currentMonthReport.TotalAmount, previousMonthReport.TotalAmount)
            : null;

        var topCategories = currentMonthReport.CategoryBreakdown
            .Take(5)
            .Select(c => new TopCategoryDto(c.CategoryId, c.CategoryName, c.TotalAmount, c.Percentage));

        return new DashboardSummaryResponseDto(
            currentMonthDto,
            previousMonthDto,
            monthOverMonthChange,
            topCategories,
            recentExpenses,
            upcomingSubscriptions
        );
    }

    private static MonthOverMonthChangeDto BuildMonthOverMonthChange(decimal currentAmount, decimal previousAmount)
    {
        var amountDiff = currentAmount - previousAmount;
        var percentageChange = previousAmount == 0m
            ? 0m
            : Math.Round(amountDiff / previousAmount * 100m, 1, MidpointRounding.AwayFromZero);

        return new MonthOverMonthChangeDto(amountDiff, percentageChange);
    }

    private async Task<IEnumerable<RecentExpenseSummaryDto>> FetchRecentExpensesAsync(string userId, int count)
    {
        var expenses = await _expenseService.GetExpensesAsync(userId, new ExpenseFilter { Page = 1, PageSize = count });
        return expenses.Select(e => new RecentExpenseSummaryDto(
            e.Id,
            e.Amount,
            e.Category?.Name,
            e.Description,
            e.Date
        ));
    }

    private async Task<IEnumerable<UpcomingSubscriptionDto>> FetchUpcomingSubscriptionsAsync(string userId, int daysAhead)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var subscriptions = await _subscriptionService.GetUpcomingBillingsAsync(userId, daysAhead);

        return subscriptions.Select(s => new UpcomingSubscriptionDto(
            s.Id,
            s.ServiceName,
            s.Amount,
            s.NextBillingDate,
            s.NextBillingDate.DayNumber - today.DayNumber
        ));
    }

    private static (int year, int month) GetPreviousYearMonth(int year, int month)
    {
        if (month == 1) return (year - 1, 12);
        return (year, month - 1);
    }
}
