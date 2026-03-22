# C# Testing — FinFlow (xUnit + FluentAssertions)

## テスト命名規則

```
{対象メソッド}_{状況}_{期待結果}
```

例:
```csharp
CreateExpenseAsync_ValidInput_ReturnsCreatedExpense
GetExpenseByIdAsync_NotExistingId_ReturnsNull
DeleteExpenseAsync_OtherUsersExpense_ThrowsUnauthorizedException
```

## テスト構造（AAA パターン）

```csharp
[Fact]
public async Task CreateExpenseAsync_ValidInput_ReturnsCreatedExpense()
{
    // Arrange
    var userId = "user-123";
    var request = new CreateExpenseRequest(Amount: 1500m, Description: "昼食", Date: DateTime.Today);
    var mockRepo = new Mock<IExpenseRepository>();
    mockRepo.Setup(r => r.CreateAsync(It.IsAny<Expense>()))
            .ReturnsAsync(new Expense { Id = 1, Amount = 1500m });
    var service = new ExpenseService(mockRepo.Object);

    // Act
    var result = await service.CreateExpenseAsync(userId, request);

    // Assert
    result.Should().NotBeNull();
    result.Amount.Should().Be(1500m);
    mockRepo.Verify(r => r.CreateAsync(It.IsAny<Expense>()), Times.Once);
}
```

## パラメータ化テスト

```csharp
[Theory]
[InlineData(0)]
[InlineData(-100)]
[InlineData(-0.01)]
public async Task CreateExpenseAsync_InvalidAmount_ThrowsValidationException(decimal amount)
{
    // ...
    await act.Should().ThrowAsync<ValidationException>();
}
```

## ユーザー分離テスト（必須）

```csharp
[Fact]
public async Task GetExpenseByIdAsync_OtherUsersExpense_ReturnsNull()
{
    // 別ユーザーのExpenseにアクセスできないことを確認
    var expense = await _service.GetExpenseByIdAsync(expenseId, otherUserId: "user-999");
    expense.Should().BeNull();
}
```

## 統合テスト（APIエンドポイント）

```csharp
public class ExpensesControllerTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    [Fact]
    public async Task POST_Expenses_ValidRequest_Returns201()
    {
        var response = await _client.PostAsJsonAsync("/api/expenses", validRequest);
        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task GET_Expenses_Unauthenticated_Returns401()
    {
        var response = await _client.GetAsync("/api/expenses");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
```

## FluentAssertions よく使うアサーション

```csharp
result.Should().NotBeNull();
result.Amount.Should().Be(1500m);
result.Should().BeOfType<ExpenseDto>();
list.Should().HaveCount(3);
list.Should().Contain(e => e.Amount == 1500m);
await act.Should().ThrowAsync<NotFoundException>();
response.StatusCode.Should().Be(HttpStatusCode.OK);
```
