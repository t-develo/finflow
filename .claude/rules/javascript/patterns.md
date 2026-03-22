# JavaScript Patterns — FinFlow Frontend

## SPA ルーティング

```
src/frontend/
├── index.html          # エントリーポイント
├── js/
│   ├── app.js          # アプリ初期化
│   ├── router.js       # SPA ルーター
│   ├── pages/          # ページコンポーネント（ルートごと）
│   │   ├── login.js
│   │   ├── dashboard.js
│   │   └── expenses.js
│   ├── components/     # Web Components (ff-プレフィックス)
│   ├── utils/
│   │   ├── api-client.js  # API通信（JWT自動付与）
│   │   └── auth.js        # 認証状態管理
│   └── mocks/          # Sprint 1 用モックデータ
└── css/
```

## 認証状態管理

```javascript
// js/utils/auth.js
export const auth = {
    getToken: () => localStorage.getItem('jwt_token'),
    setToken: (token) => localStorage.setItem('jwt_token', token),
    removeToken: () => localStorage.removeItem('jwt_token'),
    isAuthenticated: () => !!auth.getToken(),
};
```

## APIクライアントパターン

```javascript
// js/utils/api-client.js
const API_BASE = '/api';

export const apiClient = {
    async get(path) {
        const response = await fetch(`${API_BASE}${path}`, {
            headers: { 'Authorization': `Bearer ${auth.getToken()}` },
        });
        if (response.status === 401) { router.navigate('/login'); return; }
        if (!response.ok) throw new Error(await response.text());
        return response.json();
    },
    // post, put, delete...
};
```

## ページコンポーネントパターン

```javascript
// js/pages/expenses.js
export class ExpensesPage {
    async render(container) {
        container.innerHTML = '<ff-expense-list></ff-expense-list>';
        await container.querySelector('ff-expense-list').loadData();
    }

    cleanup() {
        // イベントリスナーの削除等
    }
}
```

## 未認証リダイレクト

```javascript
// router.js でガード
router.on('/expenses', () => {
    if (!auth.isAuthenticated()) {
        router.navigate('/login');
        return;
    }
    new ExpensesPage().render(mainContainer);
});
```

## Chart.js 利用パターン（ダッシュボード）

```javascript
import Chart from 'chart.js/auto';

const ctx = document.getElementById('expense-chart').getContext('2d');
new Chart(ctx, {
    type: 'doughnut',
    data: {
        labels: categories.map(c => c.name),
        datasets: [{ data: categories.map(c => c.total) }],
    },
});
```
