using FinFlow.Domain.Entities;
using FinFlow.Domain.Exceptions;
using FinFlow.Domain.Interfaces;
using FinFlow.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace FinFlow.Infrastructure.Services;

/// <summary>
/// サブスクリプション管理のビジネスロジックを担当するサービス
/// </summary>
public class SubscriptionService : ISubscriptionService
{
    private readonly FinFlowDbContext _context;

    public SubscriptionService(FinFlowDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<Subscription>> GetSubscriptionsAsync(string userId)
    {
        return await _context.Subscriptions
            .Include(s => s.Category)
            .Where(s => s.UserId == userId)
            .OrderBy(s => s.NextBillingDate)
            .ToListAsync();
    }

    public async Task<Subscription?> GetSubscriptionByIdAsync(int id, string userId)
    {
        return await _context.Subscriptions
            .Include(s => s.Category)
            .FirstOrDefaultAsync(s => s.Id == id && s.UserId == userId);
    }

    public async Task<Subscription> CreateSubscriptionAsync(Subscription subscription)
    {
        await ValidateCategoryExistsAsync(subscription.CategoryId, subscription.UserId);

        subscription.CreatedAt = DateTime.UtcNow;
        subscription.UpdatedAt = DateTime.UtcNow;

        _context.Subscriptions.Add(subscription);
        await _context.SaveChangesAsync();

        // カテゴリ情報をロードして返す
        await _context.Entry(subscription).Reference(s => s.Category).LoadAsync();

        return subscription;
    }

    public async Task<Subscription?> UpdateSubscriptionAsync(int id, string userId, Subscription updated)
    {
        var existing = await _context.Subscriptions
            .FirstOrDefaultAsync(s => s.Id == id && s.UserId == userId);

        if (existing is null) return null;

        await ValidateCategoryExistsAsync(updated.CategoryId, userId);

        existing.ServiceName = updated.ServiceName;
        existing.Amount = updated.Amount;
        existing.CategoryId = updated.CategoryId;
        existing.BillingCycle = updated.BillingCycle;
        existing.NextBillingDate = updated.NextBillingDate;
        existing.IsActive = updated.IsActive;
        existing.Notes = updated.Notes;
        existing.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        // カテゴリ情報をロードして返す
        await _context.Entry(existing).Reference(s => s.Category).LoadAsync();

        return existing;
    }

    public async Task<bool> DeleteSubscriptionAsync(int id, string userId)
    {
        var subscription = await _context.Subscriptions
            .FirstOrDefaultAsync(s => s.Id == id && s.UserId == userId);

        if (subscription is null) return false;

        _context.Subscriptions.Remove(subscription);
        await _context.SaveChangesAsync();

        return true;
    }

    public async Task<IEnumerable<Subscription>> GetUpcomingBillingsAsync(string userId, int daysAhead = 3)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var cutoffDate = today.AddDays(daysAhead);

        return await _context.Subscriptions
            .Include(s => s.Category)
            .Where(s => s.UserId == userId
                && s.IsActive
                && s.NextBillingDate >= today
                && s.NextBillingDate <= cutoffDate)
            .OrderBy(s => s.NextBillingDate)
            .ToListAsync();
    }

    private async Task ValidateCategoryExistsAsync(int? categoryId, string userId)
    {
        if (!categoryId.HasValue)
            return;

        var categoryExists = await _context.Categories
            .AnyAsync(c => c.Id == categoryId.Value && (c.IsSystem || c.UserId == userId));

        if (!categoryExists)
            throw new ValidationException($"Category with ID {categoryId.Value} does not exist.");
    }
}
