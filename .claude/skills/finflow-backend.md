# FinFlow バックエンド固有ガイド【FinFlow固有】

SE-1・SE-2が意識すべきFinFlow固有の実装ルール。
C#の汎用的なコーディング規約は `/backend-csharp` を参照。
TDDの基本は `/tdd` を参照。

---

## SE-1（支出管理・CSV取込・自動分類）固有事項

### CSVパーサーの設計

```csharp
// インターフェース（Domain層）
public interface ICsvParser
{
    IEnumerable<ParsedExpenseRow> Parse(Stream csvStream, Encoding encoding);
}

// 実装クラス（Infrastructure層）
// GenericCsvParser: date, description, amount, category の汎用形式
// MufgCsvParser:    MUFG銀行のCSVフォーマット（Sprint 2）
// RakutenCsvParser: 楽天カードのCSVフォーマット（Sprint 2）

// Factoryでヘッダー行から自動選択
public class CsvParserFactory
{
    public ICsvParser CreateParser(string headerLine)
    {
        if (headerLine.Contains("取引日") && headerLine.Contains("摘要"))
            return new MufgCsvParser();
        if (headerLine.Contains("利用日") && headerLine.Contains("利用店名・商品名"))
            return new RakutenCsvParser();
        return new GenericCsvParser();
    }
}
```

### CSV取込の制約・注意点

- **最大行数:** 10,000行（超過はエラー）
- **エンコーディング:** UTF-8とShift_JIS両方に対応
- **エラー行:** スキップ（致命的エラーにしない）、スキップした行番号を結果に含める
- **CSVインジェクション:** セル値のサニタイズ必須（`/finflow-security` 参照）

```csharp
// エラー行スキップの実装パターン
var importedRows = new List<Expense>();
var skippedRows = new List<int>();

foreach (var (row, lineNumber) in rows.WithIndex())
{
    try { importedRows.Add(ParseRow(row)); }
    catch (CsvParseException ex)
    {
        _logger.LogWarning("Skipping row {Line}: {Reason}", lineNumber, ex.Message);
        skippedRows.Add(lineNumber);
    }
}
return new ImportResult { Imported = importedRows, Skipped = skippedRows };
```

### SE-2への影響を意識する

SE-2はExpense・Categoryテーブルを集計目的で読み取り専用で使用する。

- **スキーマ変更（カラム追加・削除・リネーム）は必ずPLに相談する**
- SE-2が依存するカラム（Amount, Date, CategoryId, UserId等）を変更する前にPL経由でSE-2に確認する
- マイグレーション追加のタイミングもPLを通して調整する

---

## SE-2（集計・サブスク・通知・PDF）固有事項

### 集計コードの品質基準

```csharp
// decimal型の徹底（金額に関わる全箇所）
decimal total = expenses.Sum(e => e.Amount);
decimal percentage = totalAmount > 0
    ? Math.Round(categoryAmount / totalAmount * 100, 1, MidpointRounding.AwayFromZero)
    : 0m;
decimal dailyAverage = Math.Round(total / DateTime.DaysInMonth(year, month), 1, MidpointRounding.AwayFromZero);

// ゼロ除算防止
decimal average = count > 0 ? total / count : 0m;
```

### Dashboardは既存サービスを合成する

```csharp
// 新しい集計クエリを直接書くのではなく、既存サービスを組み合わせる
public class DashboardService : IDashboardService
{
    private readonly IReportService _reportService;
    private readonly ISubscriptionService _subscriptionService;

    public async Task<DashboardSummaryDto> GetSummaryAsync(string userId, int year, int month)
    {
        var monthly = await _reportService.GetMonthlySummaryAsync(userId, year, month);
        var upcoming = await _subscriptionService.GetUpcomingPaymentsAsync(userId, days: 3);
        return new DashboardSummaryDto
        {
            MonthlyTotal = monthly.TotalAmount,
            UpcomingPayments = upcoming
        };
    }
}
```

### SE-1データの利用ルール

- Expense・Categoryは**読み取り専用**。EF CoreのAsNoTrackingを使用する
- クエリには必ず `UserId` フィルタを含める
- スキーマ変更が必要な場合 → PLを通してSE-1に依頼（SE-1に直接連絡しない）

### NotificationScheduler（Sprint 2）

```csharp
// IHostedServiceとして実装
public class NotificationScheduler : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await CheckAndSendNotificationsAsync();
            await Task.Delay(TimeSpan.FromHours(1), stoppingToken); // 1時間ごと
        }
    }
    // 3日以内に支払日が来るサブスクを取得してメール送信
}
```

---

## FinFlow固有のテスト基準

| 担当 | テスト対象 | 最低必要テスト |
|------|-----------|--------------|
| **SE-1** | ExpenseService | 正常系2 + 異常系1 + エッジ1 |
| **SE-1** | CsvParserFactory / GenericCsvParser | 正常3 + エラー行スキップ1 + エンコーディング1 |
| **SE-2** | ReportService月次集計 | 正常3 + 0件エッジ1 + 月末日エッジ1 |
| **SE-2** | SubscriptionService | 正常系2 + 異常系1 |
| **SE-2** | NotificationScheduler（Sprint 2） | 3日以内2 + 期限外1 |

### 集計テストは手計算で期待値を確認する

```csharp
// テストデータを作成し、期待値を必ず手計算してからAssertする
var expenses = new[]
{
    new Expense { Amount = 1000m, Date = new DateTime(2026, 3, 1), UserId = userId },
    new Expense { Amount = 2500m, Date = new DateTime(2026, 3, 15), UserId = userId },
    new Expense { Amount = 500m,  Date = new DateTime(2026, 3, 31), UserId = userId },
};
// 手計算: 合計4000円 / 31日 = 129.0円（小数点第1位四捨五入）

result.TotalAmount.Should().Be(4000m);
result.DailyAverage.Should().Be(129.0m);
```
