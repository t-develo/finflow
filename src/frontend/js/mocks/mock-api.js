/**
 * mock-api.js - Mock API layer for Sprint 1 development
 *
 * Simulates the real backend API responses with a configurable network delay.
 * Swap out by pointing api-client.js to the real BASE_URL in Sprint 2.
 *
 * All write operations mutate the in-memory store so changes are reflected
 * in subsequent read calls within the same browser session.
 */

import {
  mockCategories,
  mockExpenses,
  mockSubscriptions,
  mockDashboardSummary,
} from './mock-data.js';

/** Simulated network latency in milliseconds */
const MOCK_DELAY_MS = 300;

/** In-memory mutable stores (shallow copies from mock-data) */
const store = {
  categories: mockCategories.map(c => ({ ...c })),
  expenses: mockExpenses.map(e => ({ ...e })),
  subscriptions: mockSubscriptions.map(s => ({ ...s })),
  nextExpenseId: mockExpenses.length + 1,
  nextCategoryId: mockCategories.length + 1,
};

/** Simulate async network delay */
function delay(ms = MOCK_DELAY_MS) {
  return new Promise(resolve => setTimeout(resolve, ms));
}

/** Simulate JWT token issued at login */
function generateMockToken(user) {
  // Build a minimal JWT-like structure (base64 encoded, not signed)
  const header = btoa(JSON.stringify({ alg: 'none', typ: 'JWT' }));
  const exp = Math.floor(Date.now() / 1000) + 60 * 60 * 24; // 24 hours
  const payload = btoa(JSON.stringify({ sub: String(user.id), name: user.name, exp }));
  const signature = 'mock';
  return `${header}.${payload}.${signature}`;
}

// ---------------------------------------------------------------------------
// Auth
// ---------------------------------------------------------------------------

export const mockAuthApi = {
  async login({ email, password }) {
    await delay();
    // Accept any credentials in mock mode
    if (!email || !password) {
      throw { status: 400, message: 'メールアドレスとパスワードを入力してください' };
    }
    const user = { id: 1, name: email.split('@')[0], email };
    return {
      token: generateMockToken(user),
      user,
    };
  },

  async register({ name, email, password }) {
    await delay();
    if (!name || !email || !password) {
      throw { status: 400, message: '全ての必須フィールドを入力してください' };
    }
    return { message: '登録が完了しました。ログインしてください。' };
  },
};

// ---------------------------------------------------------------------------
// Categories
// ---------------------------------------------------------------------------

export const mockCategoriesApi = {
  async getAll() {
    await delay();
    return [...store.categories];
  },
};

// ---------------------------------------------------------------------------
// Expenses
// ---------------------------------------------------------------------------

export const mockExpensesApi = {
  async getList({ year, month, categoryId, page = 1, pageSize = 20 } = {}) {
    await delay();

    let items = [...store.expenses];

    // Filter by year/month
    if (year && month) {
      items = items.filter(e => {
        const d = new Date(e.date);
        return d.getFullYear() === Number(year) && d.getMonth() + 1 === Number(month);
      });
    }

    // Filter by category
    if (categoryId && String(categoryId) !== '0') {
      items = items.filter(e => e.categoryId === Number(categoryId));
    }

    // Sort: newest first
    items.sort((a, b) => new Date(b.date) - new Date(a.date));

    const total = items.length;
    const totalPages = Math.max(1, Math.ceil(total / pageSize));
    const offset = (page - 1) * pageSize;
    const pageItems = items.slice(offset, offset + pageSize);

    return {
      items: pageItems,
      total,
      page,
      pageSize,
      totalPages,
      totalAmount: items.reduce((sum, e) => sum + e.amount, 0),
    };
  },

  async getById(id) {
    await delay();
    const expense = store.expenses.find(e => e.id === Number(id));
    if (!expense) throw { status: 404, message: '支出が見つかりません' };
    return { ...expense };
  },

  async create({ categoryId, amount, description, date, note }) {
    await delay();
    const category = store.categories.find(c => c.id === Number(categoryId));
    const expense = {
      id: store.nextExpenseId++,
      categoryId: Number(categoryId),
      categoryName: category?.name || '',
      categoryColor: category?.color || '#6B7280',
      amount: Number(amount),
      description,
      date,
      note: note || '',
      createdAt: new Date().toISOString(),
    };
    store.expenses.unshift(expense);
    return { ...expense };
  },

  async update(id, { categoryId, amount, description, date, note }) {
    await delay();
    const index = store.expenses.findIndex(e => e.id === Number(id));
    if (index === -1) throw { status: 404, message: '支出が見つかりません' };

    const category = store.categories.find(c => c.id === Number(categoryId));
    const updated = {
      ...store.expenses[index],
      categoryId: Number(categoryId),
      categoryName: category?.name || store.expenses[index].categoryName,
      categoryColor: category?.color || store.expenses[index].categoryColor,
      amount: Number(amount),
      description,
      date,
      note: note || '',
    };
    store.expenses[index] = updated;
    return { ...updated };
  },

  async remove(id) {
    await delay();
    const index = store.expenses.findIndex(e => e.id === Number(id));
    if (index === -1) throw { status: 404, message: '支出が見つかりません' };
    store.expenses.splice(index, 1);
    return null;
  },
};

// ---------------------------------------------------------------------------
// Subscriptions
// ---------------------------------------------------------------------------

export const mockSubscriptionsApi = {
  async getAll() {
    await delay();
    return [...store.subscriptions];
  },
};

// ---------------------------------------------------------------------------
// Dashboard
// ---------------------------------------------------------------------------

export const mockDashboardApi = {
  async getSummary() {
    await delay();
    return { ...mockDashboardSummary };
  },
};
