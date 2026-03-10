# Webアプリセキュリティ原則【汎用】

マルチテナントWebアプリ開発で必須のセキュリティパターン。

---

## 1. ユーザーデータの分離（マルチテナント必須）

**全てのデータアクセスクエリに認証ユーザーIDフィルタを含める。**
抜けると他ユーザーのデータが見える重大なセキュリティインシデントになる。

```csharp
// BAD: フィルタなし（他ユーザーのデータが取れてしまう）
var records = await _db.Records.ToListAsync();

// GOOD: 必ず認証ユーザーIDでフィルタ
var records = await _db.Records
    .Where(r => r.UserId == currentUserId)
    .ToListAsync();
```

### 水平アクセス制御（IDOR防止）

IDを指定するAPI（GET /items/{id}）では、取得後に所有者チェックを必ず行う。

```csharp
var item = await _db.Items.FindAsync(id);
if (item == null || item.UserId != currentUserId)
    throw new NotFoundException();
```

---

## 2. 金額計算の精度（decimal型）

**金額・価格・レートには必ず `decimal` 型を使用する。`float`/`double` は禁止。**

```csharp
// BAD: float/doubleは丸め誤差が蓄積する
double total = items.Sum(i => (double)i.Price); // 誤差が蓄積

// GOOD: decimalは10進数を正確に表現できる
decimal total = items.Sum(i => i.Price); // 正確

// 丸め処理
decimal pct = Math.Round(value, 1, MidpointRounding.AwayFromZero);

// ゼロ除算防止
decimal avg = count > 0 ? total / count : 0m;
```

---

## 3. XSS（クロスサイトスクリプティング）防止

**`innerHTML` にユーザー入力を直接埋め込まない。**

```javascript
// BAD: XSS脆弱性
element.innerHTML = `<span>${userInput}</span>`;

// GOOD: 必ずエスケープ
element.innerHTML = `<span>${escapeHtml(userInput)}</span>`;

// エスケープ関数
function escapeHtml(text) {
    const div = document.createElement('div');
    div.textContent = text;
    return div.innerHTML;
}

// eval()は絶対禁止
eval(userInput); // NG
```

---

## 4. SQLインジェクション防止

- ORMのクエリビルダー（LINQ等）を使用する。生SQLは原則禁止
- 生SQL使用が必要な場合はパラメータ化クエリを使用する

```csharp
// BAD: 文字列結合SQL
var sql = $"SELECT * FROM Items WHERE Name = '{name}'";

// GOOD: パラメータ化（ORMを使う場合は自動的に安全）
var items = await _db.Items.Where(i => i.Name == name).ToListAsync();
```

---

## 5. 認証・認可

| 項目 | 対策 |
|------|------|
| パスワード保管 | bcrypt/Argon2等でハッシュ化（平文保管禁止） |
| JWT | 適切な有効期限設定。署名アルゴリズムはHS256以上 |
| セッション | 固定化攻撃防止のためログイン後にセッションIDを再生成 |
| CSRF | State変更操作にCSRFトークンまたはSameSite Cookie |

---

## 6. その他の必須対策

| 項目 | 内容 |
|------|------|
| 入力バリデーション | サーバーサイド必須（クライアントバリデーションは信頼しない） |
| エラーメッセージ | スタックトレース・内部構造をAPIレスポンスに含めない |
| ファイルアップロード | ファイルサイズ・種別・件数の制限を設ける |
| 機密情報のログ | パスワード・トークン・個人情報をログやエラーメッセージに出さない |
| 二重送信防止 | 送信中はボタンを無効化 |

---

## セキュリティレビューチェックリスト（Must Fix判定）

```
□ 全データクエリに認証ユーザーIDフィルタがあるか
□ IDを指定するAPIで所有者チェックをしているか（IDOR防止）
□ 金額フィールドがdecimal型か（float/doubleは即Must Fix）
□ innerHTMLに未エスケープのユーザー入力が埋め込まれていないか
□ 生SQLでパラメータ化されていない箇所がないか
□ 500エラーのレスポンスにスタックトレースが含まれていないか
□ 機密情報がログに出力されていないか
```
