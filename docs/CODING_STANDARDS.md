# FinFlow コーディング規約

## C# 規約

### 命名規則

| 対象 | ケース | 例 |
|------|--------|-----|
| クラス・インターフェース・列挙型 | PascalCase | `ExpenseService`, `IExpenseService` |
| パブリックメソッド・プロパティ | PascalCase | `GetExpensesAsync`, `Amount` |
| プライベートフィールド | camelCase with _ prefix | `_dbContext`, `_logger` |
| ローカル変数・パラメータ | camelCase | `expense`, `userId` |
| 非同期メソッド | Async suffix | `GetExpensesAsync` (**必須**) |

### インターフェース・実装・ファクトリの命名

```
ICsvParser          ← インターフェース（I プレフィックス）
GenericCsvParser    ← 実装クラス
CsvParserFactory    ← ファクトリクラス（Factory サフィックス）
```

### レイヤー規約

**Controllers（薄く保つ）:**
```csharp
[HttpGet]
public async Task<IActionResult> GetExpenses([FromQuery] ExpenseFilter filter)
{
    var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value!;
    var expenses = await _expenseService.GetExpensesAsync(userId, filter);
    return Ok(expenses);
}
```

**Services（ビジネスロジック）:**
- ユーザーIDによるデータ分離を必ず行う
- `ArgumentException` でバリデーションエラー
- `KeyNotFoundException` でリソース未存在エラー
- グローバルエラーハンドリングが 400/404/500 に変換

**Entities（副作用なし）:**
- EF CoreナビゲーションプロパティはNullableまたは初期値設定
- `CreatedAt`, `UpdatedAt` は SaveChanges 時に自動設定推奨

### 禁止事項

- コントローラー内でのDB操作（EF Core直接呼び出し）
- サービス内での HTTP 依存（HttpContext など）
- Entity から DTO への変換をコントローラー内で行うこと（Service 内で行う）

---

## JavaScript 規約

### 命名規則

| 対象 | ケース | 例 |
|------|--------|-----|
| ファイル名 | kebab-case | `expense-form.js`, `api-client.js` |
| クラス | PascalCase | `ExpenseForm`, `ApiClient` |
| 関数・変数 | camelCase | `getExpenses`, `currentUser` |
| CSS クラス | BEM | `.expense-form__input--error` |
| Web Components | ff- prefix | `<ff-expense-form>` |

### モジュール構成

```
js/
├── app.js              ← エントリポイント（ルーター初期化のみ）
├── router.js           ← ルーティング定義
├── pages/              ← ページコンポーネント（1ファイル1ページ）
│   ├── dashboard.js
│   ├── expenses.js
│   └── login.js
├── components/         ← 再利用可能Web Components
│   └── ff-expense-form.js
├── utils/
│   ├── api-client.js   ← fetchラッパー（JWT自動付与）
│   └── auth.js         ← JWT管理（localStorage）
└── mocks/              ← Sprint 1用モックデータ
```

### API通信

**必ず `api-client.js` を通すこと:**
```javascript
// Good
import { api } from '../utils/api-client.js';
const expenses = await api.get('/api/expenses');

// Bad（直接fetchは禁止）
const res = await fetch('/api/expenses', { headers: { ... } });
```

### 認証ガード

```javascript
// router.js - 未認証リダイレクト
router.on('/dashboard', () => {
    if (!auth.isAuthenticated()) {
        router.navigate('/login');
        return;
    }
    // ...
});
```

---

## Git 運用ルール

### コミット前チェック

1. `dotnet build` が通ること
2. `dotnet test` が全件パスすること
3. 新機能には最低1件のユニットテストを含めること

### PR作成基準

- 1 PR = 1 WBSタスク（例: S1-A-001）
- PRタイトル: `[SE-A] S1-A-001: 支出CRUD API実装`
- テスト結果（パス件数）をPR説明に記載
- PL レビューが必要なもの: API仕様変更、エンティティ変更、共通基盤変更

### ブランチ保護

- `main` ブランチへの直接pushは禁止
- PR のマージは PL 承認後のみ
