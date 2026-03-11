using System.Globalization;
using System.Text;
using CsvHelper;
using CsvHelper.Configuration;
using FinFlow.Domain.Entities;
using FinFlow.Domain.Interfaces;

namespace FinFlow.Infrastructure.Services.CsvParsing;

/// <summary>
/// 汎用CSVフォーマットのパーサー（日付, 説明, 金額, カテゴリID）
/// ヘッダー: date,description,amount,categoryId
/// </summary>
public class GenericCsvParser : ICsvParser
{
    private const int MaxCsvRows = 10_000;

    /// <summary>
    /// このパーサーが対応するフォーマット名（Factory選択・ログ用）
    /// </summary>
    public string FormatName => "generic";

    /// <summary>
    /// ヘッダー行からこのパーサーが適切かを判定する
    /// </summary>
    public bool CanParse(string headerLine)
    {
        if (string.IsNullOrWhiteSpace(headerLine))
            return false;

        var normalizedHeader = headerLine.ToLowerInvariant().Replace(" ", "");
        return normalizedHeader.Contains("date") &&
               normalizedHeader.Contains("description") &&
               normalizedHeader.Contains("amount");
    }

    /// <summary>
    /// CSVストリームをパースして支出リストを返す。
    /// エラー行はスキップし、IsSuccess=falseの結果として記録する。
    /// </summary>
    public IEnumerable<CsvParseResult> Parse(Stream csvStream, string encoding = "utf-8")
    {
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
            results.Add(CreateErrorResult(1, $"Failed to read CSV header: {ex.Message}"));
            return results;
        }

        var rowNumber = 1;
        while (csv.Read())
        {
            rowNumber++;

            if (rowNumber - 1 > MaxCsvRows)
            {
                results.Add(CreateErrorResult(rowNumber, $"Exceeded maximum row limit of {MaxCsvRows} rows."));
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
            var rawDate = csv.GetField("date") ?? string.Empty;
            var rawDescription = csv.GetField("description") ?? string.Empty;
            var rawAmount = csv.GetField("amount") ?? string.Empty;
            var rawCategoryId = csv.GetField<string?>("categoryId");

            if (!DateOnly.TryParse(rawDate.Trim(), out var date))
                return CreateErrorResult(rowNumber, $"Invalid date format: '{rawDate}'");

            if (!decimal.TryParse(rawAmount.Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out var amount))
                return CreateErrorResult(rowNumber, $"Invalid amount value: '{rawAmount}'");

            if (amount <= 0)
                return CreateErrorResult(rowNumber, $"Amount must be positive: '{rawAmount}'");

            var description = rawDescription.Trim();
            if (string.IsNullOrEmpty(description))
                return CreateErrorResult(rowNumber, "Description is required.");

            int? categoryId = null;
            if (!string.IsNullOrWhiteSpace(rawCategoryId) &&
                int.TryParse(rawCategoryId.Trim(), out var parsedCategoryId))
            {
                categoryId = parsedCategoryId;
            }

            var expense = new Expense
            {
                Amount = amount,
                CategoryId = categoryId,
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
            return CreateErrorResult(rowNumber, $"Unexpected error parsing row: {ex.Message}");
        }
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
        // Shift_JISはCP932として登録されているため、EncodingProviderを使用
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        return encodingName.ToLowerInvariant() switch
        {
            "shift_jis" or "shift-jis" or "sjis" or "cp932" => Encoding.GetEncoding("shift_jis"),
            "utf-8" or "utf8" => Encoding.UTF8,
            _ => Encoding.UTF8
        };
    }
}
