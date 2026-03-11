using System.Text;
using FinFlow.Domain.Interfaces;
using FinFlow.Infrastructure.Services.CsvParsing;
using FluentAssertions;

namespace FinFlow.Tests.CsvParsing;

[Trait("Category", "CsvParser")]
public class CsvParserTests
{
    private readonly GenericCsvParser _parser = new();

    // =====================================================================
    // CanParse のテスト
    // =====================================================================

    [Fact]
    public void CanParse_WithValidGenericHeader_ReturnsTrue()
    {
        // Arrange
        var headerLine = "date,description,amount,categoryId";

        // Act
        var canParse = _parser.CanParse(headerLine);

        // Assert
        canParse.Should().BeTrue();
    }

    [Fact]
    public void CanParse_WithUnknownHeader_ReturnsFalse()
    {
        // Arrange: 汎用フォーマットに必須のフィールドが存在しない
        var headerLine = "振込日,摘要,金額,残高";

        // Act
        var canParse = _parser.CanParse(headerLine);

        // Assert
        canParse.Should().BeFalse();
    }

    [Fact]
    public void CanParse_WithEmptyHeader_ReturnsFalse()
    {
        // Arrange
        var headerLine = string.Empty;

        // Act
        var canParse = _parser.CanParse(headerLine);

        // Assert
        canParse.Should().BeFalse();
    }

    // =====================================================================
    // Parse 正常系テスト
    // =====================================================================

    [Fact]
    public void Parse_WithValidSingleRow_ReturnsParsedExpense()
    {
        // Arrange
        const string csvContent = "date,description,amount,categoryId\r\n2026-03-08,コンビニ 昼食,1500,1";
        var stream = CreateUtf8Stream(csvContent);

        // Act
        var results = _parser.Parse(stream).ToList();

        // Assert
        results.Should().HaveCount(1);
        var result = results[0];
        result.IsSuccess.Should().BeTrue();
        result.Expense.Should().NotBeNull();
        result.Expense!.Amount.Should().Be(1500m);
        result.Expense.Description.Should().Be("コンビニ 昼食");
        result.Expense.Date.Should().Be(new DateOnly(2026, 3, 8));
        result.Expense.CategoryId.Should().Be(1);
    }

    [Fact]
    public void Parse_WithMultipleValidRows_ReturnsAllParsedExpenses()
    {
        // Arrange
        const string csvContent =
            "date,description,amount,categoryId\r\n" +
            "2026-03-08,コンビニ,1500,1\r\n" +
            "2026-03-09,電車代,240,2\r\n" +
            "2026-03-10,電気代,8000,6";
        var stream = CreateUtf8Stream(csvContent);

        // Act
        var results = _parser.Parse(stream).ToList();

        // Assert
        results.Should().HaveCount(3);
        results.Should().AllSatisfy(r => r.IsSuccess.Should().BeTrue());
        results[0].Expense!.Description.Should().Be("コンビニ");
        results[1].Expense!.Amount.Should().Be(240m);
        results[2].Expense!.Date.Should().Be(new DateOnly(2026, 3, 10));
    }

    [Fact]
    public void Parse_WithNoCategoryId_ReturnsParsedExpenseWithNullCategoryId()
    {
        // Arrange: categoryIdが空の場合はnullとして処理する
        const string csvContent = "date,description,amount,categoryId\r\n2026-03-08,その他支出,500,";
        var stream = CreateUtf8Stream(csvContent);

        // Act
        var results = _parser.Parse(stream).ToList();

        // Assert
        results.Should().HaveCount(1);
        results[0].IsSuccess.Should().BeTrue();
        results[0].Expense!.CategoryId.Should().BeNull();
    }

    [Fact]
    public void Parse_WithShiftJisEncoding_ReturnsParsedExpense()
    {
        // Arrange: Shift_JISエンコードのCSV
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var shiftJisEncoding = Encoding.GetEncoding("shift_jis");
        const string csvContent = "date,description,amount,categoryId\r\n2026-03-08,スーパー食料品,3200,1";
        var stream = new MemoryStream(shiftJisEncoding.GetBytes(csvContent));

        // Act
        var results = _parser.Parse(stream, "shift_jis").ToList();

        // Assert
        results.Should().HaveCount(1);
        results[0].IsSuccess.Should().BeTrue();
        results[0].Expense!.Description.Should().Be("スーパー食料品");
    }

    // =====================================================================
    // Parse 異常系テスト（エラー行スキップ）
    // =====================================================================

    [Fact]
    public void Parse_WithInvalidAmountRow_SkipsErrorRowAndContinues()
    {
        // Arrange: 2行目の金額が不正だが、3行目は正常
        const string csvContent =
            "date,description,amount,categoryId\r\n" +
            "2026-03-08,正常行,1500,1\r\n" +
            "2026-03-09,金額エラー行,abc,1\r\n" +
            "2026-03-10,別の正常行,800,2";
        var stream = CreateUtf8Stream(csvContent);

        // Act
        var results = _parser.Parse(stream).ToList();

        // Assert: エラー行はスキップされるが結果オブジェクトには残る
        results.Should().HaveCount(3);
        results[0].IsSuccess.Should().BeTrue();
        results[1].IsSuccess.Should().BeFalse();
        results[1].ErrorMessage.Should().Contain("abc");
        results[2].IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void Parse_WithNegativeAmount_SkipsErrorRow()
    {
        // Arrange: 負の金額は不正
        const string csvContent = "date,description,amount,categoryId\r\n2026-03-08,負の金額,-100,1";
        var stream = CreateUtf8Stream(csvContent);

        // Act
        var results = _parser.Parse(stream).ToList();

        // Assert
        results.Should().HaveCount(1);
        results[0].IsSuccess.Should().BeFalse();
        results[0].ErrorMessage.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void Parse_WithInvalidDateRow_SkipsErrorRow()
    {
        // Arrange: 日付形式が不正
        const string csvContent = "date,description,amount,categoryId\r\ninvalid-date,日付エラー,1000,1";
        var stream = CreateUtf8Stream(csvContent);

        // Act
        var results = _parser.Parse(stream).ToList();

        // Assert
        results.Should().HaveCount(1);
        results[0].IsSuccess.Should().BeFalse();
        results[0].ErrorMessage.Should().Contain("invalid-date");
    }

    [Fact]
    public void Parse_WithEmptyDescription_SkipsErrorRow()
    {
        // Arrange: 説明が空
        const string csvContent = "date,description,amount,categoryId\r\n2026-03-08,,1500,1";
        var stream = CreateUtf8Stream(csvContent);

        // Act
        var results = _parser.Parse(stream).ToList();

        // Assert
        results.Should().HaveCount(1);
        results[0].IsSuccess.Should().BeFalse();
        results[0].ErrorMessage.Should().NotBeNullOrEmpty();
    }

    // =====================================================================
    // CsvParserFactory のテスト
    // =====================================================================

    [Fact]
    public void CsvParserFactory_SelectParser_WithGenericHeader_ReturnsGenericCsvParser()
    {
        // Arrange
        var factory = new CsvParserFactory(new ICsvParser[] { new GenericCsvParser() });
        const string csvContent = "date,description,amount,categoryId\r\n2026-03-08,test,100,1";
        var stream = CreateUtf8Stream(csvContent);

        // Act
        var parser = factory.SelectParser(stream);

        // Assert
        parser.Should().BeOfType<GenericCsvParser>();
    }

    [Fact]
    public void CsvParserFactory_SelectParser_WithUnknownHeader_FallsBackToGenericParser()
    {
        // Arrange: 登録済みパーサーがマッチしないヘッダー
        var factory = new CsvParserFactory(Enumerable.Empty<ICsvParser>());
        const string csvContent = "振込日,摘要,金額,残高\r\n2026-03-08,テスト,100,10000";
        var stream = CreateUtf8Stream(csvContent);

        // Act: フォールバックでGenericCsvParserが返されることを確認
        var parser = factory.SelectParser(stream);

        // Assert
        parser.Should().BeOfType<GenericCsvParser>();
    }

    // =====================================================================
    // ヘルパー
    // =====================================================================

    private static MemoryStream CreateUtf8Stream(string content) =>
        new(Encoding.UTF8.GetBytes(content));
}
