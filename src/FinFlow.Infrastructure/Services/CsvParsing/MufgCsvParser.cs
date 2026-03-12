using System.Globalization;
using System.Text;
using CsvHelper;
using CsvHelper.Configuration;
using FinFlow.Domain.Entities;
using FinFlow.Domain.Interfaces;

namespace FinFlow.Infrastructure.Services.CsvParsing;

/// <summary>
/// 三菱UFJ銀行（MUFG）フォーマットのCSVパーサー
/// ヘッダー例: 日付,摘要,お支払い金額,お預かり金額,残高,メモ
/// 支払い金額（debit）のみを支出として取り込む
/// </summary>
public class MufgCsvParser : ICsvParser
{
    private const int MaxCsvRows = 10_000;

    public string FormatName => "mufg";

    /// <summary>
    /// MUFG固有のヘッダーキーワードで判定する
    /// </summary>
    public bool CanParse(string headerLine)
    {
        if (string.IsNullOrWhiteSpace(headerLine))
            return false;

        // MUFG特有のヘッダーキーワード
        return headerLine.Contains("摘要") &&
               (headerLine.Contains("お支払い金額") || headerLine.Contains("支払金額"));
    }

    /// <summary>
    /// MUFGフォーマットのCSVをパースして支出リストを返す。
    /// お支払い金額（debit）のみを取り込む。残高変動のない行はスキップする。
    /// </summary>
    public IEnumerable<CsvParseResult> Parse(Stream csvStream, string encoding = "utf-8")
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var encodingInstance = ResolveEncoding(encoding);
        var results = new List<CsvParseResult>();

        using var reader = new StreamReader(csvStream, encodingInstance, leaveOpen: true);
        var csvConfig = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = true,
            MissingFieldFound = null,
            BadDataFound = null,
            TrimOptions = TrimOptions.Trim
        };

        using var csv = new CsvReader(reader, csvConfig);

        try
        {
            csv.Read();
            csv.ReadHeader();
        }
        catch (Exception ex)
        {
            results.Add(CreateErrorResult(1, $"CSVヘッダーの読み込みに失敗しました: {ex.Message}"));
            return results;
        }

        var rowNumber = 1;
        while (csv.Read())
        {
            rowNumber++;

            if (rowNumber - 1 > MaxCsvRows)
            {
                results.Add(CreateErrorResult(rowNumber, $"最大行数（{MaxCsvRows}行）を超えました。"));
                break;
            }

            var parseResult = TryParseRow(csv, rowNumber);
            // 収入行（支払い金額が空）はスキップ（エラーではない）
            if (parseResult != null)
                results.Add(parseResult);
        }

        return results;
    }

    private CsvParseResult? TryParseRow(CsvReader csv, int rowNumber)
    {
        try
        {
            var rawDate = csv.GetField("日付") ?? string.Empty;
            var rawDescription = csv.GetField("摘要") ?? string.Empty;

            // MUFGは複数の列名バリエーションを持つ
            var rawDebitAmount = csv.GetField<string?>("お支払い金額")
                ?? csv.GetField<string?>("支払金額")
                ?? string.Empty;

            // 支払い金額が空・0の行は収入行としてスキップ（nullを返すことでフィルタリング）
            if (string.IsNullOrWhiteSpace(rawDebitAmount))
                return null;

            // カンマ区切りの数値（例: "1,234"）を解析
            var cleanedAmount = rawDebitAmount.Replace(",", "").Trim();
            if (!decimal.TryParse(cleanedAmount, NumberStyles.Any, CultureInfo.InvariantCulture, out var amount))
                return CreateErrorResult(rowNumber, $"支払い金額の形式が不正です: '{rawDebitAmount}'");

            if (amount <= 0)
                return null; // 0円は収入/調整行のためスキップ

            // 日付の解析（MUFG形式: yyyy/MM/dd または yyyy-MM-dd）
            if (!TryParseMufgDate(rawDate.Trim(), out var date))
                return CreateErrorResult(rowNumber, $"日付の形式が不正です: '{rawDate}'");

            var description = rawDescription.Trim();
            if (string.IsNullOrEmpty(description))
                description = "MUFG取引";

            var expense = new Expense
            {
                Amount = amount,
                CategoryId = null, // 自動分類は上位層で実施
                Date = date,
                Description = description,
                ImportSource = FormatName
            };

            return new CsvParseResult
            {
                IsSuccess = true,
                RowNumber = rowNumber,
                Expense = expense
            };
        }
        catch (Exception ex)
        {
            return CreateErrorResult(rowNumber, $"行の解析中に予期しないエラーが発生しました: {ex.Message}");
        }
    }

    private static bool TryParseMufgDate(string rawDate, out DateOnly date)
    {
        // MUFG日付フォーマットのバリエーション
        var formats = new[] { "yyyy/MM/dd", "yyyy-MM-dd", "yyyy/M/d", "M/d/yyyy" };
        foreach (var format in formats)
        {
            if (DateOnly.TryParseExact(rawDate, format, CultureInfo.InvariantCulture, DateTimeStyles.None, out date))
                return true;
        }

        // フォールバック: DateOnly.TryParse
        return DateOnly.TryParse(rawDate, out date);
    }

    private static CsvParseResult CreateErrorResult(int rowNumber, string errorMessage) =>
        new()
        {
            IsSuccess = false,
            RowNumber = rowNumber,
            ErrorMessage = errorMessage
        };

    private static Encoding ResolveEncoding(string encodingName)
    {
        return encodingName.ToLowerInvariant() switch
        {
            "shift_jis" or "shift-jis" or "sjis" or "cp932" => Encoding.GetEncoding("shift_jis"),
            "utf-8" or "utf8" => Encoding.UTF8,
            _ => Encoding.UTF8
        };
    }
}
