# /tdd — Test-Driven Development

RED-GREEN-REFACTOR サイクルで機能を実装する。

## TDDサイクル

```
RED    → 失敗するテストを書く
GREEN  → テストを通す最小限のコードを書く
REFACTOR → テストを通したままコードを改善する
```

**RED フェーズを絶対にスキップしない。テストなしでコードを書かない。**

## 適用タイミング

- 新機能の実装
- バグ修正（バグを再現するテストを先に書く）
- リファクタリング
- ビジネスクリティカルなロジック

## C# / xUnit の実装フロー

```
1. インターフェース・DTOを定義
   → IExpenseService, CreateExpenseRequest, ExpenseDto

2. 失敗するテストを書く
   → ExpenseServiceTests.cs に Red テスト追加

3. コンパイルエラーを解消（空実装）
   → throw new NotImplementedException()

4. テストを通す最小実装
   → ExpenseService.CreateExpenseAsync() を実装

5. テスト実行 → GREEN確認
   → dotnet test --filter "ClassName=ExpenseServiceTests"

6. リファクタリング
   → テストを通したまま内部実装を改善
```

## カバレッジ基準

| コード種別 | 最低カバレッジ |
|-----------|--------------|
| 一般的なコード | 80% |
| 金額計算ロジック | 100% |
| 認証・認可 | 100% |
| CSVパーサー | 100% |

## FinFlow での適用例

```
/tdd ExpenseService.CreateExpenseAsync を実装
/tdd CsvParserFactory のパーサー選択ロジック
/tdd 月次集計レポートの計算
```
