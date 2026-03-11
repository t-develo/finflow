using FinFlow.Domain.Entities;
using FinFlow.Infrastructure.Data;
using FinFlow.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace FinFlow.Tests.Reports;

/// <summary>
/// ReportService のユニットテスト。
/// InMemory DBを使い、集計ロジックの正確性を手計算値と照合して検証する。
/// </summary>
[Trait("Category", "ReportService")]
public class ReportServiceTests : IDisposable
{
    private readonly FinFlowDbContext _context;
    private readonly ReportService _service;
    private const string TestUserId = "user-report-test";
    private const string OtherUserId = "user-other";

    public ReportServiceTests()
    {
        var options = new DbContextOptionsBuilder<FinFlowDbContext>()
            .UseInMemoryDatabase($"ReportServiceTest_{Guid.NewGuid()}")
            .Options;
        _context = new FinFlowDbContext(options);
        _context.Database.EnsureCreated();
        _service = new ReportService(_context);
    }

    public void Dispose() => _context.Dispose();

    private async Task SeedExpensesAsync(IEnumerable<Expense> expenses)
    {
        _context.Expenses.AddRange(expenses);
        await _context.SaveChangesAsync();
    }

    private static Category BuildCategory(int id, string name, string color) =>
        new()
        {
            Id = id,
            Name = name,
            Color = color,
            IsSystem = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

    private static Expense BuildExpense(int id, string userId, decimal amount, int year, int month, int day, int? categoryId = null) =>
        new()
        {
            Id = id,
            UserId = userId,
            Amount = amount,
            Date = new DateOnly(year, month, day),
            CategoryId = categoryId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

    // =====================================================================
    // GetMonthlyReportAsync のテスト
    // =====================================================================

    [Fact]
    public async Task GetMonthlyReportAsync_WithExpenses_ReturnsCorrectTotals()
    {
        // Arrange: 2026年3月に食費65000円(20件)、光熱費25000円(3件)を記録
        // 手計算: 合計=90000円, 件数=23件
        var category1 = BuildCategory(1, "食費", "#F59E0B");
        var category6 = BuildCategory(6, "光熱費", "#F97316");
        _context.Categories.AddRange(category1, category6);
        await _context.SaveChangesAsync();

        var expenses = new List<Expense>();
        // 食費: 20件 × 3250円 = 65000円
        for (int i = 0; i < 20; i++)
        {
            expenses.Add(BuildExpense(i + 1, TestUserId, 3250m, 2026, 3, 1 + (i % 28), categoryId: 1));
        }
        // 光熱費: 3件 × 8333.33円 ≈ 25000円（合計をきれいにするため端数調整）
        expenses.Add(BuildExpense(21, TestUserId, 8334m, 2026, 3, 5, categoryId: 6));
        expenses.Add(BuildExpense(22, TestUserId, 8333m, 2026, 3, 10, categoryId: 6));
        expenses.Add(BuildExpense(23, TestUserId, 8333m, 2026, 3, 15, categoryId: 6));
        await SeedExpensesAsync(expenses);

        // Act
        var result = await _service.GetMonthlyReportAsync(TestUserId, 2026, 3);

        // Assert: 金額合計・件数の正確性を確認する
        result.Year.Should().Be(2026);
        result.Month.Should().Be(3);
        result.TotalAmount.Should().Be(90000m);
        result.ExpenseCount.Should().Be(23);
    }

    [Fact]
    public async Task GetMonthlyReportAsync_WithEmptyMonth_ReturnsZeroReport()
    {
        // Arrange: データなし

        // Act: データが存在しない月を集計する
        var result = await _service.GetMonthlyReportAsync(TestUserId, 2026, 1);

        // Assert: 0件・0円・空配列で正常レスポンスが返ること（エラーではない）
        result.TotalAmount.Should().Be(0m);
        result.ExpenseCount.Should().Be(0);
        result.CategoryBreakdown.Should().BeEmpty();
    }

    [Fact]
    public async Task GetMonthlyReportAsync_WithMultipleCategories_ReturnsSortedByAmountDescending()
    {
        // Arrange: カテゴリ別に異なる金額を設定する
        var category1 = BuildCategory(11, "食費", "#F59E0B");
        var category2 = BuildCategory(12, "交通費", "#3B82F6");
        var category3 = BuildCategory(13, "娯楽", "#8B5CF6");
        _context.Categories.AddRange(category1, category2, category3);
        await _context.SaveChangesAsync();

        var expenses = new List<Expense>
        {
            // 娯楽: 5000円（最少）
            BuildExpense(100, TestUserId, 5000m, 2026, 3, 1, categoryId: 13),
            // 食費: 30000円（最大）
            BuildExpense(101, TestUserId, 30000m, 2026, 3, 2, categoryId: 11),
            // 交通費: 10000円（中間）
            BuildExpense(102, TestUserId, 10000m, 2026, 3, 3, categoryId: 12),
        };
        await SeedExpensesAsync(expenses);

        // Act
        var result = await _service.GetMonthlyReportAsync(TestUserId, 2026, 3);

        // Assert: カテゴリ別内訳が金額降順になっていること
        var breakdown = result.CategoryBreakdown.ToList();
        breakdown.Should().HaveCount(3);
        breakdown[0].TotalAmount.Should().Be(30000m); // 食費
        breakdown[1].TotalAmount.Should().Be(10000m); // 交通費
        breakdown[2].TotalAmount.Should().Be(5000m);  // 娯楽
    }

    [Fact]
    public async Task GetMonthlyReportAsync_WithMultipleCategories_CalculatesPercentagesCorrectly()
    {
        // Arrange: 合計100,000円のデータ（パーセンテージ計算の精度を検証する）
        // 食費: 35,000円 = 35.0%
        // 光熱費: 25,000円 = 25.0%
        // その他: 40,000円 = 40.0%
        var cat1 = BuildCategory(21, "食費", "#F59E0B");
        var cat6 = BuildCategory(22, "光熱費", "#F97316");
        var cat8 = BuildCategory(23, "その他", "#6B7280");
        _context.Categories.AddRange(cat1, cat6, cat8);
        await _context.SaveChangesAsync();

        var expenses = new List<Expense>
        {
            BuildExpense(200, TestUserId, 35000m, 2026, 3, 1, categoryId: 21),
            BuildExpense(201, TestUserId, 25000m, 2026, 3, 2, categoryId: 22),
            BuildExpense(202, TestUserId, 40000m, 2026, 3, 3, categoryId: 23),
        };
        await SeedExpensesAsync(expenses);

        // Act
        var result = await _service.GetMonthlyReportAsync(TestUserId, 2026, 3);

        // Assert: パーセンテージが小数点第1位まで正確であること
        result.TotalAmount.Should().Be(100000m);

        var breakdown = result.CategoryBreakdown.ToList();
        // 金額降順: その他40%、食費35%、光熱費25%
        breakdown[0].Percentage.Should().Be(40.0m); // その他
        breakdown[1].Percentage.Should().Be(35.0m); // 食費
        breakdown[2].Percentage.Should().Be(25.0m); // 光熱費

        // パーセンテージの合計は100%になること
        breakdown.Sum(c => c.Percentage).Should().Be(100.0m);
    }

    [Fact]
    public async Task GetMonthlyReportAsync_EnsuresUserIsolation_ExcludesOtherUsersData()
    {
        // Arrange: テストユーザーと他ユーザーの両方にデータを設定する
        var expenses = new List<Expense>
        {
            BuildExpense(300, TestUserId, 10000m, 2026, 3, 1),
            BuildExpense(301, OtherUserId, 50000m, 2026, 3, 1), // 他ユーザーのデータ
        };
        await SeedExpensesAsync(expenses);

        // Act: テストユーザーのみを集計する
        var result = await _service.GetMonthlyReportAsync(TestUserId, 2026, 3);

        // Assert: 他ユーザーの支出が集計結果に混入していないこと
        result.TotalAmount.Should().Be(10000m);
        result.ExpenseCount.Should().Be(1);
    }

    // =====================================================================
    // GetCategoryBreakdownAsync のテスト
    // =====================================================================

    [Fact]
    public async Task GetCategoryBreakdownAsync_WithExpenses_ReturnsCategoryColorFromMaster()
    {
        // Arrange: カテゴリマスタに色情報を設定する
        var category = BuildCategory(31, "食費", "#FF6384");
        _context.Categories.Add(category);
        await _context.SaveChangesAsync();

        var expense = BuildExpense(400, TestUserId, 5000m, 2026, 3, 1, categoryId: 31);
        await SeedExpensesAsync(new[] { expense });

        // Act
        var result = await _service.GetCategoryBreakdownAsync(TestUserId, 2026, 3);

        // Assert: カテゴリのcolor フィールドがカテゴリマスタから取得されていること
        var breakdown = result.ToList();
        breakdown.Should().HaveCount(1);
        breakdown[0].CategoryColor.Should().Be("#FF6384");
        breakdown[0].CategoryName.Should().Be("食費");
    }

    [Fact]
    public async Task GetCategoryBreakdownAsync_WithNoData_ReturnsEmptyCollection()
    {
        // Act
        var result = await _service.GetCategoryBreakdownAsync(TestUserId, 2026, 2);

        // Assert
        result.Should().BeEmpty();
    }
}
