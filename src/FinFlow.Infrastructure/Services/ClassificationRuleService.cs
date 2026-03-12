using FinFlow.Domain.Entities;
using FinFlow.Domain.Exceptions;
using FinFlow.Domain.Interfaces;
using FinFlow.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace FinFlow.Infrastructure.Services;

/// <summary>
/// 自動分類ルールのCRUDビジネスロジックを担当するサービス
/// </summary>
public class ClassificationRuleService : IClassificationRuleService
{
    private readonly FinFlowDbContext _dbContext;

    public ClassificationRuleService(FinFlowDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IEnumerable<ClassificationRule>> GetRulesAsync(string userId)
    {
        return await _dbContext.ClassificationRules
            .Include(r => r.Category)
            .Where(r => r.UserId == userId)
            .OrderBy(r => r.Priority)
            .ThenBy(r => r.CreatedAt)
            .ToListAsync();
    }

    public async Task<ClassificationRule?> GetRuleByIdAsync(int id, string userId)
    {
        return await _dbContext.ClassificationRules
            .Include(r => r.Category)
            .FirstOrDefaultAsync(r => r.Id == id && r.UserId == userId);
    }

    public async Task<ClassificationRule> CreateRuleAsync(ClassificationRule rule)
    {
        await ValidateCategoryExistsAsync(rule.CategoryId, rule.UserId);

        var now = DateTime.UtcNow;
        rule.CreatedAt = now;
        rule.UpdatedAt = now;

        _dbContext.ClassificationRules.Add(rule);
        await _dbContext.SaveChangesAsync();

        return await _dbContext.ClassificationRules
            .Include(r => r.Category)
            .FirstAsync(r => r.Id == rule.Id);
    }

    public async Task<ClassificationRule?> UpdateRuleAsync(int id, string userId, ClassificationRule updated)
    {
        var rule = await _dbContext.ClassificationRules
            .FirstOrDefaultAsync(r => r.Id == id && r.UserId == userId);

        if (rule == null)
            return null;

        await ValidateCategoryExistsAsync(updated.CategoryId, userId);

        rule.Keyword = updated.Keyword;
        rule.CategoryId = updated.CategoryId;
        rule.Priority = updated.Priority;
        rule.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync();

        return await _dbContext.ClassificationRules
            .Include(r => r.Category)
            .FirstAsync(r => r.Id == rule.Id);
    }

    public async Task<bool> DeleteRuleAsync(int id, string userId)
    {
        var rule = await _dbContext.ClassificationRules
            .FirstOrDefaultAsync(r => r.Id == id && r.UserId == userId);

        if (rule == null)
            return false;

        _dbContext.ClassificationRules.Remove(rule);
        await _dbContext.SaveChangesAsync();
        return true;
    }

    private async Task ValidateCategoryExistsAsync(int categoryId, string userId)
    {
        var categoryExists = await _dbContext.Categories
            .AnyAsync(c => c.Id == categoryId && (c.IsSystem || c.UserId == userId));

        if (!categoryExists)
            throw new ValidationException($"Category with ID {categoryId} does not exist.");
    }
}
