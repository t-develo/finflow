using System.Globalization;
using System.Text;
using CsvHelper;
using CsvHelper.Configuration;
using FinFlow.Domain.Entities;
using FinFlow.Domain.Interfaces;

namespace FinFlow.Infrastructure.Services.CsvParsing;

/// <summary>
/// 楽天カードフォーマットのCSVパーサー
/// ヘッダー例: 利用日,利用店名・商品名,利用者,支払方法,利用金額,支払金額
/// </summary>
public class RakutenCsvParser : ICsvParser
{
    private const int MaxCsvRows = 10_000;

    public string FormatName => "rakuten";

    /// <summary>
    /// 楽天カード固有のヘッダーキーワードで判定する
    /// </summary>
    public bool CanParse(string headerLine)
    {
        if (string.IsNullOrWhiteSpace(headerLine))
            return false;

        // 楽天カード特有のヘッダーキーワード
        return headerLine.Contains("利用日") &&
               (headerLine.Contains("利用店名") || headerLine.Contains("利用店名・商品名")) &&
               headerLine.Contains("利用金額");
    }

    /// <summary>
    /// 楽天カードフォーマットのCSVをパースして支出リストを返す。
    /// エラー行はスキップし、IsSuccess=falseの結果として記録する。
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
            results.Add(parseResult);
        }

        return results;
    }

    private CsvParseResult TryParseRow(CsvReader csv, int rowNumber)
    {
        try
        {
            var rawDate = csv.GetField("利用日") ?? string.Empty;

            // 楽天カードは「利用店名・商品名」または「利用店名」
            var rawDescription = csv.GetField<string?>("利用店名・商品名")
                ?? csv.GetField<string?>("利用店名")
                ?? string.Empty;

            // 楽天カードは「利用金額」（実際の利用額）を使用
            var rawAmount = csv.GetField<string?>("利用金額") ?? string.Empty;

            // 日付の解析（楽天カード形式: yyyy/MM/dd）
            if (!TryParseRakutenDate(rawDate.Trim(), out var date))
                return CreateErrorResult(rowNumber, $"日付の形式が不正です: '{rawDate}'");

            // カンマ区切りの数値（例: "1,234"）を解析
            var cleanedAmount = rawAmount.Replace(",", "").Trim();
            if (!decimal.TryParse(cleanedAmount, NumberStyles.Any, CultureInfo.InvariantCulture, out var amount))
                return CreateErrorResult(rowNumber, $"利用金額の形式が不正です: '{rawAmount}'");

            if (amount <= 0)
                return CreateErrorResult(rowNumber, $"利用金額は0より大きい値である必要があります: '{rawAmount}'");

            var description = rawDescription.Trim();
            if (string.IsNullOrEmpty(description))
                description = "楽天カード利用";

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

    private static bool TryParseRakutenDate(string rawDate, out DateOnly date)
    {
        // 楽天カード日付フォーマットのバリエーション
        var formats = new[] { "yyyy/MM/dd", "yyyy-MM-dd", "yyyy/M/d" };
        foreach (var format in formats)
        {
            if (DateOnly.TryParseExact(rawDate, format, CultureInfo.InvariantCulture, DateTimeStyles.None, out date))
                return true;
        }

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
