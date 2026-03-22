# /e2e — End-to-End Test Scenarios

重要なユーザーフローのE2Eテストシナリオを定義・実行する。

## FinFlow の重要フロー

### 1. 認証フロー
```
ユーザー登録 → ログイン → JWTトークン取得 → 保護エンドポイントへのアクセス
```

### 2. 支出管理フロー
```
ログイン → 支出一覧表示 → 支出追加 → 支出編集 → 支出削除 → 一覧で確認
```

### 3. CSV取込フロー
```
ログイン → CSV取込ページ → ファイル選択 → プレビュー確認 → 取込実行 → 結果確認
```

### 4. レポートフロー
```
ログイン → ダッシュボード → 月次レポート表示 → カテゴリ別集計 → PDFダウンロード
```

## APIレベルのE2Eテスト（統合テスト）

```csharp
// WebApplicationFactory を使ったAPIレベルのE2E
public class ExpenseWorkflowTests : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public async Task FullExpenseWorkflow_CreateReadUpdateDelete_Succeeds()
    {
        // 1. ログイン → JWT取得
        var loginResponse = await _client.PostAsJsonAsync("/api/auth/login", credentials);
        var token = (await loginResponse.Content.ReadFromJsonAsync<LoginResponse>())!.Token;
        _client.DefaultRequestHeaders.Authorization = new("Bearer", token);

        // 2. 支出作成
        var createResponse = await _client.PostAsJsonAsync("/api/expenses", newExpense);
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        // 3. 一覧取得で確認
        var listResponse = await _client.GetAsync("/api/expenses");
        var expenses = await listResponse.Content.ReadFromJsonAsync<List<ExpenseDto>>();
        expenses.Should().Contain(e => e.Description == newExpense.Description);

        // 4. 削除
        // 5. 一覧で削除確認
    }
}
```

## テスト実行

```bash
# E2Eテストのみ実行（カテゴリでフィルタ）
dotnet test --filter "Category=E2E"

# 統合テスト実行
dotnet test --filter "Category=Integration"
```

## 注意事項

- E2Eテストは本番環境では実行しない（テスト用DBを使用）
- テストデータは各テストで独立して生成・削除する
- ユーザー分離: 別ユーザーのデータに誤ってアクセスできないことを確認
