namespace FinFlow.Domain.Interfaces;

public interface ICategoryClassifier
{
    Task<int?> ClassifyAsync(string description, string userId);
}
