using FluentAssertions;
using FinFlow.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FinFlow.Tests.Infrastructure;

public class DbContextSeedDataTests
{
    private FinFlowDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<FinFlowDbContext>()
            .UseInMemoryDatabase($"SeedTest_{Guid.NewGuid()}")
            .Options;
        return new FinFlowDbContext(options);
    }

    [Fact]
    public void Categories_SeedData_Contains8SystemCategories()
    {
        // Arrange
        using var context = CreateDbContext();

        // Act
        context.Database.EnsureCreated();
        var systemCategories = context.Categories
            .Where(c => c.IsSystem)
            .ToList();

        // Assert
        systemCategories.Should().HaveCount(8);
    }

    [Fact]
    public void Categories_SeedData_ContainsExpectedNames()
    {
        // Arrange
        using var context = CreateDbContext();
        var expectedNames = new[] { "食費", "交通費", "娯楽", "日用品", "医療費", "光熱費", "通信費", "その他" };

        // Act
        context.Database.EnsureCreated();
        var categoryNames = context.Categories
            .Where(c => c.IsSystem)
            .Select(c => c.Name)
            .ToArray();

        // Assert
        categoryNames.Should().BeEquivalentTo(expectedNames);
    }

    [Fact]
    public void Categories_SeedData_SystemCategoriesHaveNoUserId()
    {
        // Arrange
        using var context = CreateDbContext();

        // Act
        context.Database.EnsureCreated();
        var systemCategories = context.Categories.Where(c => c.IsSystem).ToList();

        // Assert
        systemCategories.Should().AllSatisfy(c => c.UserId.Should().BeNull());
    }
}
