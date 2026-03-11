using System.ComponentModel.DataAnnotations;

namespace FinFlow.Api.Models;

/// <summary>
/// カテゴリ作成リクエストのDTO
/// </summary>
public class CreateCategoryRequest
{
    [Required(ErrorMessage = "Name is required.")]
    [StringLength(100, MinimumLength = 1, ErrorMessage = "Name must be between 1 and 100 characters.")]
    public string Name { get; set; } = string.Empty;

    [RegularExpression(@"^#[0-9A-Fa-f]{6}$", ErrorMessage = "Color must be a valid hex color code (e.g. #FF0000).")]
    public string Color { get; set; } = "#6B7280";
}

/// <summary>
/// カテゴリ更新リクエストのDTO
/// </summary>
public class UpdateCategoryRequest : CreateCategoryRequest { }

/// <summary>
/// カテゴリレスポンスのDTO
/// </summary>
public class CategoryResponse
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Color { get; set; } = string.Empty;
    public bool IsSystem { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
