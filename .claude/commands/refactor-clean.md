# /refactor-clean — Dead Code Removal

未使用コード・重複コードを安全に削除する。

## 検出手順

```bash
# 未使用の using 文（C#）
dotnet build --verbosity normal 2>&1 | grep "CS8019"

# 到達不能コード警告
dotnet build 2>&1 | grep "CS0162\|CS0168\|CS0219"

# フロントエンド: 未使用インポート・変数
grep -rn "^import" src/frontend/js/ --include="*.js"
```

## 削除の安全手順

```
1. dotnet test（削除前にテストが通ることを確認）
2. コードを削除
3. dotnet test（テストが引き続き通ることを確認）
4. テストが失敗したら git restore で戻す
5. 成功したら次の削除へ
```

## 分類

| 分類 | 例 | 対応 |
|------|-----|------|
| **SAFE** | 未使用のprivateメソッド | 即削除可 |
| **CAUTION** | 未使用のpublicメソッド | 外部利用確認後に削除 |
| **DANGER** | 設定ファイル・インターフェース | 要調査、慎重に |

## 重要ルール

- **テストを実行せずに削除しない**
- 1つずつ削除（まとめて削除しない）
- 不確かな場合はスキップ
- リファクタリングと同時に行わない（別PRで対応）

## 近似重複の統合

80%以上の類似コードは統合を検討:
```csharp
// Before: ExpenseService と SubscriptionService に同じ日付フォーマット処理
// After: DateHelper.FormatJapaneseDate() として共通化
```
