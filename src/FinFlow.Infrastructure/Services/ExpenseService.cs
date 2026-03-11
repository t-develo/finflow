using FinFlow.Domain.Entities;
using FinFlow.Domain.Exceptions;
using FinFlow.Domain.Interfaces;
using FinFlow.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace FinFlow.Infrastructure.Services;

/// <summary>
/// 支出管理のビジネスロジックを担当するサービス
/// </summary>
public class ExpenseService : IExpenseService
{
    private readonly FinFlowDbContext _dbContext;

    public ExpenseService(FinFlowDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IEnumerable<Expense>> GetExpensesAsync(string userId, ExpenseFilter? filter = null)
    {
        var query = _dbContext.Expenses
            .Include(e => e.Category)
            .Where(e => e.UserId == userId);

        if (filter != null)
        {
            query = ApplyFilter(query, filter);
        }

        return await query
            .OrderByDescending(e => e.Date)
            .ThenByDescending(e => e.CreatedAt)
            .ToListAsync();
    }

    public async Task<Expense?> GetExpenseByIdAsync(int id, string userId)
    {
        return await _dbContext.Expenses
            .Include(e => e.Category)
            .FirstOrDefaultAsync(e => e.Id == id && e.UserId == userId);
    }

    public async Task<Expense> CreateExpenseAsync(Expense expense)
    {
        await ValidateCategoryExistsAsync(expense.CategoryId, expense.UserId);

        var now = DateTime.UtcNow;
        expense.CreatedAt = now;
        expense.UpdatedAt = now;

        _dbContext.Expenses.Add(expense);
        await _dbContext.SaveChangesAsync();

        return await _dbContext.Expenses
            .Include(e => e.Category)
            .FirstAsync(e => e.Id == expense.Id);
    }

    public async Task<Expense?> UpdateExpenseAsync(int id, string userId, Expense updated)
    {
        var expense = await _dbContext.Expenses
            .FirstOrDefaultAsync(e => e.Id == id && e.UserId == userId);

        if (expense == null)
            return null;

        await ValidateCategoryExistsAsync(updated.CategoryId, userId);

        expense.Amount = updated.Amount;
        expense.CategoryId = updated.CategoryId;
        expense.Date = updated.Date;
        expense.Description = updated.Description;
        expense.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync();

        return await _dbContext.Expenses
            .Include(e => e.Category)
            .FirstAsync(e => e.Id == expense.Id);
    }

    public async Task<bool> DeleteExpenseAsync(int id, string userId)
    {
        var expense = await _dbContext.Expenses
            .FirstOrDefaultAsync(e => e.Id == id && e.UserId == userId);

        if (expense == null)
            return false;

        _dbContext.Expenses.Remove(expense);
        await _dbContext.SaveChangesAsync();
        return true;
    }

    public async Task<int> ImportExpensesAsync(IEnumerable<Expense> expenses, string userId)
    {
        var now = DateTime.UtcNow;
        var expenseList = expenses.ToList();

        foreach (var expense in expenseList)
        {
            expense.UserId = userId;
            expense.CreatedAt = now;
            expense.UpdatedAt = now;
        }

        _dbContext.Expenses.AddRange(expenseList);
        await _dbContext.SaveChangesAsync();
        return expenseList.Count;
    }

    private IQueryable<Expense> ApplyFilter(IQueryable<Expense> query, ExpenseFilter filter)
    {
        if (filter.From.HasValue)
            query = query.Where(e => e.Date >= filter.From.Value);

        if (filter.To.HasValue)
            query = query.Where(e => e.Date <= filter.To.Value);

        if (filter.CategoryId.HasValue)
            query = query.Where(e => e.CategoryId == filter.CategoryId.Value);

        if (filter.MinAmount.HasValue)
            query = query.Where(e => e.Amount >= filter.MinAmount.Value);

        if (filter.MaxAmount.HasValue)
            query = query.Where(e => e.Amount <= filter.MaxAmount.Value);

        if (!string.IsNullOrWhiteSpace(filter.Keyword))
            query = query.Where(e => e.Description != null && e.Description.Contains(filter.Keyword));

        return query;
    }

    private async Task ValidateCategoryExistsAsync(int? categoryId, string userId)
    {
        if (!categoryId.HasValue)
            return;

        var categoryExists = await _dbContext.Categories
            .AnyAsync(c => c.Id == categoryId.Value && (c.IsSystem || c.UserId == userId));

        if (!categoryExists)
            throw new ValidationException($"Category with ID {categoryId.Value} does not exist.");
    }
}
