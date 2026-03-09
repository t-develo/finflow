using Microsoft.AspNetCore.Identity;

namespace FinFlow.Domain.Entities;

public class ApplicationUser : IdentityUser
{
    public ICollection<Expense> Expenses { get; set; } = new List<Expense>();
    public ICollection<Subscription> Subscriptions { get; set; } = new List<Subscription>();
    public ICollection<Category> Categories { get; set; } = new List<Category>();
    public ICollection<ClassificationRule> ClassificationRules { get; set; } = new List<ClassificationRule>();
}
