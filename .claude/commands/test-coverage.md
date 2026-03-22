# /test-coverage — Coverage Analysis

テストカバレッジを80%以上に引き上げる。

## カバレッジ測定

```bash
# xUnit + Coverlet でカバレッジ測定
dotnet test --collect:"XPlat Code Coverage" \
  --results-directory ./coverage

# レポート生成（ReportGenerator ツールが必要）
dotnet tool install --global dotnet-reportgenerator-globaltool
reportgenerator \
  -reports:"coverage/**/coverage.cobertura.xml" \
  -targetdir:"coverage/report" \
  -reporttypes:Html

# カバレッジ閾値付きテスト実行
dotnet test /p:CollectCoverage=true \
  /p:CoverletOutputFormat=opencover \
  /p:Threshold=80
```

## カバレッジ基準

| コード種別 | 目標 |
|-----------|------|
| 全体 | 80%+ |
| Expense/Category サービス | 90%+ |
| 金額計算・集計ロジック | 100% |
| 認証・JWT処理 | 100% |
| CSVパーサー | 100% |

## カバレッジが低いコードへの対処

### 優先的にテストを追加すべきコード

1. **コアビジネスロジック** — 金額計算、集計、分類
2. **エラーハンドリングパス** — 例外処理の branches
3. **バリデーションロジック** — 境界値、無効入力
4. **APIエンドポイント** — 全HTTPメソッド・ステータスコード

### テスト追加のパターン

```csharp
// ハッピーパス: 正常入力
[Fact]
public async Task CreateExpenseAsync_ValidInput_ReturnsExpense() { }

// エッジケース: 境界値
[Theory]
[InlineData(0.01)]      // 最小値
[InlineData(9_999_999.99)]  // 最大値付近
public async Task CreateExpenseAsync_BoundaryAmount_Succeeds(decimal amount) { }

// エラーパス: 無効入力
[Theory]
[InlineData(0)]
[InlineData(-1)]
public async Task CreateExpenseAsync_InvalidAmount_ThrowsException(decimal amount) { }
```

## カバレッジレポートの読み方

- **Line coverage**: 実行された行の割合
- **Branch coverage**: if/else の全分岐が通ったか
- **Method coverage**: テストされたメソッドの割合

Branch coverage の確認が特に重要（if文のelseブランチを忘れがち）。
