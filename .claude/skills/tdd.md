# TDD・テスト戦略【汎用】

t_wadaメソッドに基づくTDDの実践と、テスト設計の基本原則。

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

**重要:** リファクタリングとフィーチャー追加は**別コミット**にする。帽子を被り替える意識を持つ。

---

## Arrange-Act-Assert パターン

```csharp
[Fact]
public async Task CreateAsync_WithValidData_ReturnsCreatedEntity()
{
    // Arrange: テストデータとモックの準備
    var dto = new CreateDto { Name = "テスト" };

    // Act: テスト対象メソッドの実行
    var result = await _service.CreateAsync(dto);

    // Assert: 結果の検証（FluentAssertionsを使用）
    result.Should().NotBeNull();
    result.Name.Should().Be("テスト");
}
```

---

## テスト名の命名規則

```
メソッド名_条件_期待結果

例:
CreateAsync_WithValidData_ReturnsCreatedEntity
GetAllAsync_WithNoRecords_ReturnsEmptyList
ImportCsv_WithInvalidRow_SkipsRowAndContinues
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

## テストカバレッジの考え方

各テストには以下を含める:

| 種別 | 内容 |
|------|------|
| **正常系** | 典型的な正しい入力での期待動作 |
| **異常系** | 不正入力・存在しないID・権限なし |
| **エッジケース** | 境界値（0件、最大値、月末日、null、空文字） |

---

## モックの使い方

```csharp
// モックはインターフェース境界で使用する
var mockRepo = new Mock<IItemRepository>();
mockRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(testItem);

// 過度なモックはテストの信頼性を下げる
// → 実装の詳細ではなく、振る舞いを検証する
```

---

## 境界値テストの基本

- 数値: 0, 1, 最大値-1, 最大値, 最大値+1
- コレクション: 空配列, 1件, 最大件数
- 日付: 月初, 月末, うるう年, タイムゾーン境界
- 文字列: 空文字, null, 最大長, 最大長+1

---

## テスト設計のアンチパターン（禁止）

- テストを書かずにコードをコミットする
- テスト間でstaticな状態を共有する
- テストデータを外部ファイルやDBに依存させる（テスト内で完結させる）
- 現在時刻（`DateTime.Now`）をそのままテストで使う（固定日付を注入する）
- `Thread.Sleep()` をテストコードで使う
- テスト名を `Test1`, `TestA` のような意味のない名前にする
