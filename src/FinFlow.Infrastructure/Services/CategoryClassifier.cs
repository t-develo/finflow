using FinFlow.Domain.Entities;
using FinFlow.Domain.Interfaces;
using FinFlow.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace FinFlow.Infrastructure.Services;

/// <summary>
/// 支出の説明文からキーワードマッチングによってカテゴリを自動分類するサービス。
/// ユーザー定義ルールを優先し、次にシステムデフォルトルールを適用する。
/// </summary>
public class CategoryClassifier : ICategoryClassifier
{
    private readonly FinFlowDbContext _dbContext;

    // デフォルト分類ルール（DBに未登録のフォールバック）
    // Priority 値が小さいほど優先度が高い
    private static readonly IReadOnlyList<DefaultClassificationRule> DefaultRules = new[]
    {
        new DefaultClassificationRule(Keyword: "コンビニ",  CategorySystemName: "食費",   Priority: 100),
        new DefaultClassificationRule(Keyword: "スーパー",  CategorySystemName: "食費",   Priority: 100),
        new DefaultClassificationRule(Keyword: "電車",      CategorySystemName: "交通費", Priority: 100),
        new DefaultClassificationRule(Keyword: "バス",      CategorySystemName: "交通費", Priority: 100),
        new DefaultClassificationRule(Keyword: "電気",      CategorySystemName: "光熱費", Priority: 100),
        new DefaultClassificationRule(Keyword: "ガス",      CategorySystemName: "光熱費", Priority: 100),
        new DefaultClassificationRule(Keyword: "水道",      CategorySystemName: "光熱費", Priority: 100),
    };

    public CategoryClassifier(FinFlowDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <summary>
    /// 説明文に最も優先度の高いルールのカテゴリIDを返す。
    /// マッチするルールが存在しない場合はnullを返す。
    /// </summary>
    public async Task<int?> ClassifyAsync(string description, string userId)
    {
        if (string.IsNullOrWhiteSpace(description))
            return null;

        // ユーザー定義ルールをDBから取得（Priority昇順で並べる）
        var userRules = await _dbContext.ClassificationRules
            .Where(r => r.UserId == userId)
            .OrderBy(r => r.Priority)
            .ToListAsync();

        var matchedUserRule = FindFirstMatchingRule(description, userRules);
        if (matchedUserRule != null)
            return matchedUserRule.CategoryId;

        // ユーザーのルールでマッチしなかった場合、デフォルトルールをシステムカテゴリ名で解決
        return await ApplyDefaultRulesAsync(description);
    }

    private static ClassificationRule? FindFirstMatchingRule(string description, IEnumerable<ClassificationRule> rules)
    {
        return rules.FirstOrDefault(rule =>
            description.Contains(rule.Keyword, StringComparison.OrdinalIgnoreCase));
    }

    private async Task<int?> ApplyDefaultRulesAsync(string description)
    {
        var matchedDefaultRule = DefaultRules
            .Where(rule => description.Contains(rule.Keyword, StringComparison.OrdinalIgnoreCase))
            .OrderBy(rule => rule.Priority)
            .FirstOrDefault();

        if (matchedDefaultRule == null)
            return null;

        // システムカテゴリ名からIDを解決する
        var systemCategory = await _dbContext.Categories
            .FirstOrDefaultAsync(c => c.IsSystem && c.Name == matchedDefaultRule.CategorySystemName);

        return systemCategory?.Id;
    }

    private record DefaultClassificationRule(string Keyword, string CategorySystemName, int Priority);
}
