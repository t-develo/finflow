using FinFlow.Domain.Entities;

namespace FinFlow.Domain.Interfaces;

public interface ISubscriptionService
{
    Task<IEnumerable<Subscription>> GetSubscriptionsAsync(string userId);
    Task<Subscription?> GetSubscriptionByIdAsync(int id, string userId);
    Task<Subscription> CreateSubscriptionAsync(Subscription subscription);
    Task<Subscription?> UpdateSubscriptionAsync(int id, string userId, Subscription updated);
    Task<bool> DeleteSubscriptionAsync(int id, string userId);
    Task<IEnumerable<Subscription>> GetUpcomingBillingsAsync(string userId, int daysAhead = 3);
}
