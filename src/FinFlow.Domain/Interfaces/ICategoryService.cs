using FinFlow.Domain.Entities;

namespace FinFlow.Domain.Interfaces;

public interface ICategoryService
{
    Task<IEnumerable<Category>> GetCategoriesAsync(string userId);
    Task<Category?> GetCategoryByIdAsync(int id, string userId);
    Task<Category> CreateCategoryAsync(Category category);
    Task<Category?> UpdateCategoryAsync(int id, string userId, Category updated);
    Task<bool> DeleteCategoryAsync(int id, string userId);
}
