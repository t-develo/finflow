using FinFlow.Domain.Entities;

namespace FinFlow.Domain.Interfaces;

public interface ICsvParser
{
    string FormatName { get; }
    bool CanParse(string headerLine);
    IEnumerable<CsvParseResult> Parse(Stream csvStream, string encoding = "utf-8");
}

public class CsvParseResult
{
    public bool IsSuccess { get; set; }
    public int RowNumber { get; set; }
    public Expense? Expense { get; set; }
    public string? ErrorMessage { get; set; }
}
