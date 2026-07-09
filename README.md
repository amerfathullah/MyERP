# MyERP

A full-featured, modular Enterprise Resource Planning system built with [ABP Framework](https://abp.io/) 10.5, .NET 10, Angular 21, and PostgreSQL. Designed for Malaysian businesses with built-in LHDN e-Invoice (MyInvois) integration, SST tax engine, and payroll compliance (EPF, SOCSO, EIS, PCB).

[![CI](https://github.com/your-org/myerp/actions/workflows/ci.yml/badge.svg)](https://github.com/your-org/myerp/actions/workflows/ci.yml)

---

## Features

### Core ERP
- **Multi-company & Multi-branch** — full tenant isolation with per-company settings
- **Chart of Accounts** — hierarchical tree structure with 5 account types
- **Double-Entry Accounting** — enforced at domain level; every transaction produces balanced journal entries
- **Document Workflow** — configurable state machine: Draft → Submitted → Approved → Posted → Cancelled
- **Configurable Rules Engine** — tax rates, accounting rules, and contribution tables are data-driven (never hardcoded)

### Business Modules

| Module | Capabilities |
|--------|-------------|
| **Accounting** | General ledger, journal entries, payment entries, bank reconciliation, Trial Balance / P&L / Balance Sheet reports |
| **Sales** | Quotations → Sales Orders → Delivery Notes → Sales Invoices, with auto-conversion flow |
| **Purchasing** | Purchase Orders → Purchase Receipts → Purchase Invoices, with auto-conversion flow |
| **Inventory** | Items, warehouses, stock entries, stock ledger, weighted-average valuation |
| **Tax** | Configurable tax categories & rules, SST support, date-range effective rates |
| **HR & Payroll** | Employee management, automated payroll (EPF/SOCSO/EIS/PCB), PDPA field-level security |
| **CRM** | Lead lifecycle management, opportunity pipeline, conversion to customer |
| **Projects** | Project & task management, dependencies, 4 progress calculation methods |
| **Fixed Assets** | Asset categories, 3 depreciation methods (SL/DDB/WDV), sale/scrap lifecycle |
| **Manufacturing** | Bills of Material, work orders, production tracking, material consumption |
| **E-Invoice** | LHDN MyInvois integration (submit, validate, cancel), XAdES digital signing, dashboard |

### Enterprise Features
- **Approval Workflows** — configurable multi-level approvals with amount thresholds
- **Automation Rules** — event-triggered actions (email, field updates, status changes)
- **Notifications** — in-app notification system with bell widget
- **Import/Export** — CSV import (customers, items) and export
- **POS** — Point of Sale interface for quick invoicing
- **Audit Logging** — full audit trail on all entities (ABP built-in)

---

## Tech Stack

| Layer | Technology |
|-------|-----------|
| Runtime | .NET 10, C# 13 |
| Framework | ABP.IO 10.5 (DDD, multi-tenancy, permissions, audit) |
| Database | PostgreSQL 16 + Entity Framework Core |
| Cache | Redis 7 |
| Auth | OpenIddict (OAuth 2.0 / OIDC) |
| Frontend | Angular 21, NgRx SignalStore, Bootstrap 5 (LeptonX Lite) |
| Charts | Chart.js |
| E2E Tests | Playwright |
| CI/CD | GitHub Actions |
| Deployment | Docker Compose + Traefik (TLS) |
| Container Registry | Docker Hub (`amerfathullah/myerp-api`, `amerfathullah/myerp-web`, `amerfathullah/myerp-migrator`) |

---

## Self-Hosted Deployment (Docker Hub)

Deploy MyERP on any server with Docker — no build tools required.

### Quick Start (One Command)

```bash
mkdir myerp && cd myerp
curl -sL https://raw.githubusercontent.com/amerfathullah/erp/main/MyERP/deploy/docker-compose.yml -o docker-compose.yml
docker compose up -d
```

That's it. Open http://localhost — login with `admin` / `1q2w3E*`

### What Gets Deployed

| Container | Image | Purpose |
|-----------|-------|---------|
| `myerp-db` | `postgres:16-alpine` | PostgreSQL database |
| `myerp-redis` | `redis:7-alpine` | Cache & distributed events |
| `myerp-migrator` | `amerfathullah/myerp-migrator` | Runs DB migrations & seeds admin user (exits after) |
| `myerp-api` | `amerfathullah/myerp-api` | .NET 10 REST API + OAuth server |
| `myerp-web` | `amerfathullah/myerp-web` | Angular frontend on Nginx |

### Custom Configuration

```bash
# Download compose + env template
curl -sL https://raw.githubusercontent.com/amerfathullah/erp/main/MyERP/deploy/docker-compose.yml -o docker-compose.yml
curl -sL https://raw.githubusercontent.com/amerfathullah/erp/main/MyERP/deploy/.env.example -o .env

# Edit settings (database password, URLs, ports)
nano .env

# Start
docker compose up -d
```

Key `.env` settings:

| Variable | Default | Description |
|----------|---------|-------------|
| `DB_PASSWORD` | `myerp_secret_2026` | PostgreSQL password (change this!) |
| `APP_URL` | `http://localhost:5000` | Public API URL |
| `WEB_URL` | `http://localhost` | Public frontend URL |
| `HTTP_PORT` | `80` | Port for the web app |
| `MYERP_VERSION` | `latest` | Pin to a specific release tag |

### HTTPS (Custom Domain)

```bash
# Also download the HTTPS overlay
curl -sL https://raw.githubusercontent.com/amerfathullah/erp/main/MyERP/deploy/docker-compose.https.yml -o docker-compose.https.yml

# Set in .env:
# DOMAIN=myerp.yourdomain.com
# ACME_EMAIL=admin@yourdomain.com

docker compose -f docker-compose.yml -f docker-compose.https.yml up -d
```

Auto-provisions Let's Encrypt TLS certificates via Traefik.

### Update to Latest Version

```bash
docker compose pull
docker compose up -d
```

### Backup

```bash
docker compose exec db pg_dump -U myerp MyERP | gzip > backup_$(date +%Y%m%d).sql.gz
```

See [deploy/README.md](deploy/README.md) for full self-hosting documentation (scaling, restore, troubleshooting).

---

## Development Setup

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Node.js 22 LTS](https://nodejs.org/)
- [pnpm](https://pnpm.io/) (`corepack enable && corepack prepare pnpm@10 --activate`)
- [Docker](https://www.docker.com/) (for PostgreSQL + Redis)

---

## Quick Start

### 1. Start Infrastructure

```bash
docker compose up -d postgres redis
```

### 2. Run Database Migrations

```bash
cd src/MyERP.DbMigrator
dotnet run
```

### 3. Start the API

```bash
cd src/MyERP.HttpApi.Host
dotnet run
```

### 4. Start the Angular App

```bash
cd angular
pnpm install
pnpm start
```

### All-in-One (PowerShell)

```powershell
./setup-dev.ps1
```

---

## URLs

| Service | URL |
|---------|-----|
| Angular App | http://localhost:4200 |
| API | http://localhost:5000 |
| Swagger UI | http://localhost:5000/swagger |

**Default Login:** `admin` / `1q2w3E*`

---

## Docker (Full Stack)

```bash
docker compose up -d
```

Starts PostgreSQL, Redis, API, and Angular at `http://localhost:4200`.

---

## Project Structure

```
src/
├── MyERP.Domain.Shared        → Constants, enums, error codes, localization (21 languages)
├── MyERP.Domain               → Entities, domain services, repository interfaces, events
├── MyERP.Application.Contracts → DTOs, application service interfaces, permissions
├── MyERP.Application          → Application service implementations, AutoMapper profiles
├── MyERP.EntityFrameworkCore  → DbContext, migrations, repository implementations
├── MyERP.HttpApi              → API controllers
├── MyERP.HttpApi.Host         → Host application (startup, middleware, configuration)
├── MyERP.HttpApi.Client       → HTTP client proxies for service-to-service calls
└── MyERP.DbMigrator           → Database migration console app

angular/                       → Angular 21 SPA (standalone components, NgRx SignalStore)
├── src/app/
│   ├── accounting/            → Chart of Accounts, Journal Entries, Payments, Reports
│   ├── sales/                 → Quotations, Sales Orders, Delivery Notes, Invoices
│   ├── purchasing/            → Purchase Orders, Receipts, Invoices
│   ├── inventory/             → Items, Warehouses, Stock Entries, Stock Ledger
│   ├── e-invoice/             → LHDN Dashboard, Submission Logs
│   ├── hr/                    → Employees, Payroll
│   ├── crm/                   → Leads, Opportunities
│   ├── manufacturing/         → Work Orders
│   ├── projects/              → Projects & Tasks
│   ├── assets/                → Fixed Assets
│   └── shared/                → Shared components (status badges, workflow, child table)
├── e2e/                       → Playwright E2E tests

test/
├── MyERP.Domain.Tests         → 115+ unit tests (entities, domain services)
├── MyERP.Application.Tests    → Integration tests (app services, conversion flows)
└── MyERP.EntityFrameworkCore.Tests → Repository/query tests

docs/
├── architecture.md            → System architecture & module map
├── deployment.md              → Full deployment guide (local, Docker, production)
├── testing.md                 → Testing strategy & coverage
├── api-reference.md           → REST API endpoint reference
└── malaysia-compliance.md     → LHDN, SST, EPF/SOCSO/EIS, PDPA compliance
```

---

## Running Tests

### Backend

```bash
dotnet test
```

### Frontend Unit Tests

```bash
cd angular && pnpm test
```

### E2E Tests (Playwright)

```bash
cd angular
npx playwright install --with-deps
npx playwright test --config=e2e/playwright.config.ts
```

---

## Deployment

### Production (Docker Compose + Traefik)

```bash
# Copy and configure environment
cp .env.example .env
# Edit .env with production values

# Deploy
docker compose -f docker-compose.prod.yml pull
docker compose -f docker-compose.prod.yml run --rm migrator
docker compose -f docker-compose.prod.yml up -d
```

See [docs/deployment.md](docs/deployment.md) for full production setup guide.

### CI/CD

GitHub Actions workflows:
- **CI** (`ci.yml`): Build, lint, test on every push/PR
- **Deploy** (`deploy.yml`): Build images → push to GHCR → deploy (on tag or manual trigger)

---

## Malaysia Compliance

| Feature | Status |
|---------|--------|
| LHDN e-Invoice (MyInvois) | ✅ Submit, validate, cancel, dashboard |
| XAdES Digital Signing | ✅ RSA-SHA256, UBL Extension |
| SST Tax Engine | ✅ Configurable rates & rules |
| EPF Contribution | ✅ Data-driven (age/citizenship filters) |
| SOCSO Contribution | ✅ Data-driven (salary ceiling) |
| EIS Contribution | ✅ Data-driven |
| PCB/MTD | ✅ Graduated schedule |
| PDPA Compliance | ✅ Field-level security, audit logging |
| Malaysian CoA Template | ✅ Seeded |

See [docs/malaysia-compliance.md](docs/malaysia-compliance.md) for details.

---

## Documentation

| Document | Description |
|----------|-------------|
| [Architecture](docs/architecture.md) | System architecture, layers, module map, data flows |
| [Deployment](docs/deployment.md) | Local, Docker, and production deployment guide |
| [Testing](docs/testing.md) | Test strategy, coverage, patterns |
| [API Reference](docs/api-reference.md) | REST endpoint documentation |
| [Malaysia Compliance](docs/malaysia-compliance.md) | LHDN, SST, payroll, PDPA |

---

## Contributing

1. Create a feature branch from `develop`
2. Follow ABP DDD conventions (see `.github/copilot-instructions.md`)
3. Ensure all tests pass: `dotnet test`
4. Add tests for new domain logic
5. Update localization files (`en.json` + `ms-MY.json` at minimum)
6. Submit a PR to `develop`

---

## License

See [LICENSE](LICENSE) for details.
