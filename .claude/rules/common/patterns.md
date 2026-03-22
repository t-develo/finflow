# Architecture Patterns — FinFlow

## レイヤードアーキテクチャ

```
Controller → Service → Repository (EF Core) → Database
```

- **Controller**: 薄く保つ。ロジックはServiceに委譲
- **Service**: ビジネスロジック。`IService` インターフェースを実装
- **Domain Entities**: EF Core依存なし。純粋なドメインモデル

## リポジトリパターン

EF Coreを直接Serviceから呼ぶのではなく、必要に応じてリポジトリ経由にする:

```csharp
public interface IExpenseRepository
{
    Task<IEnumerable<Expense>> GetByUserIdAsync(string userId);
    Task<Expense?> GetByIdAsync(int id, string userId);
    Task<Expense> CreateAsync(Expense expense);
    Task<Expense> UpdateAsync(Expense expense);
    Task DeleteAsync(int id, string userId);
}
```

## CSVパーサーのアダプターパターン

```
CsvParserFactory → ICsvParser
                    ├── GenericCsvParser
                    ├── MufgCsvParser
                    └── RakutenCsvParser
```

`CsvParserFactory` はCSVのヘッダー行を検査してパーサーを選択する。

## APIレスポンス形式

```json
// 成功時
{ "data": { ... } }

// エラー時（GlobalErrorHandlingMiddleware が統一管理）
{ "error": "エラーメッセージ", "statusCode": 400 }
```

## フロントエンドのWeb Componentsパターン

```javascript
class FfExpenseForm extends HTMLElement {
    connectedCallback() { this.render(); }

    render() {
        this.innerHTML = `<form>...</form>`;
        this.attachEventListeners();
    }
}
customElements.define('ff-expense-form', FfExpenseForm);
```

## SE担当境界

| 担当 | 範囲 |
|------|------|
| SE-1 | Expense CRUD, Category CRUD, CSV取込, 自動分類 |
| SE-2 | Subscription, レポート集計, 通知, PDF生成 |
| SE-3 | 全フロントエンド（SPA, UIコンポーネント, ページ） |
| PL   | 認証基盤, 共通インフラ, OpenAPI仕様 |

`Expense`/`Category` エンティティ変更時はSE-1とSE-2で調整が必要。
