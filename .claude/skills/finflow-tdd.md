# FinFlow TDD・テスト戦略【全ロール共通】

t_wadaメソッドに基づくTDDの実践と、FinFlow各レイヤーのテスト基準。

---

## TDDサイクル（Red → Green → Refactor）

```
Red: 失敗するテストを書く（仕様を先にコードで表現する）
  ↓
Green: テストを通す最小限の実装をする（ここでは美しさより正しさ）
  ↓
Refactor: テストが通った状態を保ちながらコードを改善する
  ↓（繰り返す）
```

**テストが書きにくい = 設計の問題のシグナル。** テスタビリティは設計品質の指標として使う。

---

## テストの基本構造

### Arrange-Act-Assert パターン

```csharp
[Fact]
public async Task CreateExpenseAsync_WithValidData_ReturnsCreatedExpense()
{
    // Arrange: テストデータとモックの準備
    var userId = "user-123";
    var dto = new CreateExpenseDto { Amount = 1500, Description = "昼食" };

    // Act: テスト対象メソッドの実行
    var result = await _service.CreateExpenseAsync(userId, dto);

    // Assert: 結果の検証（FluentAssertionsを使用）
    result.Should().NotBeNull();
    result.Amount.Should().Be(1500);
    result.UserId.Should().Be(userId);
}
```

### テスト名の命名規則

```
メソッド名_条件_期待結果

例:
CreateExpenseAsync_WithValidData_ReturnsCreatedExpense
GetExpensesAsync_WithNoExpenses_ReturnsEmptyList
ImportCsvAsync_WithInvalidRow_SkipsRowAndContinues
Classify_WithMatchingKeyword_ReturnsCategoryId
```

日本語テスト名も可（仕様のドキュメントとして機能させる場合）:
```csharp
[Fact]
public async Task 月次集計_支出が0件の場合_空の正常レスポンスを返す()
```

---

## F.I.R.S.T 原則

| 原則 | 意味 | 守り方 |
|------|------|--------|
| **Fast** | 高速に実行される | DBアクセスは最小化、モックを活用 |
| **Independent** | 実行順序に依存しない | テスト間で状態を共有しない |
| **Repeatable** | 環境を問わず同じ結果 | 現在時刻に依存しない（固定日付を使う） |
| **Self-validating** | 合否が自動判定できる | Assertで明示的に検証 |
| **Timely** | 実装と同時に書く | TDDの場合は実装前に書く |

---

## FinFlow テスト基準（レイヤー別）

### バックエンド（SE-1 / SE-2）

| レイヤー | 種別 | 最低件数 | ツール |
|---------|------|---------|--------|
| サービス層 | ユニットテスト | 正常系2 + 異常系1 + エッジ1 | xUnit + FluentAssertions |
| コントローラー層 | 統合テスト | 正常系1 + 400系1 + 404系1 | WebApplicationFactory |
| CSVパーサー（SE-1） | ユニットテスト | 正常3 + 異常行スキップ1 + エンコーディング1 | xUnit |
| 集計サービス（SE-2） | ユニットテスト | 正常3 + エッジ2（月末・0件） | xUnit + FluentAssertions |
| 通知スケジューラ（SE-2） | ユニットテスト | 正常2 + 期限外1 | xUnit |

### フロントエンド（SE-3）

xUnitテストは対象外。代わりに**動作確認チェックリスト**を作成して手動テストを体系化する。
UIロジック（バリデーション関数等）はDOMから分離し、テスト可能な純粋関数として実装する。

---

## テスト設計のポイント

### 集計テストは手計算で期待値を確認する

```csharp
// テストデータで事前に期待値を手計算しておく
var expenses = new[]
{
    new Expense { Amount = 1000m, Date = new DateTime(2026, 3, 1) },
    new Expense { Amount = 2500m, Date = new DateTime(2026, 3, 15) },
    new Expense { Amount = 500m,  Date = new DateTime(2026, 3, 31) }
};
// 期待値: 合計4000円、件数3、1日平均 4000/31 ≒ 129.0円（手計算）

result.TotalAmount.Should().Be(4000m);
result.TransactionCount.Should().Be(3);
result.DailyAverage.Should().Be(Math.Round(4000m / 31, 1, MidpointRounding.AwayFromZero));
```

### 境界値テストを必ず含める

- 月末日（1/31, 2/28, 2/29うるう年, 3/31）
- 0件データ（空配列、空月）
- 最大値・最小値
- CSVの最大行数（10,000行）、エンコーディング（UTF-8, Shift_JIS）

### モックの使い方

```csharp
// モックはインターフェース境界で使用する
var mockDb = new Mock<FinFlowDbContext>();
var mockEmailSender = new Mock<IEmailSender>();

// 過度なモックはテストの信頼性を下げる
// → 実装の詳細ではなく、振る舞いを検証する
```

---

## テスト時の禁止事項

- テストを書かずにコードをコミットしない
- テスト間でstaticな状態を共有しない
- テストデータを外部ファイルやDBに依存させない（テスト内で完結）
- 現在時刻（`DateTime.Now`）をそのままテストで使わない（固定日付を注入）
- `Thread.Sleep()` をテストコードで使わない
