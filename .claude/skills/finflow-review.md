# FinFlow レビュー固有チェック【FinFlow固有】

FinFlow固有のレビュー確認事項。
汎用的なレビュープロセス・観点・フォーマットは `/code-review` を参照。

---

## FinFlow固有の必須確認事項（全て Must Fix判定）

### 1. UserId分離（SE-1/SE-2）

**全Expense・Category・Subscriptionクエリで確認する。**

```csharp
// OK: UserIdフィルタあり
await _db.Expenses.Where(e => e.UserId == userId).ToListAsync()

// NG: フィルタなし（即Must Fix）
await _db.Expenses.ToListAsync()
```

IDを指定するAPI（`GET /expenses/{id}`等）では取得後の所有者チェックも確認する。

### 2. decimal型（SE-2）

集計・計算ロジックで `float`/`double` が使われていたら即Must Fix。

```csharp
decimal total = expenses.Sum(e => e.Amount); // OK
double total = ...; // NG → Must Fix
```

### 3. CSVインジェクション（SE-1）

CSVパーサー実装に `=`, `+`, `-`, `@` 始まりのセル値のサニタイズロジックがあるか確認する。

### 4. XSS（SE-3）

`innerHTML` への代入全箇所を確認する。`escapeHtml()` を経由しているか。

### 5. モック→実API切り替え確認（SE-3 Sprint 2）

`api-client.js` のベースURL変更だけで切り替わるか確認する。
各ページや各コンポーネントに直接 `fetch()` が書かれていないか。

---

## FinFlow固有の Should Fix 項目

| 対象 | 確認事項 |
|------|---------|
| SE-1/SE-2 | 読み取り専用クエリに `AsNoTracking()` があるか |
| SE-2 | 月末日計算に `DateTime.DaysInMonth(year, month)` を使っているか |
| SE-2 | ゼロ除算防止（`count > 0 ? total / count : 0m`）があるか |
| SE-3 | `console.log` がプロダクションコードに残っていないか |
| SE-3 | `ff-` プレフィックスでカスタム要素を定義しているか |
| SE-3 | `disconnectedCallback` でイベントリスナーを解除しているか |

---

## FinFlow固有のレビュー出力フォーマット

（汎用フォーマットは `/code-review` を参照。FinFlow向けに役割名を補足する）

```
## コードレビュー結果

### 対象
- 実装者: [SE-1 / SE-2 / SE-3 / PL]
- タスク: [タスクID] [タスク名]
- 対象ファイル: [ファイルパス一覧]

### 総合判定: [APPROVE / REQUEST_CHANGES]

### FinFlow固有チェック結果
- UserId分離: OK / NG（XXXクエリに漏れあり）
- decimal型: OK / NG（XXXの計算でdouble使用）
- CSVインジェクション: OK / NG / 対象外
- XSS防止: OK / NG / 対象外

### 良い点
- [具体的に良かった点]

### 指摘事項（Must Fix / Should Fix / Nit）
[汎用フォーマットに準拠]

### 学びの共有
[チーム全体に共有すべき知見]
```
