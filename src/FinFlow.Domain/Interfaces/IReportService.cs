namespace FinFlow.Domain.Interfaces;

public interface IReportService
{
    Task<MonthlyReportDto> GetMonthlyReportAsync(string userId, int year, int month);
    Task<IEnumerable<CategoryBreakdownDto>> GetCategoryBreakdownAsync(string userId, int year, int month);
    Task<DashboardSummaryDto> GetDashboardSummaryAsync(string userId);
}

public record MonthlyReportDto(
    int Year,
    int Month,
    decimal TotalAmount,
    int ExpenseCount,
    IEnumerable<CategoryBreakdownDto> CategoryBreakdown
);

public record CategoryBreakdownDto(
    int CategoryId,
    string CategoryName,
    string CategoryColor,
    decimal TotalAmount,
    int Count,
    decimal Percentage
);

public record DashboardSummaryDto(
    decimal CurrentMonthTotal,
    decimal PreviousMonthTotal,
    decimal MonthOverMonthChange,
    IEnumerable<CategoryBreakdownDto> TopCategories,
    IEnumerable<RecentExpenseDto> RecentExpenses
);

public record RecentExpenseDto(
    int Id,
    decimal Amount,
    string? Description,
    DateOnly Date,
    string? CategoryName,
    string? CategoryColor
);
