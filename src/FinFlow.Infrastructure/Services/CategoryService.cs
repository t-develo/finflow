using FinFlow.Domain.Entities;
using FinFlow.Domain.Exceptions;
using FinFlow.Domain.Interfaces;
using FinFlow.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace FinFlow.Infrastructure.Services;

/// <summary>
/// カテゴリ管理のビジネスロジックを担当するサービス
/// </summary>
public class CategoryService : ICategoryService
{
    private readonly FinFlowDbContext _dbContext;

    public CategoryService(FinFlowDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IEnumerable<Category>> GetCategoriesAsync(string userId)
    {
        // システムカテゴリとユーザー固有カテゴリの両方を返す
        return await _dbContext.Categories
            .Where(c => c.IsSystem || c.UserId == userId)
            .OrderBy(c => c.IsSystem ? 0 : 1)
            .ThenBy(c => c.Id)
            .ToListAsync();
    }

    public async Task<Category?> GetCategoryByIdAsync(int id, string userId)
    {
        return await _dbContext.Categories
            .FirstOrDefaultAsync(c => c.Id == id && (c.IsSystem || c.UserId == userId));
    }

    public async Task<Category> CreateCategoryAsync(Category category)
    {
        var isDuplicateName = await _dbContext.Categories
            .AnyAsync(c => c.Name == category.Name && (c.IsSystem || c.UserId == category.UserId));

        if (isDuplicateName)
            throw new ConflictException($"Category with name '{category.Name}' already exists.");

        var now = DateTime.UtcNow;
        category.IsSystem = false; // ユーザー作成カテゴリはシステムカテゴリではない
        category.CreatedAt = now;
        category.UpdatedAt = now;

        _dbContext.Categories.Add(category);
        await _dbContext.SaveChangesAsync();
        return category;
    }

    public async Task<Category?> UpdateCategoryAsync(int id, string userId, Category updated)
    {
        var category = await _dbContext.Categories
            .FirstOrDefaultAsync(c => c.Id == id && c.UserId == userId && !c.IsSystem);

        if (category == null)
            return null;

        var isDuplicateName = await _dbContext.Categories
            .AnyAsync(c => c.Id != id && c.Name == updated.Name && (c.IsSystem || c.UserId == userId));

        if (isDuplicateName)
            throw new ConflictException($"Category with name '{updated.Name}' already exists.");

        category.Name = updated.Name;
        category.Color = updated.Color;
        category.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync();
        return category;
    }

    public async Task<bool> DeleteCategoryAsync(int id, string userId)
    {
        var category = await _dbContext.Categories
            .FirstOrDefaultAsync(c => c.Id == id && c.UserId == userId && !c.IsSystem);

        if (category == null)
        {
            var isSystemCategory = await _dbContext.Categories
                .AnyAsync(c => c.Id == id && c.IsSystem);

            if (isSystemCategory)
                throw new ConflictException("System default categories cannot be deleted.");

            return false;
        }

        var hasLinkedExpenses = await _dbContext.Expenses
            .AnyAsync(e => e.CategoryId == id);

        if (hasLinkedExpenses)
            throw new ConflictException($"Category with ID {id} cannot be deleted because it has associated expenses.");

        _dbContext.Categories.Remove(category);
        await _dbContext.SaveChangesAsync();
        return true;
    }
}
