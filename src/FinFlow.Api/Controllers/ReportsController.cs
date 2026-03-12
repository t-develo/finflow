using System.Security.Claims;
using FinFlow.Domain.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinFlow.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ReportsController : ControllerBase
{
    private readonly IReportService _reportService;
    private readonly IPdfReportGenerator _pdfReportGenerator;

    public ReportsController(IReportService reportService, IPdfReportGenerator pdfReportGenerator)
    {
        _reportService = reportService;
        _pdfReportGenerator = pdfReportGenerator;
    }

    private string GetUserId() =>
        User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new UnauthorizedAccessException("User ID not found in token.");

    /// <summary>月次集計を取得する</summary>
    [HttpGet("monthly")]
    public async Task<IActionResult> GetMonthlyReport([FromQuery] int year, [FromQuery] int month)
    {
        if (!IsValidYearMonth(year, month))
            return BadRequest("year は 2000〜2099、month は 1〜12 の範囲で指定してください。");

        var userId = GetUserId();
        var report = await _reportService.GetMonthlyReportAsync(userId, year, month);

        var daysInMonth = DateTime.DaysInMonth(year, month);
        var dailyAverage = report.TotalAmount == 0m
            ? 0m
            : Math.Round(report.TotalAmount / daysInMonth, 0, MidpointRounding.AwayFromZero);

        var response = new MonthlyReportResponse(
            report.Year,
            report.Month,
            report.TotalAmount,
            report.ExpenseCount,
            (int)dailyAverage,
            report.CategoryBreakdown.Select(c => new CategoryBreakdownResponse(
                c.CategoryId,
                c.CategoryName,
                c.TotalAmount,
                c.Count,
                c.Percentage
            ))
        );

        return Ok(response);
    }

    /// <summary>カテゴリ別集計を取得する（円グラフ用にcolorを含む）</summary>
    [HttpGet("by-category")]
    public async Task<IActionResult> GetCategoryBreakdown([FromQuery] int year, [FromQuery] int month)
    {
        if (!IsValidYearMonth(year, month))
            return BadRequest("year は 2000〜2099、month は 1〜12 の範囲で指定してください。");

        var userId = GetUserId();
        var breakdown = await _reportService.GetCategoryBreakdownAsync(userId, year, month);

        var response = new CategoryBreakdownReportResponse(
            year,
            month,
            breakdown.Select(c => new CategoryBreakdownWithColorResponse(
                c.CategoryId,
                c.CategoryName,
                c.CategoryColor,
                c.TotalAmount,
                c.Count,
                c.Percentage
            ))
        );

        return Ok(response);
    }

    /// <summary>月次レポートをPDF形式でダウンロードする</summary>
    [HttpGet("monthly/pdf")]
    public async Task<IActionResult> GetMonthlyReportPdf([FromQuery] int year, [FromQuery] int month)
    {
        if (!IsValidYearMonth(year, month))
            return BadRequest("year は 2000〜2099、month は 1〜12 の範囲で指定してください。");

        var userId = GetUserId();
        var report = await _reportService.GetMonthlyReportAsync(userId, year, month);
        var pdfBytes = await _pdfReportGenerator.GenerateMonthlyReportAsync(report);

        var fileName = $"finflow-report-{year}-{month:D2}.pdf";
        return File(pdfBytes, "application/pdf", fileName);
    }

    private static bool IsValidYearMonth(int year, int month) =>
        year is >= 2000 and <= 2099 && month is >= 1 and <= 12;
}

public record MonthlyReportResponse(
    int Year,
    int Month,
    decimal TotalAmount,
    int TotalCount,
    int DailyAverage,
    IEnumerable<CategoryBreakdownResponse> CategoryBreakdown
);

public record CategoryBreakdownResponse(
    int CategoryId,
    string CategoryName,
    decimal Amount,
    int Count,
    decimal Percentage
);

public record CategoryBreakdownReportResponse(
    int Year,
    int Month,
    IEnumerable<CategoryBreakdownWithColorResponse> Categories
);

public record CategoryBreakdownWithColorResponse(
    int CategoryId,
    string CategoryName,
    string CategoryColor,
    decimal TotalAmount,
    int Count,
    decimal Percentage
);
