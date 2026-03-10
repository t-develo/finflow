namespace FinFlow.Domain.Entities;

public class Category
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Color { get; set; } = "#6B7280";
    public bool IsSystem { get; set; }
    public string? UserId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Navigation properties (within Domain only; ApplicationUser is in Infrastructure)
    public ICollection<Expense> Expenses { get; set; } = new List<Expense>();
    public ICollection<Subscription> Subscriptions { get; set; } = new List<Subscription>();
    public ICollection<ClassificationRule> ClassificationRules { get; set; } = new List<ClassificationRule>();
}
