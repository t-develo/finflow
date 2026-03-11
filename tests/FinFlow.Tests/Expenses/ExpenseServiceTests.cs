using FinFlow.Domain.Entities;
using FinFlow.Domain.Exceptions;
using FinFlow.Domain.Interfaces;
using FinFlow.Infrastructure.Data;
using FinFlow.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace FinFlow.Tests.Expenses;

[Trait("Category", "ExpenseService")]
public class ExpenseServiceTests
{
    private readonly FinFlowDbContext _dbContext;
    private readonly ExpenseService _service;
    private const string TestUserId = "test-user-001";

    public ExpenseServiceTests()
    {
        var options = new DbContextOptionsBuilder<FinFlowDbContext>()
            .UseInMemoryDatabase($"ExpenseServiceTest_{Guid.NewGuid()}")
            .Options;

        _dbContext = new FinFlowDbContext(options);
        _dbContext.Database.EnsureCreated();
        _service = new ExpenseService(_dbContext);
    }

    // =====================================================================
    // GetExpensesAsync のテスト
    // =====================================================================

    [Fact]
    public async Task GetExpensesAsync_WithUserHavingExpenses_ReturnsOnlyUserExpenses()
    {
        // Arrange: 別ユーザーの支出が混在する状況
        var otherUserId = "other-user-999";
        _dbContext.Expenses.AddRange(
            CreateExpense(TestUserId, 1000m, new DateOnly(2026, 3, 1)),
            CreateExpense(TestUserId, 2000m, new DateOnly(2026, 3, 2)),
            CreateExpense(otherUserId, 9999m, new DateOnly(2026, 3, 3))
        );
        await _dbContext.SaveChangesAsync();

        // Act
        var expenses = await _service.GetExpensesAsync(TestUserId);

        // Assert: 他ユーザーのデータは取得されない
        expenses.Should().HaveCount(2);
        expenses.Should().AllSatisfy(e => e.UserId.Should().Be(TestUserId));
    }

    [Fact]
    public async Task GetExpensesAsync_ReturnsExpensesInDescendingDateOrder()
    {
        // Arrange
        _dbContext.Expenses.AddRange(
            CreateExpense(TestUserId, 1000m, new DateOnly(2026, 3, 1)),
            CreateExpense(TestUserId, 2000m, new DateOnly(2026, 3, 10)),
            CreateExpense(TestUserId, 3000m, new DateOnly(2026, 3, 5))
        );
        await _dbContext.SaveChangesAsync();

        // Act
        var expenses = (await _service.GetExpensesAsync(TestUserId)).ToList();

        // Assert: 日付降順
        expenses[0].Date.Should().Be(new DateOnly(2026, 3, 10));
        expenses[1].Date.Should().Be(new DateOnly(2026, 3, 5));
        expenses[2].Date.Should().Be(new DateOnly(2026, 3, 1));
    }

    // =====================================================================
    // GetExpenseByIdAsync のテスト
    // =====================================================================

    [Fact]
    public async Task GetExpenseByIdAsync_WithValidIdAndUser_ReturnsExpense()
    {
        // Arrange
        var expense = CreateExpense(TestUserId, 1500m, new DateOnly(2026, 3, 8));
        _dbContext.Expenses.Add(expense);
        await _dbContext.SaveChangesAsync();

        // Act
        var found = await _service.GetExpenseByIdAsync(expense.Id, TestUserId);

        // Assert
        found.Should().NotBeNull();
        found!.Amount.Should().Be(1500m);
    }

    [Fact]
    public async Task GetExpenseByIdAsync_WithDifferentUser_ReturnsNull()
    {
        // Arrange: 別ユーザーの支出は取得できない（セキュリティ確認）
        var expense = CreateExpense("other-user", 1500m, new DateOnly(2026, 3, 8));
        _dbContext.Expenses.Add(expense);
        await _dbContext.SaveChangesAsync();

        // Act
        var found = await _service.GetExpenseByIdAsync(expense.Id, TestUserId);

        // Assert
        found.Should().BeNull();
    }

    // =====================================================================
    // CreateExpenseAsync のテスト
    // =====================================================================

    [Fact]
    public async Task CreateExpenseAsync_WithValidData_ReturnsCreatedExpense()
    {
        // Arrange: システムカテゴリ（食費）はシードデータで存在する
        var foodCategoryId = 1;
        var expense = new Expense
        {
            UserId = TestUserId,
            Amount = 1500m,
            CategoryId = foodCategoryId,
            Date = new DateOnly(2026, 3, 8),
            Description = "コンビニ 昼食"
        };

        // Act
        var created = await _service.CreateExpenseAsync(expense);

        // Assert
        created.Id.Should().BeGreaterThan(0);
        created.Amount.Should().Be(1500m);
        created.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        created.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task CreateExpenseAsync_WithNonExistentCategory_ThrowsValidationException()
    {
        // Arrange: 存在しないカテゴリID
        var expense = new Expense
        {
            UserId = TestUserId,
            Amount = 1500m,
            CategoryId = 99999,
            Date = new DateOnly(2026, 3, 8),
            Description = "存在しないカテゴリ"
        };

        // Act & Assert
        await _service.Invoking(s => s.CreateExpenseAsync(expense))
            .Should().ThrowAsync<ValidationException>();
    }

    // =====================================================================
    // UpdateExpenseAsync のテスト
    // =====================================================================

    [Fact]
    public async Task UpdateExpenseAsync_WithValidData_ReturnsUpdatedExpense()
    {
        // Arrange
        var expense = CreateExpense(TestUserId, 1000m, new DateOnly(2026, 3, 1));
        expense.CategoryId = 1; // 食費
        _dbContext.Expenses.Add(expense);
        await _dbContext.SaveChangesAsync();

        var updatedData = new Expense
        {
            Amount = 2000m,
            CategoryId = 2, // 交通費
            Date = new DateOnly(2026, 3, 10),
            Description = "更新後の説明"
        };

        // Act
        var result = await _service.UpdateExpenseAsync(expense.Id, TestUserId, updatedData);

        // Assert
        result.Should().NotBeNull();
        result!.Amount.Should().Be(2000m);
        result.Description.Should().Be("更新後の説明");
    }

    [Fact]
    public async Task UpdateExpenseAsync_WithNonExistentId_ReturnsNull()
    {
        // Arrange
        var nonExistentId = 99999;
        var updatedData = new Expense
        {
            Amount = 1000m,
            CategoryId = 1,
            Date = new DateOnly(2026, 3, 1),
            Description = "存在しないID"
        };

        // Act
        var result = await _service.UpdateExpenseAsync(nonExistentId, TestUserId, updatedData);

        // Assert
        result.Should().BeNull();
    }

    // =====================================================================
    // DeleteExpenseAsync のテスト
    // =====================================================================

    [Fact]
    public async Task DeleteExpenseAsync_WithValidId_ReturnsTrueAndRemovesExpense()
    {
        // Arrange
        var expense = CreateExpense(TestUserId, 1000m, new DateOnly(2026, 3, 1));
        _dbContext.Expenses.Add(expense);
        await _dbContext.SaveChangesAsync();

        // Act
        var deleted = await _service.DeleteExpenseAsync(expense.Id, TestUserId);

        // Assert
        deleted.Should().BeTrue();
        var found = await _dbContext.Expenses.FindAsync(expense.Id);
        found.Should().BeNull();
    }

    [Fact]
    public async Task DeleteExpenseAsync_WithNonExistentId_ReturnsFalse()
    {
        // Arrange
        const int nonExistentId = 99999;

        // Act
        var deleted = await _service.DeleteExpenseAsync(nonExistentId, TestUserId);

        // Assert
        deleted.Should().BeFalse();
    }

    // =====================================================================
    // フィルタリングのテスト
    // =====================================================================

    [Fact]
    public async Task GetExpensesAsync_WithDateFilter_ReturnsFilteredExpenses()
    {
        // Arrange
        _dbContext.Expenses.AddRange(
            CreateExpense(TestUserId, 1000m, new DateOnly(2026, 1, 15)),
            CreateExpense(TestUserId, 2000m, new DateOnly(2026, 3, 5)),
            CreateExpense(TestUserId, 3000m, new DateOnly(2026, 3, 20))
        );
        await _dbContext.SaveChangesAsync();

        var filter = new ExpenseFilter
        {
            From = new DateOnly(2026, 3, 1),
            To = new DateOnly(2026, 3, 31)
        };

        // Act
        var expenses = (await _service.GetExpensesAsync(TestUserId, filter)).ToList();

        // Assert: 3月のみが返る
        expenses.Should().HaveCount(2);
        expenses.Should().AllSatisfy(e => e.Date.Month.Should().Be(3));
    }

    // =====================================================================
    // ヘルパー
    // =====================================================================

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
