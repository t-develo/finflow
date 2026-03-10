using FinFlow.Domain.Entities;

namespace FinFlow.Domain.Interfaces;

public interface IExpenseService
{
    Task<IEnumerable<Expense>> GetExpensesAsync(string userId, ExpenseFilter? filter = null);
    Task<Expense?> GetExpenseByIdAsync(int id, string userId);
    Task<Expense> CreateExpenseAsync(Expense expense);
    Task<Expense?> UpdateExpenseAsync(int id, string userId, Expense updated);
    Task<bool> DeleteExpenseAsync(int id, string userId);
    Task<int> ImportExpensesAsync(IEnumerable<Expense> expenses, string userId);
}

public class ExpenseFilter
{
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
    public int? CategoryId { get; set; }
    public decimal? MinAmount { get; set; }
    public decimal? MaxAmount { get; set; }
    public string? Keyword { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
}
