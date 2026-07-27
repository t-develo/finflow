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
        await EnsureServiceNameIsUniqueAsync(subscription.UserId, subscription.ServiceName);

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
        await EnsureServiceNameIsUniqueAsync(userId, updated.ServiceName, excludeId: id);

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

    /// <summary>
    /// 同一ユーザー内でサービス名が重複していないことを確認する。
    /// 重複していれば <see cref="ConflictException"/>（HTTP 409）を投げる。
    /// </summary>
    /// <remarks>
    /// フロントエンドにも二重送信ガードがあるが、それだけでは塞げない経路がある。
    /// api-client.js は 15 秒でリクエストを中断する（REQUEST_TIMEOUT_MS）が、
    /// 中断されるのはクライアント側の待ち受けだけで、サーバーの INSERT は
    /// すでに完了していることがある。ユーザーには「タイムアウトしました」と
    /// 表示されるので、当然もう一度「保存」を押す — これで 2 件目が入る。
    /// 家庭内 LAN + ラズパイという構成では現実に起こりうるため、
    /// CategoryService.CreateCategoryAsync と同様にサーバー側でも防ぐ。
    ///
    /// 比較は前後の空白を無視し、大文字小文字も区別しない
    /// （"Netflix" と "netflix " を別物として登録できてしまうのは、
    ///   利用者から見れば重複そのもの）。
    /// </remarks>
    /// <param name="excludeId">更新時に自分自身を除外するための ID。</param>
    private async Task EnsureServiceNameIsUniqueAsync(string userId, string serviceName, int? excludeId = null)
    {
        var normalized = (serviceName ?? string.Empty).Trim();
        if (normalized.Length == 0)
            return; // 空文字の検証は ModelState / ValidationException の担当

        // EF Core は string.Equals(..., StringComparison) を SQL に翻訳できないため、
        // ToLower() で比較する（インデックスは UserId 側で効くので、
        // 個人の家計簿という規模では実用上の問題にならない）。
        var lowered = normalized.ToLower();

        // 除外条件は式の中で分岐させず、C# 側でクエリを組み立てる。
        // `excludeId == null || s.Id != excludeId.Value` のような式は
        // プロバイダによって翻訳の挙動が変わりうるため。
        var query = _context.Subscriptions.Where(s => s.UserId == userId);
        if (excludeId.HasValue)
        {
            var idToExclude = excludeId.Value;
            query = query.Where(s => s.Id != idToExclude);
        }

        var isDuplicate = await query.AnyAsync(s => s.ServiceName.Trim().ToLower() == lowered);

        if (isDuplicate)
            throw new ConflictException($"サブスクリプション「{normalized}」は既に登録されています。");
    }
}
