using FinFlow.Domain.Interfaces;
using FinFlow.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FinFlow.Infrastructure.Services;

/// <summary>
/// サブスクリプションの支払期日を定期的に確認し、3日前になったらメール通知を送るバックグラウンドサービス。
/// IHostedServiceとして登録し、アプリケーション起動時に自動実行される。
/// </summary>
public class NotificationScheduler : BackgroundService
{
    // 3日前に通知する
    private const int NotificationDaysAhead = 3;

    // チェック間隔: 本番は1時間、テスト用に上書き可能
    private readonly TimeSpan _checkInterval;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<NotificationScheduler> _logger;

    public NotificationScheduler(
        IServiceScopeFactory scopeFactory,
        ILogger<NotificationScheduler> logger,
        TimeSpan? checkInterval = null)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _checkInterval = checkInterval ?? TimeSpan.FromHours(1);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("NotificationScheduler started. Checking every {Interval}.", _checkInterval);

        // 起動直後に1回チェックしてから定期実行
        await CheckAndNotifyAsync(stoppingToken);

        using var timer = new PeriodicTimer(_checkInterval);
        while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken))
        {
            await CheckAndNotifyAsync(stoppingToken);
        }

        _logger.LogInformation("NotificationScheduler stopped.");
    }

    /// <summary>
    /// 3日以内に支払いが迫っているサブスクリプションを検索し、該当ユーザーにメールを送信する。
    /// </summary>
    internal async Task CheckAndNotifyAsync(CancellationToken cancellationToken)
    {
        _logger.LogDebug("Starting subscription billing notification check.");

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var subscriptionService = scope.ServiceProvider.GetRequiredService<ISubscriptionService>();
            var emailSender = scope.ServiceProvider.GetRequiredService<IEmailSender>();
            var dbContext = scope.ServiceProvider.GetRequiredService<FinFlowDbContext>();

            // 全アクティブユーザーの直近支払いサブスクを取得
            var upcomingSubscriptions = await dbContext.Subscriptions
                .Where(s => s.IsActive)
                .Include(s => s.Category)
                .ToListAsync(cancellationToken);

            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var notificationThreshold = today.AddDays(NotificationDaysAhead);

            // 3日以内に支払いが迫っているサブスク
            var dueSubscriptions = upcomingSubscriptions
                .Where(s => s.NextBillingDate >= today && s.NextBillingDate <= notificationThreshold)
                .ToList();

            if (dueSubscriptions.Count == 0)
            {
                _logger.LogDebug("No subscriptions due within {Days} days.", NotificationDaysAhead);
                return;
            }

            _logger.LogInformation("Found {Count} subscriptions due within {Days} days.", dueSubscriptions.Count, NotificationDaysAhead);

            // ユーザーIDでグループ化してメールを送信
            var byUser = dueSubscriptions.GroupBy(s => s.UserId);
            foreach (var userGroup in byUser)
            {
                var userId = userGroup.Key;

                try
                {
                    // ユーザーのメールアドレスを取得
                    var userEmail = await GetUserEmailAsync(dbContext, userId, cancellationToken);
                    if (string.IsNullOrEmpty(userEmail))
                    {
                        _logger.LogWarning("Could not find email for user {UserId}. Skipping notification.", userId);
                        continue;
                    }

                    var subject = $"FinFlow: {userGroup.Count()}件のサブスクリプションの支払い期日が近づいています";
                    var htmlBody = BuildNotificationEmailBody(userGroup.ToList(), today);

                    await emailSender.SendEmailAsync(userEmail, subject, htmlBody);
                    _logger.LogInformation("Notification sent to user {UserId} for {Count} subscriptions.", userId, userGroup.Count());
                }
                catch (Exception ex)
                {
                    // 個別ユーザーへの通知失敗は他のユーザーの通知に影響させない
                    _logger.LogError(ex, "Failed to send notification to user {UserId}.", userId);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during notification check.");
        }
    }

    private static async Task<string?> GetUserEmailAsync(
        FinFlowDbContext dbContext,
        string userId,
        CancellationToken cancellationToken)
    {
        // ASP.NET Identity の AspNetUsers テーブルからメールアドレスを取得
        var user = await dbContext.Users.FindAsync(new object[] { userId }, cancellationToken);
        return user?.Email;
    }

    private static string BuildNotificationEmailBody(
        IReadOnlyList<Domain.Entities.Subscription> subscriptions,
        DateOnly today)
    {
        var rows = string.Join("\n", subscriptions.Select(s =>
        {
            var daysUntilBilling = s.NextBillingDate.DayNumber - today.DayNumber;
            var daysText = daysUntilBilling == 0 ? "本日" : $"{daysUntilBilling}日後";
            return $"""
                <tr>
                  <td style="padding: 8px; border: 1px solid #e5e7eb;">{s.ServiceName}</td>
                  <td style="padding: 8px; border: 1px solid #e5e7eb; text-align: right;">&yen;{s.Amount:N0}</td>
                  <td style="padding: 8px; border: 1px solid #e5e7eb; text-align: center;">{s.NextBillingDate:yyyy/MM/dd}</td>
                  <td style="padding: 8px; border: 1px solid #e5e7eb; text-align: center;">{daysText}</td>
                </tr>
                """;
        }));

        return $"""
            <!DOCTYPE html>
            <html lang="ja">
            <head><meta charset="UTF-8"></head>
            <body style="font-family: sans-serif; color: #374151; max-width: 600px; margin: 0 auto; padding: 20px;">
              <h2 style="color: #1d4ed8;">FinFlow - サブスクリプション支払い通知</h2>
              <p>以下のサブスクリプションの支払い期日が近づいています。</p>
              <table style="width: 100%; border-collapse: collapse; margin-top: 16px;">
                <thead>
                  <tr style="background-color: #f3f4f6;">
                    <th style="padding: 8px; border: 1px solid #e5e7eb; text-align: left;">サービス名</th>
                    <th style="padding: 8px; border: 1px solid #e5e7eb; text-align: right;">金額</th>
                    <th style="padding: 8px; border: 1px solid #e5e7eb; text-align: center;">支払日</th>
                    <th style="padding: 8px; border: 1px solid #e5e7eb; text-align: center;">残り日数</th>
                  </tr>
                </thead>
                <tbody>
                  {rows}
                </tbody>
              </table>
              <p style="margin-top: 24px; color: #6b7280; font-size: 12px;">
                このメールはFinFlowから自動送信されています。
              </p>
            </body>
            </html>
            """;
    }
}
