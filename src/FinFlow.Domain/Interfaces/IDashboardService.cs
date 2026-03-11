namespace FinFlow.Domain.Interfaces;

public interface IDashboardService
{
    Task<DashboardSummaryResponseDto> GetDashboardSummaryAsync(string userId);
}

public record MonthSummaryDto(
    int Year,
    int Month,
    decimal TotalAmount,
    int TotalCount
);

public record MonthOverMonthChangeDto(
    decimal AmountDiff,
    decimal PercentageChange
);

public record TopCategoryDto(
    int CategoryId,
    string CategoryName,
    decimal Amount,
    decimal Percentage
);

public record RecentExpenseSummaryDto(
    int Id,
    decimal Amount,
    string? CategoryName,
    string? Description,
    DateOnly Date
);

public record UpcomingSubscriptionDto(
    int Id,
    string ServiceName,
    decimal Amount,
    DateOnly NextBillingDate,
    int DaysUntilBilling
);

public record DashboardSummaryResponseDto(
    MonthSummaryDto CurrentMonth,
    MonthSummaryDto? PreviousMonth,
    MonthOverMonthChangeDto? MonthOverMonthChange,
    IEnumerable<TopCategoryDto> TopCategories,
    IEnumerable<RecentExpenseSummaryDto> RecentExpenses,
    IEnumerable<UpcomingSubscriptionDto> UpcomingSubscriptions
);
