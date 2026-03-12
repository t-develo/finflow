using System.Text;
using FinFlow.Infrastructure.Services.CsvParsing;
using FluentAssertions;

namespace FinFlow.Tests.CsvParsing;

[Trait("Category", "CsvParser")]
public class MufgCsvParserTests
{
    private readonly MufgCsvParser _parser = new();

    // =====================================================================
    // CanParse のテスト
    // =====================================================================

    [Fact]
    public void CanParse_WithMufgHeader_ReturnsTrue()
    {
        // Arrange
        var headerLine = "日付,摘要,お支払い金額,お預かり金額,残高,メモ";

        // Act
        var canParse = _parser.CanParse(headerLine);

        // Assert
        canParse.Should().BeTrue();
    }

    [Fact]
    public void CanParse_WithAlternativeMufgHeader_ReturnsTrue()
    {
        // Arrange: 「支払金額」バリエーション
        var headerLine = "日付,摘要,支払金額,入金額,残高";

        // Act
        var canParse = _parser.CanParse(headerLine);

        // Assert
        canParse.Should().BeTrue();
    }

    [Fact]
    public void CanParse_WithGenericHeader_ReturnsFalse()
    {
        // Arrange: 汎用フォーマットのヘッダー
        var headerLine = "date,description,amount,categoryId";

        // Act
        var canParse = _parser.CanParse(headerLine);

        // Assert
        canParse.Should().BeFalse();
    }

    [Fact]
    public void CanParse_WithEmptyHeader_ReturnsFalse()
    {
        canParse_WithEmptyHeader_AssertsFalse(string.Empty);
        canParse_WithEmptyHeader_AssertsFalse("   ");
    }

    private void canParse_WithEmptyHeader_AssertsFalse(string headerLine)
    {
        _parser.CanParse(headerLine).Should().BeFalse();
    }

    // =====================================================================
    // Parse 正常系テスト
    // =====================================================================

    [Fact]
    public void Parse_WithValidMufgCsv_ReturnsParsedExpenses()
    {
        // Arrange: 正常なMUFG形式のCSV（お支払い金額のある行のみ取込）
        var csvContent = """
            日付,摘要,お支払い金額,お預かり金額,残高,メモ
            2026/03/01,コンビニ 渋谷店,1200,,100000,
            2026/03/05,電気代,5400,,94600,
            """;
        var stream = CreateStream(csvContent);

        // Act
        var results = _parser.Parse(stream).ToList();

        // Assert
        var successResults = results.Where(r => r.IsSuccess).ToList();
        successResults.Should().HaveCount(2);
        successResults[0].Expense!.Amount.Should().Be(1200m);
        successResults[0].Expense!.Description.Should().Be("コンビニ 渋谷店");
        successResults[0].Expense!.Date.Should().Be(new DateOnly(2026, 3, 1));
        successResults[0].Expense!.ImportSource.Should().Be("mufg");
    }

    [Fact]
    public void Parse_WithIncomeRows_SkipsIncomeRows()
    {
        // Arrange: 収入行（お支払い金額が空）はスキップされること
        var csvContent = """
            日付,摘要,お支払い金額,お預かり金額,残高,メモ
            2026/03/01,コンビニ,1200,,100000,
            2026/03/15,給与,,200000,298800,
            2026/03/20,電気代,3000,,295800,
            """;
        var stream = CreateStream(csvContent);

        // Act
        var results = _parser.Parse(stream).ToList();

        // Assert: 収入行はスキップ（エラーにも成功にも含まれない）
        var successResults = results.Where(r => r.IsSuccess).ToList();
        successResults.Should().HaveCount(2);
        successResults.Select(r => r.Expense!.Description)
            .Should().ContainInOrder("コンビニ", "電気代");
    }

    [Fact]
    public void Parse_WithCommaFormattedAmount_ParsesCorrectly()
    {
        // Arrange: 金額がカンマ区切りの場合（例: 1,234,567）
        var csvContent = """
            日付,摘要,お支払い金額,お預かり金額,残高,メモ
            2026/03/01,家賃,"150,000",,50000,
            """;
        var stream = CreateStream(csvContent);

        // Act
        var results = _parser.Parse(stream).ToList();

        // Assert
        results.Should().HaveCount(1);
        results[0].IsSuccess.Should().BeTrue();
        results[0].Expense!.Amount.Should().Be(150000m);
    }

    // =====================================================================
    // Parse エラー系テスト
    // =====================================================================

    [Fact]
    public void Parse_WithInvalidDate_ReturnsErrorResult()
    {
        // Arrange: 不正な日付
        var csvContent = """
            日付,摘要,お支払い金額,お預かり金額,残高,メモ
            invalid-date,コンビニ,1200,,100000,
            """;
        var stream = CreateStream(csvContent);

        // Act
        var results = _parser.Parse(stream).ToList();

        // Assert
        results.Should().HaveCount(1);
        results[0].IsSuccess.Should().BeFalse();
        results[0].ErrorMessage.Should().Contain("日付");
    }

    [Fact]
    public void Parse_WithInvalidAmount_ReturnsErrorResult()
    {
        // Arrange: 不正な金額
        var csvContent = """
            日付,摘要,お支払い金額,お預かり金額,残高,メモ
            2026/03/01,コンビニ,invalid,,100000,
            """;
        var stream = CreateStream(csvContent);

        // Act
        var results = _parser.Parse(stream).ToList();

        // Assert
        results.Should().HaveCount(1);
        results[0].IsSuccess.Should().BeFalse();
        results[0].ErrorMessage.Should().Contain("金額");
    }

    // =====================================================================
    // Helpers
    // =====================================================================

    private static Stream CreateStream(string content)
    {
        var bytes = Encoding.UTF8.GetBytes(content);
        return new MemoryStream(bytes);
    }
}
