using FinFlow.Domain.Entities;
using FinFlow.Domain.Interfaces;
using FinFlow.Infrastructure.Data;
using FinFlow.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace FinFlow.Tests.Reports;

/// <summary>
/// PDF レポート生成の E2E テスト。
/// ReportService でデータを集計し、QuestPdfReportGenerator でPDFを生成するフローを検証する。
/// </summary>
[Trait("Category", "PdfReportE2E")]
public class PdfReportE2ETests : IDisposable
{
    private readonly FinFlowDbContext _context;
    private readonly ReportService _reportService;
    private readonly QuestPdfReportGenerator _pdfGenerator;
    private const string TestUserId = "user-pdf-e2e";

    public PdfReportE2ETests()
    {
        var options = new DbContextOptionsBuilder<FinFlowDbContext>()
            .UseInMemoryDatabase($"PdfReportE2ETest_{Guid.NewGuid()}")
            .Options;
        _context = new FinFlowDbContext(options);
        _context.Database.EnsureCreated();
        _reportService = new ReportService(_context);
        _pdfGenerator = new QuestPdfReportGenerator();
    }

    public void Dispose() => _context.Dispose();

    private static Expense BuildExpense(int id, string userId, decimal amount, int year, int month, int day, int? categoryId = null) =>
        new()
        {
            Id = id,
            UserId = userId,
            Amount = amount,
            Date = new DateOnly(year, month, day),
            CategoryId = categoryId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

    // =====================================================================
    // S3-B-002-1: 月次レポートのPDF生成が正常に完了するか
    // =====================================================================

    [Fact]
    public async Task E2E_GenerateMonthlyPdf_WithExpenseData_ReturnsPdfBytes()
    {
        // Arrange: 2026年3月に複数カテゴリの支出を登録する
        // EnsureCreated() によってシードカテゴリ（食費=1、交通費=2）が存在する
        var expenses = new List<Expense>
        {
            BuildExpense(1, TestUserId, 15000m, 2026, 3, 5, categoryId: 1),  // 食費
            BuildExpense(2, TestUserId, 8000m, 2026, 3, 10, categoryId: 2),  // 交通費
            BuildExpense(3, TestUserId, 12000m, 2026, 3, 15, categoryId: 1), // 食費
            BuildExpense(4, TestUserId, 5000m, 2026, 3, 20, categoryId: 3),  // 娯楽
        };
        _context.Expenses.AddRange(expenses);
        await _context.SaveChangesAsync();

        // Act: ReportService でデータを集計し、PDF を生成する（E2Eフロー）
        var report = await _reportService.GetMonthlyReportAsync(TestUserId, 2026, 3);
        var pdfBytes = await _pdfGenerator.GenerateMonthlyReportAsync(report);

        // Assert: PDF バイト列が返ること
        pdfBytes.Should().NotBeNull();
        pdfBytes.Should().NotBeEmpty();
    }

    [Fact]
    public async Task E2E_GenerateMonthlyPdf_WithExpenseData_PdfStartsWithPdfMagicBytes()
    {
        // Arrange
        var expenses = new List<Expense>
        {
            BuildExpense(10, TestUserId, 30000m, 2026, 3, 1, categoryId: 1),
            BuildExpense(11, TestUserId, 10000m, 2026, 3, 5, categoryId: 6), // 光熱費
        };
        _context.Expenses.AddRange(expenses);
        await _context.SaveChangesAsync();

        // Act
        var report = await _reportService.GetMonthlyReportAsync(TestUserId, 2026, 3);
        var pdfBytes = await _pdfGenerator.GenerateMonthlyReportAsync(report);

        // Assert: PDF は "%PDF" で始まること（PDFマジックバイト）
        var header = System.Text.Encoding.ASCII.GetString(pdfBytes.Take(4).ToArray());
        header.Should().Be("%PDF");
    }

    [Fact]
    public async Task E2E_GenerateMonthlyPdf_WithExpenseData_PdfHasReasonableSize()
    {
        // Arrange: 10件の支出を登録する
        var expenses = Enumerable.Range(1, 10)
            .Select(i => BuildExpense(i + 200, TestUserId, i * 3000m, 2026, 3, i, categoryId: (i % 3) + 1))
            .ToList();
        _context.Expenses.AddRange(expenses);
        await _context.SaveChangesAsync();

        // Act
        var report = await _reportService.GetMonthlyReportAsync(TestUserId, 2026, 3);
        var pdfBytes = await _pdfGenerator.GenerateMonthlyReportAsync(report);

        // Assert: PDFのバイト数が最低限の閾値を超えること（最低1KB以上）
        pdfBytes.Length.Should().BeGreaterThan(1024, "PDFにはヘッダー・コンテンツ・フッターが含まれるため1KB以上のはず");
    }

    // =====================================================================
    // S3-B-002-2: レポートにデータが含まれているか（バイト数チェック等）
    // =====================================================================

    [Fact]
    public async Task E2E_GenerateMonthlyPdf_EmptyMonth_ReturnsValidButSmallerPdf()
    {
        // Arrange: データなし（空の月）
        // Act: ReportService はデータ0件の MonthlyReportDto を返す
        var report = await _reportService.GetMonthlyReportAsync(TestUserId, 2026, 1);
        var pdfBytes = await _pdfGenerator.GenerateMonthlyReportAsync(report);

        // Assert: データがなくてもPDFが生成されること
        pdfBytes.Should().NotBeNull();
        pdfBytes.Should().NotBeEmpty();
        var header = System.Text.Encoding.ASCII.GetString(pdfBytes.Take(4).ToArray());
        header.Should().Be("%PDF");
    }

    [Fact]
    public async Task E2E_GenerateMonthlyPdf_WithData_IsLargerThan_EmptyPdf()
    {
        // Arrange: 空月と支出あり月の両方でPDFを生成し、バイトサイズを比較する
        var emptyReport = await _reportService.GetMonthlyReportAsync(TestUserId, 2026, 1);
        var emptyPdfBytes = await _pdfGenerator.GenerateMonthlyReportAsync(emptyReport);

        // 支出あり月のデータを登録（複数カテゴリ）
        var expenses = new List<Expense>
        {
            BuildExpense(300, TestUserId, 50000m, 2026, 3, 1, categoryId: 1),
            BuildExpense(301, TestUserId, 30000m, 2026, 3, 5, categoryId: 2),
            BuildExpense(302, TestUserId, 20000m, 2026, 3, 10, categoryId: 3),
            BuildExpense(303, TestUserId, 15000m, 2026, 3, 15, categoryId: 4),
            BuildExpense(304, TestUserId, 10000m, 2026, 3, 20, categoryId: 5),
        };
        _context.Expenses.AddRange(expenses);
        await _context.SaveChangesAsync();

        var dataReport = await _reportService.GetMonthlyReportAsync(TestUserId, 2026, 3);
        var dataPdfBytes = await _pdfGenerator.GenerateMonthlyReportAsync(dataReport);

        // Assert: データありのPDFは空のPDFより大きいこと（コンテンツが含まれている証拠）
        dataPdfBytes.Length.Should().BeGreaterThan(emptyPdfBytes.Length,
            "カテゴリ別内訳テーブルの行が追加されるため、データありのPDFはサイズが大きくなるはず");
    }

    [Fact]
    public async Task E2E_GenerateMonthlyPdf_CorrectAmountInReport_MatchesExpectedTotal()
    {
        // Arrange: 合計金額が既知のデータを登録する（手計算との照合）
        // 食費 20000円 + 交通費 10000円 + 娯楽 5000円 = 合計 35000円
        var expenses = new List<Expense>
        {
            BuildExpense(400, TestUserId, 20000m, 2026, 3, 1, categoryId: 1),
            BuildExpense(401, TestUserId, 10000m, 2026, 3, 5, categoryId: 2),
            BuildExpense(402, TestUserId, 5000m, 2026, 3, 10, categoryId: 3),
        };
        _context.Expenses.AddRange(expenses);
        await _context.SaveChangesAsync();

        // Act: レポートデータを取得し、PDFを生成する
        var report = await _reportService.GetMonthlyReportAsync(TestUserId, 2026, 3);
        var pdfBytes = await _pdfGenerator.GenerateMonthlyReportAsync(report);

        // Assert: 集計結果が正確であること（PDFに正しいデータが含まれることを間接検証）
        report.TotalAmount.Should().Be(35000m);
        report.ExpenseCount.Should().Be(3);
        report.CategoryBreakdown.Should().HaveCount(3);
        pdfBytes.Should().NotBeEmpty();
    }

    [Fact]
    public async Task E2E_GenerateMonthlyPdf_MultipleCategories_AllCategoriesIncludedInReport()
    {
        // Arrange: 全8システムカテゴリに支出を設定する（多数カテゴリでのPDF生成テスト）
        var expenses = new List<Expense>();
        for (int catId = 1; catId <= 8; catId++)
        {
            expenses.Add(BuildExpense(500 + catId, TestUserId, catId * 5000m, 2026, 3, catId, categoryId: catId));
        }
        _context.Expenses.AddRange(expenses);
        await _context.SaveChangesAsync();

        // Act
        var report = await _reportService.GetMonthlyReportAsync(TestUserId, 2026, 3);
        var pdfBytes = await _pdfGenerator.GenerateMonthlyReportAsync(report);

        // Assert: 8カテゴリ全て集計されること
        report.CategoryBreakdown.Should().HaveCount(8);
        // 合計 = 1*5000 + 2*5000 + ... + 8*5000 = 5000*(1+2+...+8) = 5000*36 = 180000円
        report.TotalAmount.Should().Be(180000m);
        // PDFが正常に生成されること
        pdfBytes.Should().NotBeEmpty();
        var header = System.Text.Encoding.ASCII.GetString(pdfBytes.Take(4).ToArray());
        header.Should().Be("%PDF");
    }

    [Fact]
    public async Task E2E_GenerateMonthlyPdf_UserIsolation_OnlyCurrentUserDataInReport()
    {
        // Arrange: テストユーザーと別ユーザーの両方にデータを登録する
        var expenses = new List<Expense>
        {
            BuildExpense(600, TestUserId, 20000m, 2026, 3, 1, categoryId: 1),
            BuildExpense(601, "other-user-pdf", 100000m, 2026, 3, 1, categoryId: 1), // 別ユーザー
        };
        _context.Expenses.AddRange(expenses);
        await _context.SaveChangesAsync();

        // Act: TestUserId のレポートを生成する
        var report = await _reportService.GetMonthlyReportAsync(TestUserId, 2026, 3);
        var pdfBytes = await _pdfGenerator.GenerateMonthlyReportAsync(report);

        // Assert: TestUserId の合計のみが含まれること（他ユーザーのデータは除外される）
        report.TotalAmount.Should().Be(20000m);
        report.ExpenseCount.Should().Be(1);
        pdfBytes.Should().NotBeEmpty();
    }
}
