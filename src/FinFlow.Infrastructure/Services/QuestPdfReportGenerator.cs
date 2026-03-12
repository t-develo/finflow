using FinFlow.Domain.Interfaces;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace FinFlow.Infrastructure.Services;

/// <summary>
/// QuestPDFを使用してPDF月次レポートを生成する実装。
/// Domain層のIPdfReportGeneratorインターフェースを実装し、
/// QuestPDFの詳細をInfrastructure層に隠蔽する（Clean Architectureの依存性ルール）。
/// </summary>
public class QuestPdfReportGenerator : IPdfReportGenerator
{
    public QuestPdfReportGenerator()
    {
        // QuestPDFコミュニティライセンス設定（非商用・OSS用途）
        QuestPDF.Settings.License = LicenseType.Community;
    }

    /// <summary>
    /// 月次レポートデータをPDFバイト配列として生成する。
    /// </summary>
    public Task<byte[]> GenerateMonthlyReportAsync(MonthlyReportDto report)
    {
        var pdfBytes = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(2, Unit.Centimetre);
                page.DefaultTextStyle(x => x.FontSize(10));

                page.Header().Element(ComposeHeader(report));
                page.Content().Element(ComposeContent(report));
                page.Footer().Element(ComposeFooter());
            });
        }).GeneratePdf();

        return Task.FromResult(pdfBytes);
    }

    private static Action<IContainer> ComposeHeader(MonthlyReportDto report)
    {
        return container =>
        {
            container.Row(row =>
            {
                row.RelativeItem().Column(col =>
                {
                    col.Item().Text($"月次支出レポート").FontSize(20).Bold().FontColor(Colors.Blue.Medium);
                    col.Item().Text($"{report.Year}年{report.Month}月").FontSize(14).FontColor(Colors.Grey.Medium);
                });

                row.ConstantItem(150).Column(col =>
                {
                    col.Item().AlignRight().Text("FinFlow").FontSize(12).Bold();
                    col.Item().AlignRight().Text(DateTime.Now.ToString("yyyy/MM/dd")).FontSize(10).FontColor(Colors.Grey.Medium);
                });
            });

            container.PaddingTop(5).LineHorizontal(1).LineColor(Colors.Blue.Medium);
        };
    }

    private static Action<IContainer> ComposeContent(MonthlyReportDto report)
    {
        return container =>
        {
            container.PaddingTop(20).Column(col =>
            {
                // サマリーセクション
                col.Item().Element(ComposeSummary(report));

                col.Item().PaddingTop(20);

                // カテゴリ別内訳セクション
                col.Item().Element(ComposeCategoryBreakdown(report));
            });
        };
    }

    private static Action<IContainer> ComposeSummary(MonthlyReportDto report)
    {
        return container =>
        {
            container.Column(col =>
            {
                col.Item().Text("サマリー").FontSize(14).Bold().FontColor(Colors.Blue.Darken2);
                col.Item().PaddingTop(8).Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn(2);
                        columns.RelativeColumn(3);
                    });

                    table.Cell().Padding(6).Background(Colors.Grey.Lighten3).Text("合計支出金額").Bold();
                    table.Cell().Padding(6).AlignRight().Text($"¥{report.TotalAmount:N0}").Bold().FontColor(Colors.Red.Medium);

                    table.Cell().Padding(6).Text("支出件数");
                    table.Cell().Padding(6).AlignRight().Text($"{report.ExpenseCount}件");
                });
            });
        };
    }

    private static Action<IContainer> ComposeCategoryBreakdown(MonthlyReportDto report)
    {
        return container =>
        {
            container.Column(col =>
            {
                col.Item().Text("カテゴリ別内訳").FontSize(14).Bold().FontColor(Colors.Blue.Darken2);
                col.Item().PaddingTop(8);

                if (!report.CategoryBreakdown.Any())
                {
                    col.Item().Text("データがありません").FontColor(Colors.Grey.Medium);
                    return;
                }

                col.Item().Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn(3); // カテゴリ名
                        columns.RelativeColumn(2); // 金額
                        columns.RelativeColumn(1); // 件数
                        columns.RelativeColumn(1); // 割合
                    });

                    // ヘッダー行
                    table.Cell().Background(Colors.Blue.Medium).Padding(6).Text("カテゴリ").Bold().FontColor(Colors.White);
                    table.Cell().Background(Colors.Blue.Medium).Padding(6).AlignRight().Text("金額").Bold().FontColor(Colors.White);
                    table.Cell().Background(Colors.Blue.Medium).Padding(6).AlignRight().Text("件数").Bold().FontColor(Colors.White);
                    table.Cell().Background(Colors.Blue.Medium).Padding(6).AlignRight().Text("割合").Bold().FontColor(Colors.White);

                    // データ行
                    var isAlternate = false;
                    foreach (var category in report.CategoryBreakdown)
                    {
                        var background = isAlternate ? Colors.Grey.Lighten4 : Colors.White;

                        table.Cell().Background(background).Padding(6).Text(category.CategoryName);
                        table.Cell().Background(background).Padding(6).AlignRight().Text($"¥{category.TotalAmount:N0}");
                        table.Cell().Background(background).Padding(6).AlignRight().Text($"{category.Count}件");
                        table.Cell().Background(background).Padding(6).AlignRight().Text($"{category.Percentage}%");

                        isAlternate = !isAlternate;
                    }

                    // 合計行
                    table.Cell().Background(Colors.Grey.Lighten2).Padding(6).Text("合計").Bold();
                    table.Cell().Background(Colors.Grey.Lighten2).Padding(6).AlignRight().Text($"¥{report.TotalAmount:N0}").Bold();
                    table.Cell().Background(Colors.Grey.Lighten2).Padding(6).AlignRight().Text($"{report.ExpenseCount}件").Bold();
                    table.Cell().Background(Colors.Grey.Lighten2).Padding(6).AlignRight().Text("100%").Bold();
                });
            });
        };
    }

    private static Action<IContainer> ComposeFooter()
    {
        return container =>
        {
            container.PaddingTop(5).BorderTop(1).BorderColor(Colors.Grey.Medium)
                .Row(row =>
                {
                    row.RelativeItem().Text("FinFlow - 家計管理アプリ").FontSize(8).FontColor(Colors.Grey.Medium);
                    row.ConstantItem(50).AlignRight().Text(text =>
                    {
                        text.Span("ページ ").FontSize(8).FontColor(Colors.Grey.Medium);
                        text.CurrentPageNumber().FontSize(8).FontColor(Colors.Grey.Medium);
                        text.Span(" / ").FontSize(8).FontColor(Colors.Grey.Medium);
                        text.TotalPages().FontSize(8).FontColor(Colors.Grey.Medium);
                    });
                });
        };
    }
}
