namespace FinFlow.Domain.Entities;

public class ClassificationRule
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public int CategoryId { get; set; }
    public string Keyword { get; set; } = string.Empty;
    public int Priority { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Navigation properties (within Domain only)
    public Category? Category { get; set; }
}
