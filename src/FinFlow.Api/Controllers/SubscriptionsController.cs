using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using FinFlow.Domain.Entities;
using FinFlow.Domain.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinFlow.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class SubscriptionsController : ControllerBase
{
    private readonly ISubscriptionService _subscriptionService;

    public SubscriptionsController(ISubscriptionService subscriptionService)
    {
        _subscriptionService = subscriptionService;
    }

    private string GetUserId() =>
        User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new UnauthorizedAccessException("User ID not found in token.");

    /// <summary>サブスクリプション一覧を取得する</summary>
    [HttpGet]
    public async Task<IActionResult> GetSubscriptions()
    {
        var userId = GetUserId();
        var subscriptions = await _subscriptionService.GetSubscriptionsAsync(userId);

        var response = subscriptions.Select(MapToResponse);
        return Ok(response);
    }

    /// <summary>サブスクリプション詳細を取得する</summary>
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetSubscription(int id)
    {
        var userId = GetUserId();
        var subscription = await _subscriptionService.GetSubscriptionByIdAsync(id, userId);

        if (subscription is null) return NotFound();

        return Ok(MapToResponse(subscription));
    }

    /// <summary>サブスクリプションを登録する</summary>
    [HttpPost]
    public async Task<IActionResult> CreateSubscription([FromBody] SubscriptionRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var userId = GetUserId();
        var subscription = MapToEntity(request, userId);

        var created = await _subscriptionService.CreateSubscriptionAsync(subscription);

        return CreatedAtAction(
            nameof(GetSubscription),
            new { id = created.Id },
            MapToResponse(created));
    }

    /// <summary>サブスクリプションを更新する</summary>
    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdateSubscription(int id, [FromBody] SubscriptionRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var userId = GetUserId();
        var updated = MapToEntity(request, userId);

        var result = await _subscriptionService.UpdateSubscriptionAsync(id, userId, updated);

        if (result is null) return NotFound();

        return Ok(MapToResponse(result));
    }

    /// <summary>サブスクリプションを削除する</summary>
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteSubscription(int id)
    {
        var userId = GetUserId();
        var deleted = await _subscriptionService.DeleteSubscriptionAsync(id, userId);

        if (!deleted) return NotFound();

        return NoContent();
    }

    private static Subscription MapToEntity(SubscriptionRequest request, string userId) =>
        new()
        {
            UserId = userId,
            ServiceName = request.ServiceName,
            Amount = request.Amount,
            CategoryId = request.CategoryId,
            BillingCycle = request.BillingCycle,
            NextBillingDate = request.NextBillingDate,
            IsActive = request.IsActive,
            Notes = request.Description
        };

    private static SubscriptionResponse MapToResponse(Subscription s) =>
        new(
            s.Id,
            s.ServiceName,
            s.Amount,
            s.CategoryId,
            s.Category?.Name,
            s.BillingCycle,
            s.NextBillingDate,
            s.Notes,
            s.IsActive,
            s.CreatedAt,
            s.UpdatedAt
        );
}

public record SubscriptionRequest(
    [Required, StringLength(100, MinimumLength = 1)] string ServiceName,
    [Range(0.01, double.MaxValue, ErrorMessage = "Amount must be a positive number.")] decimal Amount,
    int? CategoryId,
    [Required, RegularExpression("^(monthly|yearly|weekly)$",
        ErrorMessage = "BillingCycle must be 'monthly', 'yearly', or 'weekly'.")] string BillingCycle,
    DateOnly NextBillingDate,
    string? Description,
    bool IsActive = true
);

public record SubscriptionResponse(
    int Id,
    string ServiceName,
    decimal Amount,
    int? CategoryId,
    string? CategoryName,
    string BillingCycle,
    DateOnly NextBillingDate,
    string? Description,
    bool IsActive,
    DateTime CreatedAt,
    DateTime UpdatedAt
);
