using System.ComponentModel.DataAnnotations;

namespace FinFlow.Api.Models;

/// <summary>
/// サブスクリプション作成リクエストのDTO
/// </summary>
public class CreateSubscriptionRequest
{
    [Required(ErrorMessage = "ServiceName is required.")]
    [StringLength(100, MinimumLength = 1, ErrorMessage = "ServiceName must be between 1 and 100 characters.")]
    public string ServiceName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Amount is required.")]
    [Range(0.01, double.MaxValue, ErrorMessage = "Amount must be a positive number.")]
    public decimal Amount { get; set; }

    public int? CategoryId { get; set; }

    [Required(ErrorMessage = "BillingCycle is required.")]
    [RegularExpression("^(monthly|yearly|weekly)$", ErrorMessage = "BillingCycle must be one of: monthly, yearly, weekly.")]
    public string BillingCycle { get; set; } = "monthly";

    [Required(ErrorMessage = "NextBillingDate is required.")]
    public DateOnly NextBillingDate { get; set; }

    [StringLength(500, ErrorMessage = "Description must be 500 characters or less.")]
    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;
}

/// <summary>
/// サブスクリプション更新リクエストのDTO（作成と同一フィールド）
/// </summary>
public class UpdateSubscriptionRequest : CreateSubscriptionRequest { }

/// <summary>
/// サブスクリプションレスポンスのDTO
/// </summary>
public class SubscriptionResponse
{
    public int Id { get; set; }
    public string ServiceName { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public int? CategoryId { get; set; }
    public string? CategoryName { get; set; }
    public string BillingCycle { get; set; } = "monthly";
    public DateOnly NextBillingDate { get; set; }
    public string? Description { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
