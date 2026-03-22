# JavaScript Security — FinFlow Frontend

## XSS防止（最重要）

```javascript
// NG: innerHTML に未サニタイズの値を代入
element.innerHTML = `<span>${userInput}</span>`;
element.innerHTML = expense.description;

// OK: textContent を使用（HTMLとして解釈されない）
element.textContent = expense.description;

// OK: DOM API で安全に構築
const span = document.createElement('span');
span.textContent = expense.description;
element.appendChild(span);

// OK: テンプレートリテラルを使う場合はサニタイズ必須
element.innerHTML = DOMPurify.sanitize(`<span>${expense.description}</span>`);
```

## JWT の安全な管理

```javascript
// localStorage に保存（XSS リスクあり、FinFlow では許容）
// NG: URL パラメータやログに JWT を含める
console.log('token:', auth.getToken()); // NG

// OK: 使用時のみ取得
const token = auth.getToken();
```

## APIリクエストの安全パターン

```javascript
// Content-Type を明示して CSRF対策
const response = await fetch('/api/expenses', {
    method: 'POST',
    headers: {
        'Content-Type': 'application/json',
        'Authorization': `Bearer ${auth.getToken()}`,
    },
    body: JSON.stringify(data),
});
```

## 入力バリデーション

```javascript
function validateExpenseInput(amount, description) {
    const errors = [];
    if (!amount || isNaN(amount) || Number(amount) <= 0) {
        errors.push('金額は0より大きい数値を入力してください');
    }
    if (!description?.trim()) {
        errors.push('説明は必須です');
    }
    if (description?.length > 500) {
        errors.push('説明は500文字以内で入力してください');
    }
    return errors;
}
```

## 機密情報の扱い

```javascript
// NG: コードにAPIキーやシークレットをハードコード
const API_KEY = 'sk-1234567890abcdef';

// OK: バックエンド経由でのみ外部APIを呼び出す
// フロントエンドには機密情報を持たせない
```
