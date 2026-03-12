using FinFlow.Domain.Interfaces;
using FinFlow.Infrastructure.Services;
using FluentAssertions;

namespace FinFlow.Tests.Reports;

[Trait("Category", "PdfReport")]
public class PdfReportGeneratorTests
{
    private readonly QuestPdfReportGenerator _generator = new();

    [Fact]
    public async Task GenerateMonthlyReportAsync_WithValidData_ReturnsPdfBytes()
    {
        // Arrange: 正常なレポートデータ
        var report = new MonthlyReportDto(
            Year: 2026,
            Month: 3,
            TotalAmount: 50000m,
            ExpenseCount: 15,
            CategoryBreakdown: new[]
            {
                new CategoryBreakdownDto(1, "食費", "#3B82F6", 20000m, 8, 40.0m),
                new CategoryBreakdownDto(2, "交通費", "#10B981", 15000m, 4, 30.0m),
                new CategoryBreakdownDto(3, "光熱費", "#F59E0B", 15000m, 3, 30.0m)
            }
        );

        // Act
        var pdfBytes = await _generator.GenerateMonthlyReportAsync(report);

        // Assert: PDFバイト列が返ること（%PDF-ヘッダーで始まること）
        pdfBytes.Should().NotBeNull();
        pdfBytes.Should().HaveCountGreaterThan(0);

        // PDFマジックバイト（%PDF-）の確認
        var pdfHeader = System.Text.Encoding.ASCII.GetString(pdfBytes.Take(4).ToArray());
        pdfHeader.Should().Be("%PDF");
    }

    [Fact]
    public async Task GenerateMonthlyReportAsync_WithEmptyData_ReturnsPdfBytes()
    {
        // Arrange: 支出0件のレポート（エッジケース）
        var report = new MonthlyReportDto(
            Year: 2026,
            Month: 2,
            TotalAmount: 0m,
            ExpenseCount: 0,
            CategoryBreakdown: Enumerable.Empty<CategoryBreakdownDto>()
        );

        // Act
        var pdfBytes = await _generator.GenerateMonthlyReportAsync(report);

        // Assert: 空データでもPDFが生成されること
        pdfBytes.Should().NotBeNull();
        pdfBytes.Should().HaveCountGreaterThan(0);
        var pdfHeader = System.Text.Encoding.ASCII.GetString(pdfBytes.Take(4).ToArray());
        pdfHeader.Should().Be("%PDF");
    }

    [Fact]
    public async Task GenerateMonthlyReportAsync_WithLargeAmounts_ReturnsPdfBytes()
    {
        // Arrange: 大きな金額（フォーマット境界値テスト）
        var report = new MonthlyReportDto(
            Year: 2026,
            Month: 12,
            TotalAmount: 9999999m,
            ExpenseCount: 1000,
            CategoryBreakdown: new[]
            {
                new CategoryBreakdownDto(1, "その他", "#6B7280", 9999999m, 1000, 100.0m)
            }
        );

        // Act & Assert: エラーなく処理されること
        var act = async () => await _generator.GenerateMonthlyReportAsync(report);
        await act.Should().NotThrowAsync();
    }
}
