namespace FinFlow.Domain.Entities;

public class Expense
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public int? CategoryId { get; set; }
    public decimal Amount { get; set; }
    public string? Description { get; set; }
    public DateOnly Date { get; set; }
    public string? ImportSource { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Navigation properties
    public ApplicationUser? User { get; set; }
    public Category? Category { get; set; }
}
