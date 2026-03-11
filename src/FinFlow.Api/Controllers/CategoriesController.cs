using System.Security.Claims;
using FinFlow.Api.Models;
using FinFlow.Domain.Entities;
using FinFlow.Domain.Exceptions;
using FinFlow.Domain.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinFlow.Api.Controllers;

[ApiController]
[Route("api/categories")]
[Authorize]
public class CategoriesController : ControllerBase
{
    private readonly ICategoryService _categoryService;

    public CategoriesController(ICategoryService categoryService)
    {
        _categoryService = categoryService;
    }

    [HttpGet]
    public async Task<IActionResult> GetCategories()
    {
        var userId = GetCurrentUserId();
        var categories = await _categoryService.GetCategoriesAsync(userId);
        return Ok(categories.Select(MapToResponse));
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetCategory(int id)
    {
        var userId = GetCurrentUserId();
        var category = await _categoryService.GetCategoryByIdAsync(id, userId);

        if (category == null)
            return NotFound(new { error = $"Category with ID {id} was not found." });

        return Ok(MapToResponse(category));
    }

    [HttpPost]
    public async Task<IActionResult> CreateCategory([FromBody] CreateCategoryRequest request)
    {
        var userId = GetCurrentUserId();
        var category = MapToEntity(request, userId);

        var created = await _categoryService.CreateCategoryAsync(category);
        return CreatedAtAction(nameof(GetCategory), new { id = created.Id }, MapToResponse(created));
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdateCategory(int id, [FromBody] UpdateCategoryRequest request)
    {
        var userId = GetCurrentUserId();
        var updated = MapToEntity(request, userId);

        var category = await _categoryService.UpdateCategoryAsync(id, userId, updated);

        if (category == null)
            return NotFound(new { error = $"Category with ID {id} was not found or is a system category." });

        return Ok(MapToResponse(category));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteCategory(int id)
    {
        var userId = GetCurrentUserId();
        var deleted = await _categoryService.DeleteCategoryAsync(id, userId);

        if (!deleted)
            return NotFound(new { error = $"Category with ID {id} was not found." });

        return NoContent();
    }

    private string GetCurrentUserId()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub");

        if (string.IsNullOrEmpty(userId))
            throw new UnauthorizedAccessException("User ID not found in token.");

        return userId;
    }

    private static Category MapToEntity(CreateCategoryRequest request, string userId) =>
        new()
        {
            Name = request.Name,
            Color = request.Color,
            UserId = userId
        };

    private static CategoryResponse MapToResponse(Category category) =>
        new()
        {
            Id = category.Id,
            Name = category.Name,
            Color = category.Color,
            IsSystem = category.IsSystem,
            CreatedAt = category.CreatedAt,
            UpdatedAt = category.UpdatedAt
        };
}
