# Testing Standards — FinFlow

## カバレッジ基準

- **最低80%** のテストカバレッジを維持
- **100%** 必須: 金額計算、認証・認可、CSVパース、セキュリティ重要ロジック

## 必須テスト種別

1. **ユニットテスト** — サービス・ドメインロジックの単体テスト
2. **統合テスト** — APIエンドポイント・DBアクセスのテスト
3. **E2Eテスト** — 重要ユーザーフロー（支出登録→レポート表示など）

## TDDサイクル（RED-GREEN-REFACTOR）

1. **RED**: 失敗するテストを書く
2. **GREEN**: テストを通す最小限のコードを書く
3. **REFACTOR**: テストを通したままコードを改善する

## C# / xUnit規約

```csharp
// テスト名: 対象メソッド_状況_期待結果
[Fact]
public async Task CreateExpenseAsync_ValidInput_ReturnsCreatedExpense()
{
    // Arrange
    // Act
    // Assert — FluentAssertions を使用
    result.Should().NotBeNull();
    result.Amount.Should().Be(1000m);
}

[Theory]
[InlineData(0)]
[InlineData(-1)]
public async Task CreateExpenseAsync_InvalidAmount_ThrowsValidationException(decimal amount)
```

## テスト実行コマンド

```bash
# 全テスト
dotnet test

# 特定クラス
dotnet test --filter "ClassName=ExpensesControllerTests"

# 特定メソッド
dotnet test --filter "FullyQualifiedName~ExpensesControllerTests.MethodName"
```

## ユーザー分離のテスト

UserId によるデータ分離は必ずテストすること:
- ユーザーAのデータがユーザーBに見えないことを確認
- 他ユーザーのリソースへのアクセスが403を返すことを確認
