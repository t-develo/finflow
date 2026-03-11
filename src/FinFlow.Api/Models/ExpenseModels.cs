using System.ComponentModel.DataAnnotations;

namespace FinFlow.Api.Models;

/// <summary>
/// 支出作成・更新リクエストのDTO
/// </summary>
public class CreateExpenseRequest
{
    [Required(ErrorMessage = "Amount is required.")]
    [Range(0.01, double.MaxValue, ErrorMessage = "Amount must be a positive number.")]
    public decimal Amount { get; set; }

    [Required(ErrorMessage = "CategoryId is required.")]
    public int CategoryId { get; set; }

    [Required(ErrorMessage = "Date is required.")]
    public DateOnly Date { get; set; }

    [Required(ErrorMessage = "Description is required.")]
    [StringLength(200, MinimumLength = 1, ErrorMessage = "Description must be between 1 and 200 characters.")]
    public string Description { get; set; } = string.Empty;

    [StringLength(500, ErrorMessage = "Memo must be 500 characters or less.")]
    public string? Memo { get; set; }
}

/// <summary>
/// 支出更新リクエストのDTO（作成と同一フィールド）
/// </summary>
public class UpdateExpenseRequest : CreateExpenseRequest { }

/// <summary>
/// 支出レスポンスのDTO（エンティティをそのままAPIに返さないためのマッピング）
/// </summary>
public class ExpenseResponse
{
    public int Id { get; set; }
    public decimal Amount { get; set; }
    public int? CategoryId { get; set; }
    public string? CategoryName { get; set; }
    public DateOnly Date { get; set; }
    public string Description { get; set; } = string.Empty;
    public string? Memo { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
