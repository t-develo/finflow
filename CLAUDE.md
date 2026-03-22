# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Plugin: everything-claude-code

このプロジェクトは [everything-claude-code](https://github.com/affaan-m/everything-claude-code) プラグインを使用します。
専門エージェント・スキル・セッション永続化フックが含まれています。

### インストール方法（初回のみ）

Claude Code のチャットで以下を実行:

```
/plugin marketplace add affaan-m/everything-claude-code
/plugin install everything-claude-code@everything-claude-code
```

### セッション継続（iOS対応）

**`SESSION_STATE.md`** がセッション間のコンテキスト引き継ぎファイルです。

- **セッション開始時**: `SessionStart` フックが自動的に `SESSION_STATE.md` を表示します
- **作業中**: 重要な決定・進捗があれば `SESSION_STATE.md` を更新してください
- **セッション終了前**: 現在の状態を `SESSION_STATE.md` に記録してから終了してください

```bash
# 現在の作業状態を確認
cat SESSION_STATE.md
```

## Project Overview

**FinFlow** is a household/subscription management app.
- **Backend:** C# (.NET 8), ASP.NET Core Web API, Entity Framework Core, SQL Server
- **Frontend:** Vanilla JS (ES2020+, no framework, no build tools), Web Components, Chart.js
- **Testing:** xUnit + FluentAssertions
- **Auth:** ASP.NET Identity + JWT

## Build & Run Commands

```bash
# Build the solution
dotnet build

# Run the API
dotnet run --project src/FinFlow.Api

# Run all tests
dotnet test

# Run a single test class
dotnet test --filter "ClassName=ExpensesControllerTests"

# Run a single test method
dotnet test --filter "FullyQualifiedName~ExpensesControllerTests.MethodName"

# Create a new EF Core migration
dotnet ef migrations add <MigrationName> --project src/FinFlow.Infrastructure --startup-project src/FinFlow.Api

# Apply migrations
dotnet ef database update --project src/FinFlow.Infrastructure --startup-project src/FinFlow.Api
```

The frontend is served as static files (no build step). Open `src/frontend/index.html` directly in a browser or via the API's static file hosting.

## Solution Structure

```
src/
├── FinFlow.Api/             # ASP.NET Core Web API (controllers, Program.cs, middleware)
├── FinFlow.Domain/          # Entities, interfaces, domain logic (no EF/infra dependencies)
└── FinFlow.Infrastructure/  # EF Core DbContext, repositories, services, migrations
src/frontend/               # Vanilla JS SPA (no build step, ES Modules)
tests/
└── FinFlow.Tests/           # xUnit tests for all layers
docs/wbs/                   # Project WBS, milestones, and SE task instructions
```

## Backend Architecture

**Layered architecture:**
- **Controllers** (`FinFlow.Api/Controllers/`) — thin; delegate all logic to services
- **Services** (`FinFlow.Infrastructure/Services/`) — business logic
- **Entities** (`FinFlow.Domain/Entities/`) — EF Core entity classes
- **Interfaces** (`FinFlow.Domain/Interfaces/`) — service/repository contracts

**Key patterns:**
- Repository pattern via EF Core
- All async methods use the `Async` suffix
- JWT user ID is extracted from claims for per-user data isolation
- Global error handling middleware handles 400/404/500 responses uniformly

**Main API routes:**
- `POST/GET/PUT/DELETE /api/expenses` — expense CRUD
- `POST/GET/PUT/DELETE /api/categories` — category master CRUD
- `POST/GET/PUT/DELETE /api/subscriptions` — subscription CRUD
- `GET /api/reports/monthly` — monthly expense summary
- `GET /api/reports/by-category` — category breakdown
- `GET /api/dashboard/summary` — dashboard aggregate
- `POST /api/expenses/import` — CSV bulk import
- `GET /api/reports/monthly/pdf` — PDF report download
- `POST /api/auth/login`, `POST /api/auth/register` — authentication

## Frontend Architecture

- **Entry point:** `src/frontend/index.html` → `js/app.js`
- **Routing:** Navigo (or custom) in `js/router.js`; unauthenticated users redirect to `/login`
- **Components:** Web Components with `ff-` prefix (e.g., `<ff-expense-form>`)
- **Pages:** `js/pages/*.js` — one file per route
- **API communication:** all requests go through `js/utils/api-client.js` (JWT auto-attached)
- **Auth state:** JWT stored in `localStorage`, managed by `js/utils/auth.js`
- **Mocks:** `js/mocks/` — used during Sprint 1 before real API is available

## Coding Conventions

**C#:**
- `PascalCase` for public members; `camelCase` for private fields
- Async method suffix: `GetExpensesAsync`, not `GetExpenses`
- Naming: `ICsvParser` (interface), `GenericCsvParser` (implementation), `CsvParserFactory` (factory)

**JavaScript:**
- File names: `kebab-case.js`
- Classes: `PascalCase`; methods/variables: `camelCase`
- CSS classes: BEM notation (`.expense-form__input--error`)

## CSV Import Design

The CSV import uses an adapter pattern to handle different bank formats:
- `ICsvParser` — interface in `FinFlow.Domain/Interfaces/`
- `GenericCsvParser` — generic format (date, description, amount, category)
- `MufgCsvParser`, `RakutenCsvParser` — bank-specific adapters (Sprint 2)
- `CsvParserFactory` — selects parser by inspecting the CSV header line
- Library: **CsvHelper**; encoding: UTF-8 and Shift_JIS; max 10,000 rows; error rows are skipped (not fatal)

## Notification & Background Services

- `NotificationScheduler` — `IHostedService` that detects subscriptions due within 3 days
- `EmailSender` — `IEmailSender` implementation (SMTP or SendGrid); dev uses MailHog mock SMTP

## PDF Reports

Use **QuestPDF** or **iText7** for `GET /api/reports/monthly/pdf`. Library selection should be confirmed in Sprint 1 before SE-B starts Sprint 2 work.

## SE Responsibility Boundaries

| Area | Owner |
|------|-------|
| Expense CRUD, Categories, CSV parsing, Auto-classification | SE-A |
| Subscriptions, Reports, Notifications, PDF generation | SE-B |
| All frontend (SPA, UI components, pages) | SE-C |
| Auth foundation, shared infrastructure, OpenAPI spec | PL |

When modifying `Expense` or `Category` entities, coordinate between SE-A and SE-B since SE-B reads these tables for aggregation.
