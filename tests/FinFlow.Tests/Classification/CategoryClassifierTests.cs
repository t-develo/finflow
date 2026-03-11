using FinFlow.Domain.Entities;
using FinFlow.Infrastructure.Data;
using FinFlow.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace FinFlow.Tests.Classification;

[Trait("Category", "CategoryClassifier")]
public class CategoryClassifierTests
{
    private readonly FinFlowDbContext _dbContext;
    private readonly CategoryClassifier _classifier;

    // テスト間で一意なDBを使い、テストの独立性を担保する
    public CategoryClassifierTests()
    {
        var options = new DbContextOptionsBuilder<FinFlowDbContext>()
            .UseInMemoryDatabase($"ClassifierTest_{Guid.NewGuid()}")
            .Options;

        _dbContext = new FinFlowDbContext(options);
        _dbContext.Database.EnsureCreated();
        _classifier = new CategoryClassifier(_dbContext);
    }

    // =====================================================================
    // デフォルトルール（システムカテゴリ）のテスト
    // =====================================================================

    [Fact]
    public async Task ClassifyAsync_WithConvenienceStoreKeyword_ReturnsFoodCategoryId()
    {
        // Arrange: "コンビニ" → 食費 (Id=1) のデフォルトルール
        const string description = "コンビニ 昼食";
        const string userId = "user-001";

        // Act
        var categoryId = await _classifier.ClassifyAsync(description, userId);

        // Assert
        categoryId.Should().NotBeNull();
        var category = await _dbContext.Categories.FindAsync(categoryId);
        category!.Name.Should().Be("食費");
    }

    [Fact]
    public async Task ClassifyAsync_WithSupermarketKeyword_ReturnsFoodCategoryId()
    {
        // Arrange: "スーパー" → 食費
        const string description = "スーパー 週末の買い物";
        const string userId = "user-001";

        // Act
        var categoryId = await _classifier.ClassifyAsync(description, userId);

        // Assert
        categoryId.Should().NotBeNull();
        var category = await _dbContext.Categories.FindAsync(categoryId);
        category!.Name.Should().Be("食費");
    }

    [Fact]
    public async Task ClassifyAsync_WithTrainKeyword_ReturnsTransportCategoryId()
    {
        // Arrange: "電車" → 交通費
        const string description = "電車代 通勤";
        const string userId = "user-001";

        // Act
        var categoryId = await _classifier.ClassifyAsync(description, userId);

        // Assert
        categoryId.Should().NotBeNull();
        var category = await _dbContext.Categories.FindAsync(categoryId);
        category!.Name.Should().Be("交通費");
    }

    [Fact]
    public async Task ClassifyAsync_WithElectricityKeyword_ReturnsUtilityCategoryId()
    {
        // Arrange: "電気" → 光熱費
        const string description = "電気代 3月分";
        const string userId = "user-001";

        // Act
        var categoryId = await _classifier.ClassifyAsync(description, userId);

        // Assert
        categoryId.Should().NotBeNull();
        var category = await _dbContext.Categories.FindAsync(categoryId);
        category!.Name.Should().Be("光熱費");
    }

    [Fact]
    public async Task ClassifyAsync_WithNoMatchingKeyword_ReturnsNull()
    {
        // Arrange: マッチするルールが存在しない説明文
        const string description = "定期購読サービス";
        const string userId = "user-001";

        // Act
        var categoryId = await _classifier.ClassifyAsync(description, userId);

        // Assert
        categoryId.Should().BeNull();
    }

    [Fact]
    public async Task ClassifyAsync_WithEmptyDescription_ReturnsNull()
    {
        // Arrange: 空文字はnullを返す（早期リターン）
        const string description = "";
        const string userId = "user-001";

        // Act
        var categoryId = await _classifier.ClassifyAsync(description, userId);

        // Assert
        categoryId.Should().BeNull();
    }

    // =====================================================================
    // ユーザー定義ルールのテスト
    // =====================================================================

    [Fact]
    public async Task ClassifyAsync_WithUserDefinedRule_UserRuleTakesPriorityOverDefault()
    {
        // Arrange: ユーザーが「コンビニ」を「娯楽」に分類するカスタムルールを定義
        const string userId = "user-custom-rule";
        var entertainmentCategory = await _dbContext.Categories
            .FirstAsync(c => c.Name == "娯楽");

        _dbContext.ClassificationRules.Add(new ClassificationRule
        {
            UserId = userId,
            Keyword = "コンビニ",
            CategoryId = entertainmentCategory.Id,
            Priority = 1,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await _dbContext.SaveChangesAsync();

        // Act
        var categoryId = await _classifier.ClassifyAsync("コンビニ 昼食", userId);

        // Assert: ユーザー定義ルールが優先される
        categoryId.Should().Be(entertainmentCategory.Id);
    }

    [Fact]
    public async Task ClassifyAsync_WithMultipleUserRules_ReturnsHighestPriorityMatch()
    {
        // Arrange: 複数ユーザールールのうち、Priority値が小さい方が優先される
        const string userId = "user-multi-rule";
        var foodCategory = await _dbContext.Categories.FirstAsync(c => c.Name == "食費");
        var transportCategory = await _dbContext.Categories.FirstAsync(c => c.Name == "交通費");

        _dbContext.ClassificationRules.AddRange(
            new ClassificationRule
            {
                UserId = userId,
                Keyword = "スーパー",
                CategoryId = foodCategory.Id,
                Priority = 10,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new ClassificationRule
            {
                UserId = userId,
                Keyword = "スーパー",
                CategoryId = transportCategory.Id,
                Priority = 5, // こちらがPriority低い（優先度高い）
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            }
        );
        await _dbContext.SaveChangesAsync();

        // Act
        var categoryId = await _classifier.ClassifyAsync("スーパー 特売", userId);

        // Assert: Priority=5のルール（交通費）が選ばれる
        categoryId.Should().Be(transportCategory.Id);
    }
}
