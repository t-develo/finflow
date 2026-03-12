using System.Text;
using FinFlow.Infrastructure.Services.CsvParsing;
using FluentAssertions;

namespace FinFlow.Tests.CsvParsing;

[Trait("Category", "CsvParser")]
public class RakutenCsvParserTests
{
    private readonly RakutenCsvParser _parser = new();

    // =====================================================================
    // CanParse のテスト
    // =====================================================================

    [Fact]
    public void CanParse_WithRakutenHeader_ReturnsTrue()
    {
        // Arrange: 楽天カード標準ヘッダー
        var headerLine = "利用日,利用店名・商品名,利用者,支払方法,利用金額,支払金額";

        // Act
        var canParse = _parser.CanParse(headerLine);

        // Assert
        canParse.Should().BeTrue();
    }

    [Fact]
    public void CanParse_WithSimplifiedRakutenHeader_ReturnsTrue()
    {
        // Arrange: 簡略版ヘッダー
        var headerLine = "利用日,利用店名,利用金額";

        // Act
        var canParse = _parser.CanParse(headerLine);

        // Assert
        canParse.Should().BeTrue();
    }

    [Fact]
    public void CanParse_WithMufgHeader_ReturnsFalse()
    {
        // Arrange: MUFGフォーマットのヘッダー
        var headerLine = "日付,摘要,お支払い金額,お預かり金額,残高";

        // Act
        var canParse = _parser.CanParse(headerLine);

        // Assert
        canParse.Should().BeFalse();
    }

    // =====================================================================
    // Parse 正常系テスト
    // =====================================================================

    [Fact]
    public void Parse_WithValidRakutenCsv_ReturnsParsedExpenses()
    {
        // Arrange: 正常な楽天カード形式のCSV
        var csvContent = """
            利用日,利用店名・商品名,利用者,支払方法,利用金額,支払金額
            2026/03/01,Amazon Japan,本人,1回払い,3980,3980
            2026/03/05,Spotify Japan,本人,1回払い,980,980
            """;
        var stream = CreateStream(csvContent);

        // Act
        var results = _parser.Parse(stream).ToList();

        // Assert
        var successResults = results.Where(r => r.IsSuccess).ToList();
        successResults.Should().HaveCount(2);
        successResults[0].Expense!.Amount.Should().Be(3980m);
        successResults[0].Expense!.Description.Should().Be("Amazon Japan");
        successResults[0].Expense!.Date.Should().Be(new DateOnly(2026, 3, 1));
        successResults[0].Expense!.ImportSource.Should().Be("rakuten");
    }

    [Fact]
    public void Parse_WithCommaFormattedAmount_ParsesCorrectly()
    {
        // Arrange: 金額がカンマ区切りの場合
        var csvContent = """
            利用日,利用店名・商品名,利用者,支払方法,利用金額,支払金額
            2026/03/01,旅行代金,本人,1回払い,"120,000","120,000"
            """;
        var stream = CreateStream(csvContent);

        // Act
        var results = _parser.Parse(stream).ToList();

        // Assert
        results.Should().HaveCount(1);
        results[0].IsSuccess.Should().BeTrue();
        results[0].Expense!.Amount.Should().Be(120000m);
    }

    // =====================================================================
    // Parse エラー系テスト
    // =====================================================================

    [Fact]
    public void Parse_WithInvalidDate_ReturnsErrorResult()
    {
        // Arrange: 不正な日付
        var csvContent = """
            利用日,利用店名・商品名,利用者,支払方法,利用金額,支払金額
            not-a-date,Amazon,本人,1回払い,980,980
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
    public void Parse_WithZeroAmount_ReturnsErrorResult()
    {
        // Arrange: 0円の取引
        var csvContent = """
            利用日,利用店名・商品名,利用者,支払方法,利用金額,支払金額
            2026/03/01,調整,本人,1回払い,0,0
            """;
        var stream = CreateStream(csvContent);

        // Act
        var results = _parser.Parse(stream).ToList();

        // Assert: 0円はエラー扱い
        results.Should().HaveCount(1);
        results[0].IsSuccess.Should().BeFalse();
    }

    [Fact]
    public void Parse_WithEmptyDescriptionRow_UsesDefaultDescription()
    {
        // Arrange: 利用店名が空の行
        var csvContent = """
            利用日,利用店名・商品名,利用者,支払方法,利用金額,支払金額
            2026/03/01,,本人,1回払い,500,500
            """;
        var stream = CreateStream(csvContent);

        // Act
        var results = _parser.Parse(stream).ToList();

        // Assert: デフォルト説明文が使用される
        results.Should().HaveCount(1);
        results[0].IsSuccess.Should().BeTrue();
        results[0].Expense!.Description.Should().Be("楽天カード利用");
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
