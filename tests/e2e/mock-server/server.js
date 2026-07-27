/**
 * server.js — フロントエンド E2E 用のモック API サーバー（テスト専用）
 *
 * ------------------------------------------------------------------
 * なぜ必要か
 * ------------------------------------------------------------------
 * 既定の playwright.config.js は `dotnet run` で本物の API を起動する。
 * それが使えるならそちらが正しい。しかし
 *   - .NET SDK が入っていない環境（クラウド上の作業コンテナ等）
 *   - フロントエンドの挙動だけを速く回したいとき
 * では起動できず、フロントの回帰テストがまったく実行できなくなる。
 *
 * このサーバーは `src/frontend` を静的配信し、`/api/*` をインメモリで
 * 応答するだけの薄いスタブ。**アプリのビルドには一切関与しない**
 * （CLAUDE.md の「フロントエンドにビルドステップを持たない」方針に抵触しない）。
 *
 * ------------------------------------------------------------------
 * 本物のサーバーと意図的に揃えている点
 * ------------------------------------------------------------------
 *  - 静的ファイルに `Cache-Control: no-cache, must-revalidate` を付ける
 *    （Program.cs と同じ。これが無いと E2E とラズパイ実機で挙動が変わる）
 *  - SPA フォールバック（未知のパスは index.html を返す）
 *  - エラー応答の形は `{ error, statusCode }`（GlobalExceptionMiddleware と同じ。
 *    api-client.js が `err.error` を読む）
 *  - 認証は JWT の**形**だけ真似る（auth.js が payload.exp を見るため）。
 *    署名は検証しない — ここはフロントの検証用であって認証の検証用ではない。
 *
 * 使い方:
 *   node tests/e2e/mock-server/server.js [port]
 *   npm run test:e2e:mock
 */

'use strict';

const http = require('http');
const fs = require('fs');
const path = require('path');

const FRONTEND_ROOT = path.resolve(__dirname, '../../../src/frontend');
const PORT = Number(process.argv[2] || process.env.MOCK_PORT || 5212);

const MIME_TYPES = {
  '.html': 'text/html; charset=utf-8',
  '.js': 'text/javascript; charset=utf-8',
  '.css': 'text/css; charset=utf-8',
  '.json': 'application/json; charset=utf-8',
  '.svg': 'image/svg+xml',
  '.png': 'image/png',
  '.ico': 'image/x-icon',
};

// ---------------------------------------------------------------------------
// インメモリのデータストア（ユーザー単位）
// ---------------------------------------------------------------------------

/**
 * 本物の API は JWT のクレームから UserId を取り出して必ずフィルタする
 * （.claude/rules/csharp/security.md の必須パターン）。
 * ここでも**ユーザー単位に分離する**。共有ストアにすると、
 * 一意のユーザーを毎回作る E2E ヘルパーの前提が崩れ、前のテストの
 * データが次のテストに漏れて件数アサーションが不安定になる。
 */
function createUserStore() {
  return {
    nextId: { subscription: 1, category: 100, expense: 1 },
    subscriptions: [],
    categories: [
      { id: 1, name: '食費', color: '#EF4444', isSystem: true },
      { id: 2, name: '交通費', color: '#3B82F6', isSystem: true },
      { id: 3, name: '娯楽', color: '#8B5CF6', isSystem: true },
      { id: 4, name: '光熱費', color: '#F59E0B', isSystem: true },
      { id: 5, name: 'サブスク', color: '#10B981', isSystem: true },
      { id: 101, name: 'カフェ', color: '#EC4899', isSystem: false },
    ],
    expenses: [],
  };
}

/** userId → ストア。 */
const stores = new Map();

function storeFor(userId) {
  if (!stores.has(userId)) stores.set(userId, createUserStore());
  return stores.get(userId);
}

/**
 * Authorization ヘッダーの JWT（形だけのもの）から sub を取り出す。
 * 署名は検証しない — これはフロント検証用のスタブであって、
 * 認証機構の検証用ではない。
 */
function userIdFrom(req) {
  const header = req.headers['authorization'] || '';
  const token = header.startsWith('Bearer ') ? header.slice(7) : '';
  const payload = token.split('.')[1];
  if (!payload) return 'anonymous';
  try {
    return JSON.parse(Buffer.from(payload, 'base64url').toString('utf8')).sub || 'anonymous';
  } catch {
    return 'anonymous';
  }
}

// ---------------------------------------------------------------------------
// ヘルパー
// ---------------------------------------------------------------------------

function sendJson(res, statusCode, payload) {
  const body = JSON.stringify(payload);
  res.writeHead(statusCode, {
    'Content-Type': 'application/json; charset=utf-8',
    'Content-Length': Buffer.byteLength(body),
    'Cache-Control': 'no-store',
  });
  res.end(body);
}

/** GlobalExceptionMiddleware と同じ形。api-client.js が err.error を読む。 */
function sendError(res, statusCode, message) {
  sendJson(res, statusCode, { error: message, statusCode });
}

function sendNoContent(res) {
  res.writeHead(204);
  res.end();
}

function readBody(req) {
  return new Promise((resolve, reject) => {
    let raw = '';
    req.on('data', (chunk) => {
      raw += chunk;
      // 悪意ではなく事故（無限ループ等）への保険
      if (raw.length > 5_000_000) reject(new Error('body too large'));
    });
    req.on('end', () => {
      if (!raw) return resolve({});
      try {
        resolve(JSON.parse(raw));
      } catch {
        resolve({});
      }
    });
    req.on('error', reject);
  });
}

/** multipart 等、JSON でないボディを生テキストとして読む。 */
function readRawBody(req) {
  return new Promise((resolve, reject) => {
    let raw = '';
    req.setEncoding('utf8');
    req.on('data', (chunk) => {
      raw += chunk;
      if (raw.length > 20_000_000) reject(new Error('body too large'));
    });
    req.on('end', () => resolve(raw));
    req.on('error', reject);
  });
}

/**
 * auth.js が `JSON.parse(atob(token.split('.')[1])).exp` を見るので、
 * 3 分割・payload が base64url の JSON という**形**だけ満たす。
 */
function makeFakeJwt(email) {
  const encode = (obj) => Buffer.from(JSON.stringify(obj)).toString('base64url');
  const header = encode({ alg: 'none', typ: 'JWT' });
  const payload = encode({
    // 本物は Identity のユーザー ID。ここでは E2E ヘルパーが毎回作る
    // 一意なメールアドレスをそのまま使い、ユーザー間の分離を再現する。
    sub: `mock-user:${email}`,
    email,
    exp: Math.floor(Date.now() / 1000) + 60 * 60 * 8,
  });
  return `${header}.${payload}.mock-signature`;
}

function toIsoDate(date) {
  const y = date.getFullYear();
  const m = String(date.getMonth() + 1).padStart(2, '0');
  const d = String(date.getDate()).padStart(2, '0');
  return `${y}-${m}-${d}`;
}

// ---------------------------------------------------------------------------
// API ルーティング
// ---------------------------------------------------------------------------

async function handleApi(req, res, url) {
  const { pathname, searchParams } = url;
  const method = req.method;

  // --- テスト用フック -------------------------------------------------
  if (pathname === '/api/__test__/reset' && method === 'POST') {
    stores.clear();
    return sendJson(res, 200, { ok: true });
  }

  // 以降のハンドラは、この呼び出し元ユーザーのストアだけを触る。
  const store = storeFor(userIdFrom(req));

  // --- 認証 -----------------------------------------------------------
  if (pathname === '/api/auth/login' || pathname === '/api/auth/register') {
    const body = await readBody(req);
    const email = body.email || 'mock@example.com';
    return sendJson(res, 200, {
      token: makeFakeJwt(email),
      user: { id: `mock-user:${email}`, email, displayName: body.displayName || 'テストユーザー' },
    });
  }

  // --- サブスクリプション ---------------------------------------------
  if (pathname === '/api/subscriptions') {
    if (method === 'GET') {
      const sorted = [...store.subscriptions].sort(
        (a, b) => new Date(a.nextBillingDate) - new Date(b.nextBillingDate)
      );
      return sendJson(res, 200, sorted);
    }

    if (method === 'POST') {
      const body = await readBody(req);
      const serviceName = String(body.serviceName ?? '').trim();

      if (!serviceName) return sendError(res, 400, 'サービス名を入力してください。');

      // 本物の SubscriptionService と同じ重複チェック（409）。
      // フロントの二重送信ガードをすり抜けた場合の最後の砦なので、
      // モック側でも同じ挙動にしておかないと E2E の意味が薄れる。
      const duplicate = store.subscriptions.some(
        (s) => s.serviceName.trim().toLowerCase() === serviceName.toLowerCase()
      );
      if (duplicate) {
        return sendError(res, 409, `サブスクリプション「${serviceName}」は既に登録されています。`);
      }

      const created = {
        id: store.nextId.subscription++,
        serviceName,
        amount: Number(body.amount ?? 0),
        billingCycle: body.billingCycle ?? 'monthly',
        nextBillingDate: body.nextBillingDate ?? toIsoDate(new Date()),
        notes: body.notes ?? '',
        isActive: body.isActive !== false,
        categoryId: body.categoryId ?? null,
      };
      store.subscriptions.push(created);
      return sendJson(res, 201, created);
    }
  }

  const subMatch = pathname.match(/^\/api\/subscriptions\/(\d+)$/);
  if (subMatch) {
    const id = Number(subMatch[1]);
    const index = store.subscriptions.findIndex((s) => s.id === id);
    if (index === -1) return sendError(res, 404, `Subscription ${id} not found`);

    if (method === 'GET') return sendJson(res, 200, store.subscriptions[index]);

    if (method === 'PUT') {
      const body = await readBody(req);
      const serviceName = String(body.serviceName ?? '').trim();

      const duplicate = store.subscriptions.some(
        (s) => s.id !== id && s.serviceName.trim().toLowerCase() === serviceName.toLowerCase()
      );
      if (duplicate) {
        return sendError(res, 409, `サブスクリプション「${serviceName}」は既に登録されています。`);
      }

      store.subscriptions[index] = {
        ...store.subscriptions[index],
        serviceName,
        amount: Number(body.amount ?? 0),
        billingCycle: body.billingCycle ?? 'monthly',
        nextBillingDate: body.nextBillingDate,
        notes: body.notes ?? '',
        isActive: body.isActive !== false,
      };
      return sendJson(res, 200, store.subscriptions[index]);
    }

    if (method === 'DELETE') {
      store.subscriptions.splice(index, 1);
      return sendNoContent(res);
    }
  }

  // --- カテゴリ -------------------------------------------------------
  if (pathname === '/api/categories') {
    if (method === 'GET') return sendJson(res, 200, store.categories);

    if (method === 'POST') {
      const body = await readBody(req);
      const name = String(body.name ?? '').trim();
      if (!name) return sendError(res, 400, 'カテゴリ名を入力してください。');
      if (store.categories.some((c) => c.name === name)) {
        return sendError(res, 409, `カテゴリ「${name}」は既に存在します。`);
      }
      const created = {
        id: store.nextId.category++,
        name,
        color: body.color ?? '#3B82F6',
        isSystem: false,
      };
      store.categories.push(created);
      return sendJson(res, 201, created);
    }
  }

  const catMatch = pathname.match(/^\/api\/categories\/(\d+)$/);
  if (catMatch) {
    const id = Number(catMatch[1]);
    const index = store.categories.findIndex((c) => c.id === id);
    if (index === -1) return sendError(res, 404, `Category ${id} not found`);

    if (method === 'PUT') {
      const body = await readBody(req);
      store.categories[index] = {
        ...store.categories[index],
        name: String(body.name ?? '').trim() || store.categories[index].name,
        color: body.color ?? store.categories[index].color,
      };
      return sendJson(res, 200, store.categories[index]);
    }

    if (method === 'DELETE') {
      if (store.categories[index].isSystem) {
        return sendError(res, 400, 'システムカテゴリは削除できません。');
      }
      store.categories.splice(index, 1);
      return sendNoContent(res);
    }
  }

  // --- 支出 -----------------------------------------------------------
  if (pathname === '/api/expenses') {
    if (method === 'GET') {
      const from = searchParams.get('from');
      const to = searchParams.get('to');
      const categoryId = searchParams.get('categoryId');
      const page = Number(searchParams.get('page') || 1);
      const pageSize = Number(searchParams.get('pageSize') || 20);

      let filtered = [...store.expenses];
      if (from) filtered = filtered.filter((e) => e.date >= from);
      if (to) filtered = filtered.filter((e) => e.date <= to);
      if (categoryId && categoryId !== '0') {
        filtered = filtered.filter((e) => String(e.categoryId) === String(categoryId));
      }
      filtered.sort((a, b) => (a.date < b.date ? 1 : -1));

      const start = (page - 1) * pageSize;
      const pageItems = filtered.slice(start, start + pageSize);

      return sendJson(res, 200, {
        data: pageItems.map((e) => withCategoryInfo(store, e)),
        pagination: {
          page,
          pageSize,
          totalCount: filtered.length,
          hasNextPage: start + pageSize < filtered.length,
        },
        totalAmount: filtered.reduce((sum, e) => sum + e.amount, 0),
      });
    }

    if (method === 'POST') {
      const body = await readBody(req);
      const created = {
        id: store.nextId.expense++,
        amount: Number(body.amount ?? 0),
        description: body.description ?? '',
        date: body.date ?? toIsoDate(new Date()),
        categoryId: body.categoryId ?? null,
      };
      store.expenses.push(created);
      return sendJson(res, 201, withCategoryInfo(store, created));
    }
  }

  const expenseMatch = pathname.match(/^\/api\/expenses\/(\d+)$/);
  if (expenseMatch) {
    const id = Number(expenseMatch[1]);
    const index = store.expenses.findIndex((e) => e.id === id);
    if (index === -1) return sendError(res, 404, `Expense ${id} not found`);

    if (method === 'GET') return sendJson(res, 200, withCategoryInfo(store, store.expenses[index]));

    if (method === 'PUT') {
      const body = await readBody(req);
      store.expenses[index] = {
        ...store.expenses[index],
        amount: Number(body.amount ?? store.expenses[index].amount),
        description: body.description ?? store.expenses[index].description,
        date: body.date ?? store.expenses[index].date,
        categoryId: body.categoryId ?? store.expenses[index].categoryId,
      };
      return sendJson(res, 200, withCategoryInfo(store, store.expenses[index]));
    }

    if (method === 'DELETE') {
      store.expenses.splice(index, 1);
      return sendNoContent(res);
    }
  }

  // --- CSV 取込 -------------------------------------------------------
  // multipart/form-data を本気で解析はしない。ボディ全体をテキストとして
  // 読み、CSV に見える行（日付,説明,金額[,カテゴリ]）を拾う。
  // 目的は「取り込んだ支出が一覧に出るか」というフロントの導線検証であって、
  // パーサーの正しさ（CsvParserFactory）は xUnit 側の担当。
  if (pathname === '/api/expenses/import' && method === 'POST') {
    const raw = await readRawBody(req);
    const rows = raw
      .split(/\r?\n/)
      .map((line) => line.trim())
      .filter((line) => /^\d{4}[-/]\d{1,2}[-/]\d{1,2},/.test(line));

    const errors = [];
    let imported = 0;

    for (const row of rows) {
      const [date, description, amount, categoryName] = row.split(',');
      const parsedAmount = Number(String(amount ?? '').replace(/[^\d.-]/g, ''));
      if (!parsedAmount || Number.isNaN(parsedAmount)) {
        errors.push(`金額を解釈できません: ${row}`);
        continue;
      }
      const category = store.categories.find((c) => c.name === (categoryName ?? '').trim());
      store.expenses.push({
        id: store.nextId.expense++,
        amount: parsedAmount,
        description: (description ?? '').trim(),
        date: date.replace(/\//g, '-'),
        categoryId: category?.id ?? null,
      });
      imported += 1;
    }

    return sendJson(res, 200, { imported, skipped: errors.length, errors });
  }

  // --- ダッシュボード / レポート ---------------------------------------
  if (pathname === '/api/dashboard/summary' && method === 'GET') {
    const now = new Date();
    const thisMonth = `${now.getFullYear()}-${String(now.getMonth() + 1).padStart(2, '0')}`;
    const currentMonthTotal = store.expenses
      .filter((e) => e.date.startsWith(thisMonth))
      .reduce((sum, e) => sum + e.amount, 0);

    const prev = new Date(now.getFullYear(), now.getMonth() - 1, 1);
    const prevMonth = `${prev.getFullYear()}-${String(prev.getMonth() + 1).padStart(2, '0')}`;
    const previousMonthTotal = store.expenses
      .filter((e) => e.date.startsWith(prevMonth))
      .reduce((sum, e) => sum + e.amount, 0);

    const change = previousMonthTotal === 0
      ? 0
      : ((currentMonthTotal - previousMonthTotal) / previousMonthTotal) * 100;

    return sendJson(res, 200, {
      currentMonthTotal,
      previousMonthTotal,
      monthOverMonthChange: Number(change.toFixed(1)),
      recentExpenses: [...store.expenses]
        .sort((a, b) => (a.date < b.date ? 1 : -1))
        .slice(0, 5)
        .map((e) => withCategoryInfo(store, e)),
    });
  }

  if (pathname === '/api/reports/by-category' && method === 'GET') {
    const totals = new Map();
    for (const expense of store.expenses) {
      const category = store.categories.find((c) => c.id === expense.categoryId);
      const key = category?.id ?? 0;
      const entry = totals.get(key) ?? {
        categoryId: key,
        categoryName: category?.name ?? '未分類',
        categoryColor: category?.color ?? '#6B7280',
        totalAmount: 0,
      };
      entry.totalAmount += expense.amount;
      totals.set(key, entry);
    }
    const rows = [...totals.values()].sort((a, b) => b.totalAmount - a.totalAmount);
    const grandTotal = rows.reduce((sum, r) => sum + r.totalAmount, 0);
    // フロントのフォールバック表示が percentage を読むので必ず入れる。
    for (const row of rows) {
      row.percentage = grandTotal === 0 ? 0 : (row.totalAmount / grandTotal) * 100;
    }
    return sendJson(res, 200, rows);
  }

  if (pathname === '/api/reports/monthly' && method === 'GET') {
    return sendJson(res, 200, { items: [], totalAmount: 0 });
  }

  return sendError(res, 404, `No mock route for ${method} ${pathname}`);
}

function withCategoryInfo(store, expense) {
  const category = store.categories.find((c) => c.id === expense.categoryId);
  return {
    ...expense,
    categoryName: category?.name ?? '未分類',
    categoryColor: category?.color ?? '#6B7280',
  };
}

// ---------------------------------------------------------------------------
// 静的ファイル配信（Program.cs と同じキャッシュ方針）
// ---------------------------------------------------------------------------

function serveStatic(res, filePath) {
  fs.readFile(filePath, (err, data) => {
    if (err) {
      res.writeHead(404, { 'Content-Type': 'text/plain; charset=utf-8' });
      res.end('Not found');
      return;
    }
    res.writeHead(200, {
      'Content-Type': MIME_TYPES[path.extname(filePath)] || 'application/octet-stream',
      // Program.cs と同じ。付け忘れるとテスト環境だけキャッシュ挙動が変わる。
      'Cache-Control': 'no-cache, must-revalidate',
      'Content-Length': data.length,
    });
    res.end(data);
  });
}

const server = http.createServer((req, res) => {
  const url = new URL(req.url, `http://localhost:${PORT}`);

  if (url.pathname.startsWith('/api/')) {
    handleApi(req, res, url).catch((err) => {
      sendError(res, 500, err.message || 'Mock server error');
    });
    return;
  }

  // パストラバーサル防止: 解決後のパスが配信ルートの外に出たら拒否する。
  const requested = path.normalize(path.join(FRONTEND_ROOT, url.pathname));
  if (!requested.startsWith(FRONTEND_ROOT)) {
    res.writeHead(403);
    res.end('Forbidden');
    return;
  }

  fs.stat(requested, (err, stats) => {
    if (!err && stats.isFile()) {
      serveStatic(res, requested);
      return;
    }
    // SPA フォールバック（MapFallbackToFile と同じ役割）
    serveStatic(res, path.join(FRONTEND_ROOT, 'index.html'));
  });
});

server.listen(PORT, '127.0.0.1', () => {
  console.log(`[mock-server] serving ${FRONTEND_ROOT} on http://127.0.0.1:${PORT}`);
});
