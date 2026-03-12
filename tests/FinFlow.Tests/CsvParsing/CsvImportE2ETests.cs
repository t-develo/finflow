using System.Text;
using FinFlow.Domain.Interfaces;
using FinFlow.Infrastructure.Services.CsvParsing;
using FluentAssertions;

namespace FinFlow.Tests.CsvParsing;

/// <summary>
/// CSVインポートのE2Eテスト（S3-A-001）
/// パーサー選択 → パース → 結果検証の一連の流れをテストする
/// </summary>
[Trait("Category", "CsvImportE2E")]
public class CsvImportE2ETests
{
    // =====================================================================
    // 汎用フォーマットCSVのインポートE2Eテスト
    // =====================================================================

    [Fact]
    public void GenericCsvImport_WithValidCsv_ParsesAllRows()
    {
        // Arrange: ファクトリーに全パーサーを登録してE2Eシナリオを再現する
        var parsers = new ICsvParser[]
        {
            new MufgCsvParser(),
            new RakutenCsvParser(),
            new GenericCsvParser()
        };
        var factory = new CsvParserFactory(parsers);

        var csvContent =
            "date,description,amount,categoryId\r\n" +
            "2026-03-01,スーパー食料品,3200,1\r\n" +
            "2026-03-05,電車代,470,2\r\n" +
            "2026-03-10,電気代,8500,6";
        var stream = CreateUtf8Stream(csvContent);

        // Act: ファクトリーで適切なパーサーを選択し、パースを実行する
        var parser = factory.SelectParser(stream);
        var results = parser.Parse(stream).ToList();

        // Assert: 汎用パーサーが選択され、全行が正常にパースされること
        parser.Should().BeOfType<GenericCsvParser>();
        results.Should().HaveCount(3);
        results.Should().AllSatisfy(r => r.IsSuccess.Should().BeTrue());

        results[0].Expense!.Amount.Should().Be(3200m);
        results[0].Expense!.Description.Should().Be("スーパー食料品");
        results[0].Expense!.Date.Should().Be(new DateOnly(2026, 3, 1));
        results[0].Expense!.CategoryId.Should().Be(1);

        results[1].Expense!.Amount.Should().Be(470m);
        results[2].Expense!.Amount.Should().Be(8500m);
    }

    [Fact]
    public void GenericCsvImport_WithMixedValidAndErrorRows_SkipsErrorRowsAndContinues()
    {
        // Arrange
        var parsers = new ICsvParser[] { new GenericCsvParser() };
        var factory = new CsvParserFactory(parsers);

        var csvContent =
            "date,description,amount,categoryId\r\n" +
            "2026-03-01,正常行1,1000,1\r\n" +
            "2026-03-02,金額エラー行,abc,1\r\n" +
            "invalid-date,日付エラー行,500,1\r\n" +
            "2026-03-04,正常行2,2000,2";
        var stream = CreateUtf8Stream(csvContent);

        // Act
        var parser = factory.SelectParser(stream);
        var results = parser.Parse(stream).ToList();

        // Assert: エラー行はスキップされ、成功行と失敗行が混在する結果が返ること
        parser.Should().BeOfType<GenericCsvParser>();
        results.Should().HaveCount(4);

        results[0].IsSuccess.Should().BeTrue();
        results[0].Expense!.Description.Should().Be("正常行1");

        results[1].IsSuccess.Should().BeFalse();
        results[1].ErrorMessage.Should().NotBeNullOrEmpty();

        results[2].IsSuccess.Should().BeFalse();
        results[2].ErrorMessage.Should().NotBeNullOrEmpty();

        results[3].IsSuccess.Should().BeTrue();
        results[3].Expense!.Description.Should().Be("正常行2");
    }

    [Fact]
    public void GenericCsvImport_WithShiftJisEncoding_ParsesJapaneseText()
    {
        // Arrange: Shift_JISエンコードの汎用フォーマットCSV
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var shiftJisEncoding = Encoding.GetEncoding("shift_jis");

        var csvContent = "date,description,amount,categoryId\r\n2026-03-08,食料品購入,4500,1";
        var stream = new MemoryStream(shiftJisEncoding.GetBytes(csvContent));

        var parser = new GenericCsvParser();

        // Act
        var results = parser.Parse(stream, "shift_jis").ToList();

        // Assert
        results.Should().HaveCount(1);
        results[0].IsSuccess.Should().BeTrue();
        results[0].Expense!.Description.Should().Be("食料品購入");
        results[0].Expense!.Amount.Should().Be(4500m);
    }

    // =====================================================================
    // 三菱UFJ銀行フォーマットのE2Eテスト
    // =====================================================================

    [Fact]
    public void MufgCsvImport_WithValidCsv_ParsesExpensesCorrectly()
    {
        // Arrange: ファクトリーに全パーサーを登録
        var parsers = new ICsvParser[]
        {
            new MufgCsvParser(),
            new RakutenCsvParser(),
            new GenericCsvParser()
        };
        var factory = new CsvParserFactory(parsers);

        var csvContent =
            "日付,摘要,お支払い金額,お預かり金額,残高,メモ\r\n" +
            "2026/03/01,コンビニ 渋谷店,1200,,100000,\r\n" +
            "2026/03/15,給与,,200000,298800,\r\n" +
            "2026/03/20,電気代,5400,,293400,";
        var stream = CreateUtf8Stream(csvContent);

        // Act: ファクトリーがMUFGパーサーを選択することを検証
        var parser = factory.SelectParser(stream);
        var results = parser.Parse(stream).ToList();

        // Assert: MUFGパーサーが選択され、支払い行のみが取り込まれること
        parser.Should().BeOfType<MufgCsvParser>();
        parser.FormatName.Should().Be("mufg");

        var successResults = results.Where(r => r.IsSuccess).ToList();
        successResults.Should().HaveCount(2);

        successResults[0].Expense!.Amount.Should().Be(1200m);
        successResults[0].Expense!.Description.Should().Be("コンビニ 渋谷店");
        successResults[0].Expense!.Date.Should().Be(new DateOnly(2026, 3, 1));
        successResults[0].Expense!.ImportSource.Should().Be("mufg");

        successResults[1].Expense!.Amount.Should().Be(5400m);
        successResults[1].Expense!.Description.Should().Be("電気代");
        successResults[1].Expense!.Date.Should().Be(new DateOnly(2026, 3, 20));
    }

    [Fact]
    public void MufgCsvImport_AmountAndDateConversion_IsCorrect()
    {
        // Arrange: カンマ区切り金額と日付変換の検証
        var parser = new MufgCsvParser();
        var csvContent =
            "日付,摘要,お支払い金額,お預かり金額,残高,メモ\r\n" +
            "2026/01/31,家賃,\"150,000\",,50000,\r\n" +
            "2026/12/31,年末支出,50000,,0,";
        var stream = CreateUtf8Stream(csvContent);

        // Act
        var results = parser.Parse(stream).ToList();

        // Assert: カンマ区切り金額が正しく変換されること
        results.Should().HaveCount(2);
        results[0].IsSuccess.Should().BeTrue();
        results[0].Expense!.Amount.Should().Be(150000m);
        results[0].Expense!.Date.Should().Be(new DateOnly(2026, 1, 31));

        results[1].IsSuccess.Should().BeTrue();
        results[1].Expense!.Amount.Should().Be(50000m);
        results[1].Expense!.Date.Should().Be(new DateOnly(2026, 12, 31));
    }

    [Fact]
    public void MufgCsvImport_WithAlternativeHeader_ParsesCorrectly()
    {
        // Arrange: 別バリエーションのMUFGヘッダー（「支払金額」）
        var parsers = new ICsvParser[]
        {
            new MufgCsvParser(),
            new RakutenCsvParser(),
            new GenericCsvParser()
        };
        var factory = new CsvParserFactory(parsers);

        var csvContent =
            "日付,摘要,支払金額,入金額,残高\r\n" +
            "2026/03/10,ガス代,3800,,95000";
        var stream = CreateUtf8Stream(csvContent);

        // Act
        var parser = factory.SelectParser(stream);
        var results = parser.Parse(stream).ToList();

        // Assert: 別バリエーションのヘッダーでもMUFGパーサーが選択されること
        parser.Should().BeOfType<MufgCsvParser>();
        results.Should().HaveCount(1);
        results[0].IsSuccess.Should().BeTrue();
        results[0].Expense!.Amount.Should().Be(3800m);
    }

    // =====================================================================
    // 楽天カードフォーマットのE2Eテスト
    // =====================================================================

    [Fact]
    public void RakutenCsvImport_WithValidCsv_ParsesExpensesCorrectly()
    {
        // Arrange: ファクトリーに全パーサーを登録
        var parsers = new ICsvParser[]
        {
            new MufgCsvParser(),
            new RakutenCsvParser(),
            new GenericCsvParser()
        };
        var factory = new CsvParserFactory(parsers);

        var csvContent =
            "利用日,利用店名・商品名,利用者,支払方法,利用金額,支払金額\r\n" +
            "2026/03/01,Amazon Japan,本人,1回払い,3980,3980\r\n" +
            "2026/03/05,Spotify Japan,本人,1回払い,980,980\r\n" +
            "2026/03/10,楽天市場,本人,分割払い,12000,2000";
        var stream = CreateUtf8Stream(csvContent);

        // Act: ファクトリーが楽天パーサーを選択することを検証
        var parser = factory.SelectParser(stream);
        var results = parser.Parse(stream).ToList();

        // Assert: 楽天パーサーが選択され、全行が正常にパースされること
        parser.Should().BeOfType<RakutenCsvParser>();
        parser.FormatName.Should().Be("rakuten");

        var successResults = results.Where(r => r.IsSuccess).ToList();
        successResults.Should().HaveCount(3);

        successResults[0].Expense!.Amount.Should().Be(3980m);
        successResults[0].Expense!.Description.Should().Be("Amazon Japan");
        successResults[0].Expense!.Date.Should().Be(new DateOnly(2026, 3, 1));
        successResults[0].Expense!.ImportSource.Should().Be("rakuten");

        successResults[1].Expense!.Amount.Should().Be(980m);
        successResults[2].Expense!.Amount.Should().Be(12000m);
    }

    [Fact]
    public void RakutenCsvImport_AmountAndDateConversion_IsCorrect()
    {
        // Arrange: カンマ区切り金額と日付変換の検証
        var parser = new RakutenCsvParser();
        var csvContent =
            "利用日,利用店名・商品名,利用者,支払方法,利用金額,支払金額\r\n" +
            "2026/01/01,旅行代金,本人,1回払い,\"120,000\",\"120,000\"\r\n" +
            "2026/12/31,大晦日の買い物,本人,1回払い,999,999";
        var stream = CreateUtf8Stream(csvContent);

        // Act
        var results = parser.Parse(stream).ToList();

        // Assert: カンマ区切り金額が正しく変換されること
        results.Should().HaveCount(2);
        results[0].IsSuccess.Should().BeTrue();
        results[0].Expense!.Amount.Should().Be(120000m);
        results[0].Expense!.Date.Should().Be(new DateOnly(2026, 1, 1));

        results[1].IsSuccess.Should().BeTrue();
        results[1].Expense!.Amount.Should().Be(999m);
        results[1].Expense!.Date.Should().Be(new DateOnly(2026, 12, 31));
    }

    [Fact]
    public void RakutenCsvImport_WithSimplifiedHeader_ParsesCorrectly()
    {
        // Arrange: 簡略版楽天ヘッダー
        var parsers = new ICsvParser[]
        {
            new MufgCsvParser(),
            new RakutenCsvParser(),
            new GenericCsvParser()
        };
        var factory = new CsvParserFactory(parsers);

        var csvContent =
            "利用日,利用店名,利用金額\r\n" +
            "2026/03/08,コンビニ,500";
        var stream = CreateUtf8Stream(csvContent);

        // Act
        var parser = factory.SelectParser(stream);
        var results = parser.Parse(stream).ToList();

        // Assert: 簡略版ヘッダーでも楽天パーサーが選択されること
        parser.Should().BeOfType<RakutenCsvParser>();
        results.Should().HaveCount(1);
        results[0].IsSuccess.Should().BeTrue();
        results[0].Expense!.Amount.Should().Be(500m);
    }

    // =====================================================================
    // CsvParserFactory ヘッダー判定テスト
    // =====================================================================

    [Fact]
    public void CsvParserFactory_WithGenericHeader_SelectsGenericParser()
    {
        // Arrange
        var parsers = new ICsvParser[]
        {
            new MufgCsvParser(),
            new RakutenCsvParser(),
            new GenericCsvParser()
        };
        var factory = new CsvParserFactory(parsers);

        var csvContent = "date,description,amount,categoryId\r\n2026-03-08,test,100,1";
        var stream = CreateUtf8Stream(csvContent);

        // Act
        var parser = factory.SelectParser(stream);

        // Assert: 汎用フォーマットのヘッダーで汎用パーサーが選択されること
        parser.Should().BeOfType<GenericCsvParser>();
        parser.FormatName.Should().Be("generic");
    }

    [Fact]
    public void CsvParserFactory_WithMufgHeader_SelectsMufgParser()
    {
        // Arrange
        var parsers = new ICsvParser[]
        {
            new MufgCsvParser(),
            new RakutenCsvParser(),
            new GenericCsvParser()
        };
        var factory = new CsvParserFactory(parsers);

        var csvContent = "日付,摘要,お支払い金額,お預かり金額,残高,メモ\r\n2026/03/01,test,1000,,99000,";
        var stream = CreateUtf8Stream(csvContent);

        // Act
        var parser = factory.SelectParser(stream);

        // Assert: MUFGヘッダーでMUFGパーサーが選択されること
        parser.Should().BeOfType<MufgCsvParser>();
        parser.FormatName.Should().Be("mufg");
    }

    [Fact]
    public void CsvParserFactory_WithRakutenHeader_SelectsRakutenParser()
    {
        // Arrange
        var parsers = new ICsvParser[]
        {
            new MufgCsvParser(),
            new RakutenCsvParser(),
            new GenericCsvParser()
        };
        var factory = new CsvParserFactory(parsers);

        var csvContent = "利用日,利用店名・商品名,利用者,支払方法,利用金額,支払金額\r\n2026/03/01,Amazon,本人,1回払い,1000,1000";
        var stream = CreateUtf8Stream(csvContent);

        // Act
        var parser = factory.SelectParser(stream);

        // Assert: 楽天ヘッダーで楽天パーサーが選択されること
        parser.Should().BeOfType<RakutenCsvParser>();
        parser.FormatName.Should().Be("rakuten");
    }

    [Fact]
    public void CsvParserFactory_WithUnknownHeader_FallsBackToGenericParser()
    {
        // Arrange: 未知のフォーマット（登録済みパーサーにマッチしない）
        var parsers = new ICsvParser[]
        {
            new MufgCsvParser(),
            new RakutenCsvParser()
            // GenericCsvParserは登録しない（ファクトリー内部のフォールバックが使用されること）
        };
        var factory = new CsvParserFactory(parsers);

        var csvContent = "unknown_col1,unknown_col2,unknown_col3\r\nvalue1,value2,value3";
        var stream = CreateUtf8Stream(csvContent);

        // Act
        var parser = factory.SelectParser(stream);

        // Assert: 未知のヘッダーで汎用パーサーにフォールバックすること
        parser.Should().BeOfType<GenericCsvParser>();
    }

    [Fact]
    public void CsvParserFactory_ParserSelection_RewindsStreamForSubsequentParse()
    {
        // Arrange: SelectParserでストリームを読んだ後、Parseが正常に動作することを確認
        var parsers = new ICsvParser[]
        {
            new MufgCsvParser(),
            new RakutenCsvParser(),
            new GenericCsvParser()
        };
        var factory = new CsvParserFactory(parsers);

        var csvContent = "date,description,amount,categoryId\r\n2026-03-08,テスト支出,1500,1";
        var stream = CreateUtf8Stream(csvContent);

        // Act: SelectParserでストリームを先頭に戻した後、Parseを呼び出す
        var parser = factory.SelectParser(stream);
        var results = parser.Parse(stream).ToList();

        // Assert: SelectParser後もストリームが正常に読み取れること
        results.Should().HaveCount(1);
        results[0].IsSuccess.Should().BeTrue();
        results[0].Expense!.Amount.Should().Be(1500m);
    }

    // =====================================================================
    // ヘルパー
    // =====================================================================

    private static MemoryStream CreateUtf8Stream(string content) =>
        new(Encoding.UTF8.GetBytes(content));
}
