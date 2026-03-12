namespace FinFlow.Domain.Interfaces;

/// <summary>
/// PDF月次レポート生成のインターフェース。
/// Domain層はQuestPDF等の外部ライブラリを直接参照せず、このインターフェース経由で依存する。
/// </summary>
public interface IPdfReportGenerator
{
    /// <summary>
    /// 月次レポートデータをPDFバイト配列として生成する。
    /// </summary>
    Task<byte[]> GenerateMonthlyReportAsync(MonthlyReportDto report);
}
