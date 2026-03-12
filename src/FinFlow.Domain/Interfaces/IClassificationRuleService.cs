using FinFlow.Domain.Entities;

namespace FinFlow.Domain.Interfaces;

public interface IClassificationRuleService
{
    Task<IEnumerable<ClassificationRule>> GetRulesAsync(string userId);
    Task<ClassificationRule?> GetRuleByIdAsync(int id, string userId);
    Task<ClassificationRule> CreateRuleAsync(ClassificationRule rule);
    Task<ClassificationRule?> UpdateRuleAsync(int id, string userId, ClassificationRule updated);
    Task<bool> DeleteRuleAsync(int id, string userId);
}
