using FinFlow.Domain.Entities;
using FinFlow.Domain.Interfaces;
using FinFlow.Infrastructure.Data;
using FinFlow.Infrastructure.Identity;
using FinFlow.Infrastructure.Services;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace FinFlow.Tests.Infrastructure;

/// <summary>
/// ラズパイ向け SQLite 構成（Database:Provider=Sqlite）の金額マッピング検証。
///
/// SQLite には decimal 型が無く、"decimal(18,2)" 列は NUMERIC affinity になるため、
/// 小数を含む金額は REAL（8バイト浮動小数点）として保存され丸め誤差が発生する。
/// これは「金額に float/double を使わない」という規約違反でもある。
/// FinFlowDbContext は SQLite の場合のみ最小単位(×100)の long へ値変換してこれを回避しており、
/// このテストはその回帰テストとして機能する。
/// </summary>
[Trait("Category", "SqliteMoneyMapping")]
public class SqliteMoneyMappingTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly FinFlowDbContext _dbContext;
    private readonly ExpenseService _service;
    private const string TestUserId = "sqlite-user-001";

    public SqliteMoneyMappingTests()
    {
        // in-memory SQLite は接続を開いている間だけ生存するため、接続を保持する
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<FinFlowDbContext>()
            .UseSqlite(_connection)
            .Options;

        _dbContext = new FinFlowDbContext(options);
        _dbContext.Database.EnsureCreated();

        // SQLite は InMemory と違い外部キー制約を実際に適用するため、先にユーザーを作る
        _dbContext.Users.Add(new ApplicationUser
        {
            Id = TestUserId,
            UserName = "sqlite-test@example.com",
            NormalizedUserName = "SQLITE-TEST@EXAMPLE.COM",
            Email = "sqlite-test@example.com",
            NormalizedEmail = "SQLITE-TEST@EXAMPLE.COM"
        });
        _dbContext.SaveChanges();

        _service = new ExpenseService(_dbContext);
    }

    [Fact]
    public void Amount_OnSqlite_IsStoredAsIntegerMinorUnits()
    {
        // Arrange & Act: モデル上の列型を確認する
        var storeType = _dbContext.Model
            .FindEntityType(typeof(Expense))!
            .FindProperty(nameof(Expense.Amount))!
            .GetColumnType();

        // Assert: decimal(18,2) のままだと REAL（浮動小数点）で保存されてしまう
        storeType.Should().Be("INTEGER");
    }

    [Theory]
    [InlineData("1500")]
    [InlineData("900")]
    [InlineData("1234.56")]
    [InlineData("0.01")]
    [InlineData("10000000")]
    // REAL 保存だと 99999999999999.98 に化ける値（decimal(18,2) の上限付近）
    [InlineData("99999999999999.99")]
    public async Task Amount_OnSqlite_RoundTripsExactlyAsDecimal(string rawAmount)
    {
        // Arrange
        var amount = decimal.Parse(rawAmount);
        _dbContext.Expenses.Add(CreateExpense(TestUserId, amount, new DateOnly(2026, 7, 1)));
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        // Act
        var stored = await _dbContext.Expenses.SingleAsync();

        // Assert: 金額は decimal として厳密に一致すること（float/double 変換は規約で禁止）
        stored.Amount.Should().Be(amount);
    }

    [Fact]
    public async Task GetExpensesAsync_OnSqliteWithMinAmount_FiltersByAmountCorrectly()
    {
        // Arrange: 値変換後も SQL 側の金額比較が正しく行われることを確認する
        _dbContext.Expenses.AddRange(
            CreateExpense(TestUserId, 900m, new DateOnly(2026, 7, 1)),
            CreateExpense(TestUserId, 1500m, new DateOnly(2026, 7, 2)),
            CreateExpense(TestUserId, 20000m, new DateOnly(2026, 7, 3))
        );
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.GetExpensesAsync(TestUserId, new ExpenseFilter { MinAmount = 1000m });

        // Assert
        result.Select(e => e.Amount).Should().BeEquivalentTo(new[] { 1500m, 20000m });
    }

    [Fact]
    public async Task GetExpensesAsync_OnSqliteWithMaxAmount_FiltersByAmountCorrectly()
    {
        // Arrange: 値変換後も SQL 側の金額比較が正しく行われることを確認する
        _dbContext.Expenses.AddRange(
            CreateExpense(TestUserId, 900m, new DateOnly(2026, 7, 1)),
            CreateExpense(TestUserId, 1500m, new DateOnly(2026, 7, 2)),
            CreateExpense(TestUserId, 20000m, new DateOnly(2026, 7, 3))
        );
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.GetExpensesAsync(TestUserId, new ExpenseFilter { MaxAmount = 1500m });

        // Assert
        result.Select(e => e.Amount).Should().BeEquivalentTo(new[] { 900m, 1500m });
    }

    [Fact]
    public async Task SubscriptionAmount_OnSqlite_RoundTripsExactlyAsDecimal()
    {
        // Arrange
        _dbContext.Subscriptions.Add(new Subscription
        {
            UserId = TestUserId,
            ServiceName = "動画配信サービス",
            Amount = 1078.50m,
            BillingCycle = "monthly",
            NextBillingDate = new DateOnly(2026, 8, 1),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        // Act
        var stored = await _dbContext.Subscriptions.SingleAsync();

        // Assert
        stored.Amount.Should().Be(1078.50m);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        _connection.Dispose();
        GC.SuppressFinalize(this);
    }

    private static Expense CreateExpense(string userId, decimal amount, DateOnly date) =>
        new()
        {
            UserId = userId,
            Amount = amount,
            Date = date,
            Description = "テスト支出",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
}
