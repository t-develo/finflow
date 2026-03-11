using FinFlow.Domain.Interfaces;
using FinFlow.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace FinFlow.Infrastructure.Services;

/// <summary>
/// 月次集計・カテゴリ別集計・ダッシュボードサマリのビジネスロジックを担当するサービス
/// </summary>
public class ReportService : IReportService
{
    private readonly FinFlowDbContext _context;

    public ReportService(FinFlowDbContext context)
    {
        _context = context;
    }

    public async Task<MonthlyReportDto> GetMonthlyReportAsync(string userId, int year, int month)
    {
        var expenses = await FetchExpensesForMonthAsync(userId, year, month);

        if (expenses.Count == 0)
        {
            return new MonthlyReportDto(year, month, 0m, 0, Enumerable.Empty<CategoryBreakdownDto>());
        }

        var totalAmount = expenses.Sum(e => e.Amount);
        var totalCount = expenses.Count;
        var categoryBreakdown = BuildCategoryBreakdown(expenses, totalAmount);

        return new MonthlyReportDto(year, month, totalAmount, totalCount, categoryBreakdown);
    }

    public async Task<IEnumerable<CategoryBreakdownDto>> GetCategoryBreakdownAsync(string userId, int year, int month)
    {
        var expenses = await FetchExpensesForMonthAsync(userId, year, month);

        if (expenses.Count == 0) return Enumerable.Empty<CategoryBreakdownDto>();

        var totalAmount = expenses.Sum(e => e.Amount);
        return BuildCategoryBreakdown(expenses, totalAmount);
    }

    public async Task<DashboardSummaryDto> GetDashboardSummaryAsync(string userId)
    {
        var today = DateTime.UtcNow;
        var currentYear = today.Year;
        var currentMonth = today.Month;

        var currentMonthExpenses = await FetchExpensesForMonthAsync(userId, currentYear, currentMonth);
        var currentMonthTotal = currentMonthExpenses.Sum(e => e.Amount);

        var (previousYear, previousMonth) = GetPreviousYearMonth(currentYear, currentMonth);
        var previousMonthExpenses = await FetchExpensesForMonthAsync(userId, previousYear, previousMonth);
        var previousMonthTotal = previousMonthExpenses.Sum(e => e.Amount);

        var monthOverMonthChange = previousMonthTotal == 0m
            ? 0m
            : Math.Round((currentMonthTotal - previousMonthTotal) / previousMonthTotal * 100m, 1, MidpointRounding.AwayFromZero);

        var topCategories = currentMonthExpenses.Count > 0
            ? BuildCategoryBreakdown(currentMonthExpenses, currentMonthTotal).Take(5)
            : Enumerable.Empty<CategoryBreakdownDto>();

        var recentExpenses = await FetchRecentExpensesAsync(userId, count: 5);

        return new DashboardSummaryDto(
            currentMonthTotal,
            previousMonthTotal,
            monthOverMonthChange,
            topCategories,
            recentExpenses
        );
    }

    // クライアントの円グラフ表示のため、金額降順でカテゴリ別内訳を返す
    private static IEnumerable<CategoryBreakdownDto> BuildCategoryBreakdown(
        List<ExpenseWithCategory> expenses,
        decimal totalAmount)
    {
        return expenses
            .GroupBy(e => new { e.CategoryId, e.CategoryName, e.CategoryColor })
            .Select(g => new CategoryBreakdownDto(
                g.Key.CategoryId ?? 0,
                g.Key.CategoryName ?? "未分類",
                g.Key.CategoryColor ?? "#6B7280",
                g.Sum(e => e.Amount),
                g.Count(),
                totalAmount == 0m ? 0m : Math.Round(g.Sum(e => e.Amount) / totalAmount * 100m, 1, MidpointRounding.AwayFromZero)
            ))
            .OrderByDescending(c => c.TotalAmount)
            .ToList();
    }

    private async Task<List<ExpenseWithCategory>> FetchExpensesForMonthAsync(string userId, int year, int month)
    {
        return await _context.Expenses
            .Where(e => e.UserId == userId
                && e.Date.Year == year
                && e.Date.Month == month)
            .Select(e => new ExpenseWithCategory(
                e.Id,
                e.Amount,
                e.Description,
                e.Date,
                e.CategoryId,
                e.Category != null ? e.Category.Name : null,
                e.Category != null ? e.Category.Color : null
            ))
            .ToListAsync();
    }

    private async Task<IEnumerable<RecentExpenseDto>> FetchRecentExpensesAsync(string userId, int count)
    {
        return await _context.Expenses
            .Where(e => e.UserId == userId)
            .OrderByDescending(e => e.Date)
            .Take(count)
            .Select(e => new RecentExpenseDto(
                e.Id,
                e.Amount,
                e.Description,
                e.Date,
                e.Category != null ? e.Category.Name : null,
                e.Category != null ? e.Category.Color : null
            ))
            .ToListAsync();
    }

    private static (int year, int month) GetPreviousYearMonth(int year, int month)
    {
        if (month == 1) return (year - 1, 12);
        return (year, month - 1);
    }

    // 集計処理の内部DTO（クエリ結果の中間表現）
    private record ExpenseWithCategory(
        int Id,
        decimal Amount,
        string? Description,
        DateOnly Date,
        int? CategoryId,
        string? CategoryName,
        string? CategoryColor
    );
}
