using System.Security.Claims;
using FinFlow.Api.Models;
using FinFlow.Domain.Entities;
using FinFlow.Domain.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinFlow.Api.Controllers;

[ApiController]
[Route("api/classification-rules")]
[Authorize]
public class ClassificationRulesController : ControllerBase
{
    private readonly IClassificationRuleService _ruleService;

    public ClassificationRulesController(IClassificationRuleService ruleService)
    {
        _ruleService = ruleService;
    }

    [HttpGet]
    public async Task<IActionResult> GetRules()
    {
        var userId = GetCurrentUserId();
        var rules = await _ruleService.GetRulesAsync(userId);
        return Ok(rules.Select(MapToResponse));
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetRule(int id)
    {
        var userId = GetCurrentUserId();
        var rule = await _ruleService.GetRuleByIdAsync(id, userId);

        if (rule == null)
            return NotFound(new { error = $"Classification rule with ID {id} was not found." });

        return Ok(MapToResponse(rule));
    }

    [HttpPost]
    public async Task<IActionResult> CreateRule([FromBody] CreateClassificationRuleRequest request)
    {
        var userId = GetCurrentUserId();
        var rule = MapToEntity(request, userId);

        var created = await _ruleService.CreateRuleAsync(rule);
        return CreatedAtAction(nameof(GetRule), new { id = created.Id }, MapToResponse(created));
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdateRule(int id, [FromBody] UpdateClassificationRuleRequest request)
    {
        var userId = GetCurrentUserId();
        var updated = MapToEntity(request, userId);

        var rule = await _ruleService.UpdateRuleAsync(id, userId, updated);

        if (rule == null)
            return NotFound(new { error = $"Classification rule with ID {id} was not found." });

        return Ok(MapToResponse(rule));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteRule(int id)
    {
        var userId = GetCurrentUserId();
        var deleted = await _ruleService.DeleteRuleAsync(id, userId);

        if (!deleted)
            return NotFound(new { error = $"Classification rule with ID {id} was not found." });

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

    private static ClassificationRule MapToEntity(CreateClassificationRuleRequest request, string userId) =>
        new()
        {
            UserId = userId,
            Keyword = request.Keyword,
            CategoryId = request.CategoryId,
            Priority = request.Priority
        };

    private static ClassificationRuleResponse MapToResponse(ClassificationRule rule) =>
        new()
        {
            Id = rule.Id,
            Keyword = rule.Keyword,
            CategoryId = rule.CategoryId,
            CategoryName = rule.Category?.Name,
            Priority = rule.Priority,
            CreatedAt = rule.CreatedAt,
            UpdatedAt = rule.UpdatedAt
        };
}
