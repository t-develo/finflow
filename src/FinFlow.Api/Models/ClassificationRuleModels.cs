using System.ComponentModel.DataAnnotations;

namespace FinFlow.Api.Models;

/// <summary>
/// 自動分類ルール作成リクエストのDTO
/// </summary>
public class CreateClassificationRuleRequest
{
    [Required(ErrorMessage = "Keyword is required.")]
    [StringLength(200, MinimumLength = 1, ErrorMessage = "Keyword must be between 1 and 200 characters.")]
    public string Keyword { get; set; } = string.Empty;

    [Required(ErrorMessage = "CategoryId is required.")]
    [Range(1, int.MaxValue, ErrorMessage = "CategoryId must be a positive integer.")]
    public int CategoryId { get; set; }

    [Range(1, 1000, ErrorMessage = "Priority must be between 1 and 1000.")]
    public int Priority { get; set; } = 100;
}

/// <summary>
/// 自動分類ルール更新リクエストのDTO
/// </summary>
public class UpdateClassificationRuleRequest : CreateClassificationRuleRequest { }

/// <summary>
/// 自動分類ルールレスポンスのDTO
/// </summary>
public class ClassificationRuleResponse
{
    public int Id { get; set; }
    public string Keyword { get; set; } = string.Empty;
    public int CategoryId { get; set; }
    public string? CategoryName { get; set; }
    public int Priority { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
