using System.Security.Claims;
using FinFlow.Api.Models;
using FinFlow.Domain.Entities;
using FinFlow.Domain.Interfaces;
using FinFlow.Infrastructure.Services.CsvParsing;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinFlow.Api.Controllers;

[ApiController]
[Route("api/expenses")]
[Authorize]
public class ExpensesController : ControllerBase
{
    private readonly IExpenseService _expenseService;
    private readonly CsvParserFactory _csvParserFactory;

    public ExpensesController(IExpenseService expenseService, CsvParserFactory csvParserFactory)
    {
        _expenseService = expenseService;
        _csvParserFactory = csvParserFactory;
    }

    [HttpGet]
    public async Task<IActionResult> GetExpenses(
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        [FromQuery] int? categoryId,
        [FromQuery] decimal? minAmount,
        [FromQuery] decimal? maxAmount,
        [FromQuery] string? keyword,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        var userId = GetCurrentUserId();
        var filter = new ExpenseFilter
        {
            From = from,
            To = to,
            CategoryId = categoryId,
            MinAmount = minAmount,
            MaxAmount = maxAmount,
            Keyword = keyword,
            Page = page,
            PageSize = pageSize
        };

        var expenses = await _expenseService.GetExpensesAsync(userId, filter);
        var expenseList = expenses.ToList();

        // S2-A-004: ページネーションメタデータをレスポンスに含める
        var response = new
        {
            data = expenseList.Select(MapToResponse),
            pagination = new
            {
                page,
                pageSize,
                // 次ページの存在チェック（pageSize件返ってきた場合は次ページがある可能性あり）
                hasNextPage = expenseList.Count == pageSize
            }
        };
        return Ok(response);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetExpense(int id)
    {
        var userId = GetCurrentUserId();
        var expense = await _expenseService.GetExpenseByIdAsync(id, userId);

        if (expense == null)
            return NotFound(new { error = $"Expense with ID {id} was not found." });

        return Ok(MapToResponse(expense));
    }

    [HttpPost]
    public async Task<IActionResult> CreateExpense([FromBody] CreateExpenseRequest request)
    {
        var userId = GetCurrentUserId();
        var expense = MapToEntity(request, userId);

        var created = await _expenseService.CreateExpenseAsync(expense);
        return CreatedAtAction(nameof(GetExpense), new { id = created.Id }, MapToResponse(created));
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdateExpense(int id, [FromBody] UpdateExpenseRequest request)
    {
        var userId = GetCurrentUserId();
        var updated = MapToEntity(request, userId);

        var expense = await _expenseService.UpdateExpenseAsync(id, userId, updated);

        if (expense == null)
            return NotFound(new { error = $"Expense with ID {id} was not found." });

        return Ok(MapToResponse(expense));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteExpense(int id)
    {
        var userId = GetCurrentUserId();
        var deleted = await _expenseService.DeleteExpenseAsync(id, userId);

        if (!deleted)
            return NotFound(new { error = $"Expense with ID {id} was not found." });

        return NoContent();
    }

    [HttpPost("import")]
    public async Task<IActionResult> ImportExpenses(
        IFormFile file,
        [FromQuery] string? bankFormat = null)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { error = "No file uploaded." });

        var userId = GetCurrentUserId();

        using var stream = file.OpenReadStream();
        var parser = _csvParserFactory.SelectParser(stream, "utf-8");
        var parseResults = parser.Parse(stream, "utf-8").ToList();

        var expenses = parseResults
            .Where(r => r.IsSuccess && r.Expense != null)
            .Select(r => r.Expense!)
            .ToList();

        var parseErrors = parseResults
            .Where(r => !r.IsSuccess)
            .Select(r => r.ErrorMessage ?? $"Row {r.RowNumber}: unknown error")
            .ToList();

        var result = await _expenseService.ImportExpensesAsync(expenses, userId);

        return Ok(new
        {
            imported = result.Imported,
            skipped = result.Skipped + parseErrors.Count,
            errors = parseErrors.Concat(result.Errors).ToList()
        });
    }

    private string GetCurrentUserId()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub");

        if (string.IsNullOrEmpty(userId))
            throw new UnauthorizedAccessException("User ID not found in token.");

        return userId;
    }

    private static Expense MapToEntity(CreateExpenseRequest request, string userId) =>
        new()
        {
            UserId = userId,
            Amount = request.Amount,
            CategoryId = request.CategoryId,
            Date = request.Date,
            Description = request.Description,
        };

    private static ExpenseResponse MapToResponse(Expense expense) =>
        new()
        {
            Id = expense.Id,
            Amount = expense.Amount,
            CategoryId = expense.CategoryId,
            CategoryName = expense.Category?.Name,
            Date = expense.Date,
            Description = expense.Description ?? string.Empty,
            CreatedAt = expense.CreatedAt,
            UpdatedAt = expense.UpdatedAt
        };
}
