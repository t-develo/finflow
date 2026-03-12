using FinFlow.Domain.Entities;
using FinFlow.Infrastructure.Data;
using FinFlow.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace FinFlow.Tests.Classification;

[Trait("Category", "Service")]
public class ClassificationRuleServiceTests
{
    private static FinFlowDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<FinFlowDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new FinFlowDbContext(options);
    }

    private static Category CreateTestCategory(int id, string name, string userId = "user1")
    {
        return new Category
        {
            Id = id,
            Name = name,
            Color = "#3B82F6",
            IsSystem = false,
            UserId = userId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    // =====================================================================
    // GetRulesAsync
    // =====================================================================

    [Fact]
    public async Task GetRulesAsync_WithExistingRules_ReturnsSortedByPriority()
    {
        // Arrange
        await using var context = CreateInMemoryDbContext();
        const string userId = "user1";
        var category = CreateTestCategory(1, "食費", userId);
        context.Categories.Add(category);

        context.ClassificationRules.AddRange(
            new ClassificationRule { UserId = userId, Keyword = "コンビニ", CategoryId = 1, Priority = 200, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
            new ClassificationRule { UserId = userId, Keyword = "スーパー", CategoryId = 1, Priority = 100, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow }
        );
        await context.SaveChangesAsync();

        var service = new ClassificationRuleService(context);

        // Act
        var rules = (await service.GetRulesAsync(userId)).ToList();

        // Assert: Priority昇順で返ること
        rules.Should().HaveCount(2);
        rules[0].Keyword.Should().Be("スーパー"); // Priority=100 が先
        rules[1].Keyword.Should().Be("コンビニ"); // Priority=200 が後
    }

    [Fact]
    public async Task GetRulesAsync_WithOtherUsersRules_ReturnsOnlyCurrentUserRules()
    {
        // Arrange: 別ユーザーのルールが混入しないことを検証（セキュリティ）
        await using var context = CreateInMemoryDbContext();
        var category = CreateTestCategory(1, "食費", "user1");
        context.Categories.Add(category);

        context.ClassificationRules.AddRange(
            new ClassificationRule { UserId = "user1", Keyword = "コンビニ", CategoryId = 1, Priority = 100, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
            new ClassificationRule { UserId = "user2", Keyword = "スーパー", CategoryId = 1, Priority = 100, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow }
        );
        await context.SaveChangesAsync();

        var service = new ClassificationRuleService(context);

        // Act
        var rules = (await service.GetRulesAsync("user1")).ToList();

        // Assert: user1のルールのみ返ること
        rules.Should().HaveCount(1);
        rules[0].Keyword.Should().Be("コンビニ");
    }

    // =====================================================================
    // CreateRuleAsync
    // =====================================================================

    [Fact]
    public async Task CreateRuleAsync_WithValidData_ReturnsCreatedRule()
    {
        // Arrange
        await using var context = CreateInMemoryDbContext();
        const string userId = "user1";
        var category = CreateTestCategory(1, "食費", userId);
        context.Categories.Add(category);
        await context.SaveChangesAsync();

        var service = new ClassificationRuleService(context);
        var newRule = new ClassificationRule
        {
            UserId = userId,
            Keyword = "コンビニ",
            CategoryId = 1,
            Priority = 100
        };

        // Act
        var created = await service.CreateRuleAsync(newRule);

        // Assert
        created.Id.Should().BeGreaterThan(0);
        created.Keyword.Should().Be("コンビニ");
        created.CategoryId.Should().Be(1);
        created.Priority.Should().Be(100);
        created.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task CreateRuleAsync_WithNonExistentCategory_ThrowsValidationException()
    {
        // Arrange
        await using var context = CreateInMemoryDbContext();
        var service = new ClassificationRuleService(context);
        var newRule = new ClassificationRule
        {
            UserId = "user1",
            Keyword = "コンビニ",
            CategoryId = 9999, // 存在しないカテゴリID
            Priority = 100
        };

        // Act & Assert
        await service.Invoking(s => s.CreateRuleAsync(newRule))
            .Should().ThrowAsync<Exception>()
            .WithMessage("*9999*");
    }

    // =====================================================================
    // UpdateRuleAsync
    // =====================================================================

    [Fact]
    public async Task UpdateRuleAsync_WithValidData_ReturnsUpdatedRule()
    {
        // Arrange
        await using var context = CreateInMemoryDbContext();
        const string userId = "user1";
        var category = CreateTestCategory(1, "食費", userId);
        context.Categories.Add(category);

        var existingRule = new ClassificationRule
        {
            UserId = userId, Keyword = "コンビニ", CategoryId = 1, Priority = 100,
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
        context.ClassificationRules.Add(existingRule);
        await context.SaveChangesAsync();

        var service = new ClassificationRuleService(context);
        var updatedRule = new ClassificationRule
        {
            UserId = userId, Keyword = "コンビニATM", CategoryId = 1, Priority = 50
        };

        // Act
        var result = await service.UpdateRuleAsync(existingRule.Id, userId, updatedRule);

        // Assert
        result.Should().NotBeNull();
        result!.Keyword.Should().Be("コンビニATM");
        result.Priority.Should().Be(50);
    }

    [Fact]
    public async Task UpdateRuleAsync_WithNonExistentId_ReturnsNull()
    {
        // Arrange
        await using var context = CreateInMemoryDbContext();
        var service = new ClassificationRuleService(context);

        // Act
        var result = await service.UpdateRuleAsync(9999, "user1", new ClassificationRule { CategoryId = 1 });

        // Assert
        result.Should().BeNull();
    }

    // =====================================================================
    // DeleteRuleAsync
    // =====================================================================

    [Fact]
    public async Task DeleteRuleAsync_WithExistingId_ReturnsTrueAndRemovesRule()
    {
        // Arrange
        await using var context = CreateInMemoryDbContext();
        const string userId = "user1";
        var category = CreateTestCategory(1, "食費", userId);
        context.Categories.Add(category);

        var rule = new ClassificationRule
        {
            UserId = userId, Keyword = "コンビニ", CategoryId = 1, Priority = 100,
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
        context.ClassificationRules.Add(rule);
        await context.SaveChangesAsync();

        var service = new ClassificationRuleService(context);

        // Act
        var deleted = await service.DeleteRuleAsync(rule.Id, userId);

        // Assert
        deleted.Should().BeTrue();
        context.ClassificationRules.Should().BeEmpty();
    }

    [Fact]
    public async Task DeleteRuleAsync_WithOtherUsersRule_ReturnsFalse()
    {
        // Arrange: 他ユーザーのルールを削除しようとした場合
        await using var context = CreateInMemoryDbContext();
        var category = CreateTestCategory(1, "食費", "user1");
        context.Categories.Add(category);

        var rule = new ClassificationRule
        {
            UserId = "user1", Keyword = "コンビニ", CategoryId = 1, Priority = 100,
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
        context.ClassificationRules.Add(rule);
        await context.SaveChangesAsync();

        var service = new ClassificationRuleService(context);

        // Act: user2がuser1のルールを削除しようとする
        var deleted = await service.DeleteRuleAsync(rule.Id, "user2");

        // Assert: 削除されないこと（セキュリティ）
        deleted.Should().BeFalse();
        context.ClassificationRules.Should().HaveCount(1);
    }
}
