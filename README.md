# MyERP

A full-featured, modular Enterprise Resource Planning system built with [ABP Framework](https://abp.io/) 10.6, .NET 10, Angular 21, and PostgreSQL. Designed for Malaysian businesses with built-in LHDN e-Invoice (MyInvois) integration, SST tax engine, and payroll compliance (EPF, SOCSO, EIS, PCB).

[![CI](https://github.com/amerfathullah/myerp/actions/workflows/ci.yml/badge.svg)](https://github.com/amerfathullah/myerp/actions/workflows/ci.yml)

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
| **Accounting** | General ledger, journal entries, payment entries, bank reconciliation, budgets, period closing, exchange rate revaluation, currency exchange, accounting dimensions, fiscal years, finance books, payment orders, batch payments, invoice discounting, GL repost, payment ledger repost, ledger health monitor, share management (shareholders, types, transfers), cashier closing, bank clearance, bank guarantees, bisect statements, party links, cash flow forecast, Trial Balance / P&L / Balance Sheet / Cash Flow / Financial Ratios / Aging / Budget Variance reports, financial report templates |
| **Sales** | Quotations → Sales Orders → Delivery Notes → Sales Invoices, POS (opening/closing/profiles), blanket orders, pricing rules, promotional schemes, coupon codes, shipping rules, dunning, loyalty programs, subscriptions, installation notes, product bundles, packing slips, proforma invoices, shipments, sales partners, sales persons, territories, party-specific items, SO tracking board, gross profit report |
| **Purchasing** | Material Requests → RFQ → Supplier Quotation comparison → Purchase Orders → Purchase Receipts → Purchase Invoices, subcontracting (orders, BOMs, inward), supplier scorecards, scorecard variables, incoterms, procurement dashboard, PO tracking board |
| **Inventory** | Items, item groups, brands, manufacturers, item attributes, item alternatives, item prices, item standard cost, item lead times, customs tariff numbers, warehouses, bins, stock entries (13 purpose types), stock ledger (FIFO/Moving Average/LIFO), stock reconciliation (allow zero valuation rate), stock reservation, pick lists, putaway rules, landed costs, quality inspections, quality management (goals, reviews, procedures, meetings, feedback, non-conformances), batch management, serial numbers, UOM categories, delivery trips, transit transfers, inventory aging report, stock reorder, repost item valuation, shipment parcel templates, secondary item valuation type, stock closing |
| **Tax** | Configurable tax categories & rules, item tax templates, tax charges templates, tax withholding categories/groups, lower deduction certificates, SST support, date-range effective rates, SST-02 filing, tax summary report |
| **HR & Payroll** | Employees, departments, designations, employee groups, leave management (types, allocation, balance), attendance, shift assignments/types, holiday lists, expense claims, loans, salary components, salary structures, salary slips, payroll entry (EPF/SOCSO/EIS/PCB), PDPA field-level security |
| **CRM** | Leads, opportunities, pipeline view, campaigns, email campaigns, contracts, contract templates, appointments, prospects, competitors, market segments, industry types, sales stages, opportunity types, CRM settings |
| **Support** | Issues, issue types, issue priorities, service level agreements, support settings |
| **Projects** | Projects, project templates, project types, project updates, tasks, task types, activity types, activity costs, timesheets, timesheet billing |
| **Fixed Assets** | Asset categories, 3 depreciation methods (SL/DDB/WDV), asset repairs, capitalizations, asset movements, asset value adjustments, asset shift factors/allocations, asset maintenance logs/teams, locations, vehicles, drivers, driving license categories, sale/scrap lifecycle |
| **Manufacturing** | Bills of Material (explosion, phantom items, cycle detection), BOM creators, work orders, job cards, operations, routings, workstation types, workstations, production plans (MRP), master production schedules, production schedule, sales forecasts, material shortage summary, downtime entries, plant floors, manufacturing dashboard, manufacturing settings |
| **E-Invoice** | LHDN MyInvois integration (submit, validate, cancel), XAdES digital signing, dashboard, batch submit, submission logs, consolidation, reports, e-invoice settings |
| **Maintenance** | Maintenance schedules, maintenance visits, warranty claims |
| **Telephony** | Call logs, call types, telephony settings |
| **EDI** | Electronic data interchange code lists and common codes |

### Enterprise Features
- **Approval Workflows** — configurable multi-level approvals with amount thresholds and authorization rules
- **Automation Rules** — event-triggered actions (email, field updates, status changes)
- **Auto Repeat** — recurring document generation on schedule
- **Notifications** — in-app notification system with bell widget, email digest, and email templates
- **Import/Export** — CSV import (customers, items, suppliers) and export
- **POS** — Point of Sale interface with closing entries and consolidation
- **Audit Logging** — full audit trail on all entities (ABP built-in)
- **Bank Statement Import** — automated bank transaction matching with configurable rules
- **Payment Reconciliation** — batch payment reconciliation with outstanding invoice matching
- **Statement of Accounts** — customer/supplier statement generation
- **Opening Balances** — streamlined opening balance entry for go-live
- **Document Series** — configurable naming series per document type
- **Print Formats** — customizable print templates with letter heads
- **Settings** — granular module settings (accounts, buying, selling, stock, global)

---

## Tech Stack

| Layer | Technology |
|-------|-----------|
| Runtime | .NET 10, C# 13 |
| Framework | ABP.IO 10.6 (DDD, multi-tenancy, permissions, audit) |
| Database | PostgreSQL 16 + Entity Framework Core |
| Cache | Redis 7 |
| Auth | OpenIddict (OAuth 2.0 / OIDC) |
| Frontend | Angular 21.2, NgRx SignalStore 21, Bootstrap 5 (LeptonX Lite) |
| Unit Tests | xUnit (backend), Vitest 5 (frontend) |
| E2E Tests | Playwright |
| Charts | Chart.js 4 |
| TypeScript | 5.9 |
| CI/CD | GitHub Actions |
| Deployment | Docker Compose + Traefik (TLS) |
| Container Registry | Docker Hub (`amerfathullah/myerp-api`, `amerfathullah/myerp-web`, `amerfathullah/myerp-migrator`) |

---

## Self-Hosted Deployment (Docker Hub)

Deploy MyERP on any server with Docker — no build tools required.

### Quick Start (One Command)

```bash
curl -sL https://raw.githubusercontent.com/amerfathullah/MyERP/main/deploy/install.sh | bash
```

Or manually:

```bash
mkdir myerp && cd myerp
curl -sL https://raw.githubusercontent.com/amerfathullah/MyERP/main/deploy/docker-compose.yml -o docker-compose.yml
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
curl -sL https://raw.githubusercontent.com/amerfathullah/MyERP/main/deploy/docker-compose.yml -o docker-compose.yml
curl -sL https://raw.githubusercontent.com/amerfathullah/MyERP/main/deploy/.env.example -o .env

# Edit settings (database password, URLs, ports)
nano .env

# Start
docker compose up -d
```

Key `.env` settings:

| Variable | Default | Description |
|----------|---------|-------------|
| `DB_PASSWORD` | `change_me_to_a_strong_password` | PostgreSQL password (**change this!**) |
| `DB_USER` | `myerp` | PostgreSQL username |
| `DB_NAME` | `MyERP` | Database name |
| `APP_URL` | `http://localhost:5000` | Public API URL (OAuth issuer & CORS) |
| `WEB_URL` | `http://localhost` | Public frontend URL |
| `HTTP_PORT` | `80` | Host port for the web frontend |
| `API_PORT` | `5000` | Host port for the API |
| `MYERP_VERSION` | `latest` | Image tag — pin to a release (e.g. `1.0.0`) |
| `MYERP_IMAGE_PREFIX` | `amerfathullah` | Docker Hub username or org |
| `CERT_PASSPHRASE` | `change_me_to_a_strong_passphrase` | OAuth signing certificate passphrase |
| `REQUIRE_HTTPS` | `false` | Set `true` when behind a TLS reverse proxy |

### HTTPS (Custom Domain)

```bash
# Also download the HTTPS overlay
curl -sL https://raw.githubusercontent.com/amerfathullah/MyERP/main/deploy/docker-compose.https.yml -o docker-compose.https.yml

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
- [Node.js 24](https://nodejs.org/)
- [pnpm](https://pnpm.io/) (`corepack enable && corepack prepare pnpm@11 --activate`)
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
| API | https://localhost:44340 |
| Swagger UI | https://localhost:44340/swagger |

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
├── MyERP.Domain.Shared        → Constants, enums, error codes, localization
├── MyERP.Domain               → Entities, domain services, repository interfaces, events
├── MyERP.Application.Contracts → DTOs, application service interfaces, permissions
├── MyERP.Application          → Application service implementations, Mapperly mapping profiles
├── MyERP.EntityFrameworkCore  → DbContext, migrations, repository implementations
├── MyERP.HttpApi              → API controllers
├── MyERP.HttpApi.Host         → Host application (startup, middleware, configuration)
├── MyERP.HttpApi.Client       → HTTP client proxies for service-to-service calls
└── MyERP.DbMigrator           → Database migration console app

angular/                       → Angular 21 SPA (standalone components, NgRx SignalStore)
├── src/app/
│   ├── accounting/            → GL, Journal Entries, Payments, Payment Orders, Bank Reconciliation,
│   │                            Budgets, Period Closing, Dimensions, Shares, Invoice Discounting,
│   │                            Cash Flow Forecast, Ledger Health, Reports
│   ├── sales/                 → Quotations, Sales Orders, Delivery Notes, Invoices, POS,
│   │                            Pricing Rules, Promotional Schemes, Coupon Codes, Blanket Orders,
│   │                            Dunnings, Subscriptions, Proforma Invoices, Shipments, Sales Partners,
│   │                            SO Tracking Board, Reports
│   ├── purchasing/            → Material Requests, RFQ, SQ Comparison, Purchase Orders,
│   │                            Receipts, Invoices, Subcontracting, Scorecards, Incoterms,
│   │                            Procurement Dashboard, Reports
│   ├── inventory/             → Items, Item Groups, Brands, Item Prices, Item Alternatives,
│   │                            Warehouses, Stock Entries, Stock Reconciliation, Stock Reservations,
│   │                            Pick Lists, Putaway Rules, Landed Costs, Quality Management
│   │                            (Inspections, Goals, Reviews, Procedures, Meetings, Feedback,
│   │                            Non-Conformances), Batches, Serials, Delivery Trips, Transit Transfers,
│   │                            Inventory Aging, Stock Reorder, Reports
│   ├── manufacturing/         → BOMs, BOM Creators, Work Orders, Job Cards, Operations, Routings,
│   │                            Workstations, Production Plans, Master Production Schedules,
│   │                            Sales Forecasts, Material Shortage Summary, Downtime Entries,
│   │                            Plant Floors, Manufacturing Dashboard, Reports
│   ├── e-invoice/             → LHDN Dashboard, Submission Logs, Reports, Consolidation
│   ├── einvoice/              → E-Invoice batch submit, list, settings (standalone components)
│   ├── hr/                    → Employees, Departments, Designations, Employee Groups, Leave
│   │                            (Types/Allocation/Balance), Attendance, Shifts, Holiday Lists,
│   │                            Expense Claims, Loans, Salary Components, Salary Structures,
│   │                            Salary Slips, Payroll
│   ├── crm/                   → Leads, Opportunities, Pipeline, Campaigns, Email Campaigns,
│   │                            Contracts, Appointments, Prospects, Competitors
│   ├── support/               → Issues, Issue Types, Priorities, SLAs
│   ├── projects/              → Projects, Templates, Tasks, Activity Types/Costs,
│   │                            Timesheets, Timesheet Billing, Project Updates
│   ├── assets/                → Fixed Assets, Repairs, Capitalizations, Asset Movements,
│   │                            Value Adjustments, Shift Factors/Allocations, Maintenance
│   │                            Logs/Teams, Vehicles, Drivers, Locations
│   ├── tax/                   → Tax Categories, Item Tax Templates, Tax Charges Templates,
│   │                            Withholding Tax, Lower Deduction Certificates, SST-02 Filing
│   ├── workflow/              → Approval Rules, Approval Inbox, Pending Approvals
│   ├── automation/            → Automation Rules, Auto Repeat
│   ├── maintenance/           → Maintenance Schedules, Visits, Warranty Claims
│   ├── communication/         → Communication Media
│   ├── telephony/             → Call Logs, Call Types
│   ├── edi/                   → EDI Code Lists, Common Codes
│   ├── utilities/             → Utility Videos, Settings
│   ├── settings/              → Company Settings, Module Settings, Document Series,
│   │                            Authorization Rules, Email Templates, Print Formats, Notifications
│   ├── import-export/         → CSV Import/Export
│   ├── companies/             → Company management
│   ├── customers/             → Customer management
│   ├── suppliers/             → Supplier management
│   └── shared/                → Shared components, directives, pipes, guards, services, store
├── e2e/                       → Playwright E2E tests

test/
├── MyERP.Domain.Tests         → 11,500+ unit tests (entities, domain services, business rules)
├── MyERP.Application.Tests    → Integration tests (app services, conversion flows)
├── MyERP.EntityFrameworkCore.Tests → Repository/query tests
├── MyERP.TestBase             → Shared test fixtures and base classes
└── MyERP.HttpApi.Client.ConsoleTestApp → HTTP client smoke tests

docs/
├── architecture.md            → System architecture & module map
├── deployment.md              → Full deployment guide (local, Docker, production)
├── testing.md                 → Testing strategy & coverage
├── api-reference.md           → REST API endpoint reference
└── malaysia-compliance.md     → LHDN, SST, EPF/SOCSO/EIS, PDPA compliance

etc/
├── abp-studio/                → ABP Studio run profiles
├── scripts/                   → Utility scripts
└── run-profiles/              → Launch profiles (initialize-solution.ps1, migrate-database.ps1)
```

---

## Running Tests

### Backend

```bash
dotnet test
```

Runs 11,500+ unit tests covering domain entities, value objects, domain services, and business rule validation.

### Frontend Unit Tests

```bash
cd angular && pnpm test
```

### E2E Tests (Playwright)

```bash
cd angular
npx playwright install --with-deps
pnpm test:e2e
```

---

## Deployment

### Production (Docker Compose + Traefik)

```bash
# Download and configure
curl -sL https://raw.githubusercontent.com/amerfathullah/MyERP/main/deploy/docker-compose.yml -o docker-compose.yml
curl -sL https://raw.githubusercontent.com/amerfathullah/MyERP/main/deploy/.env.example -o .env
nano .env   # set DB_PASSWORD, CERT_PASSPHRASE, APP_URL, WEB_URL, etc.

# Deploy (includes PostgreSQL, Redis, migrator, API, web frontend)
docker compose up -d

# With HTTPS + Traefik (set DOMAIN and ACME_EMAIL in .env first)
docker compose -f docker-compose.yml -f docker-compose.https.yml up -d
```

> **Production stack** (`docker-compose.prod.yml` at repo root) also available — includes Traefik reverse proxy (`myerp-traefik`) with automatic TLS.

See [docs/deployment.md](docs/deployment.md) for full production setup guide.

### CI/CD

GitHub Actions workflows:
- **CI** (`ci.yml`): Runs on every push/PR to `main`/`develop`
  - **Backend**: restore, build (`-warnaserror`), test
  - **Frontend**: install, lint, type-check, unit tests, production build
  - **Docker Build** (on push to `main`): validates Dockerfile builds
- **Deploy** (`deploy.yml`): On tag push — build images → push to Docker Hub → deploy

---

## Malaysia Compliance

| Feature | Status |
|---------|--------|
| LHDN e-Invoice (MyInvois) | ✅ Submit, validate, cancel, dashboard, reports |
| XAdES Digital Signing | ✅ RSA-SHA256, UBL Extension |
| SST Tax Engine | ✅ Configurable rates & rules, SST-02 filing |
| EPF Contribution | ✅ Data-driven (age/citizenship filters) |
| SOCSO Contribution | ✅ Data-driven (salary ceiling) |
| EIS Contribution | ✅ Data-driven |
| PCB/MTD | ✅ Graduated schedule (annual cumulative method) |
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
| [Security](SECURITY.md) | Vulnerability reporting policy |

---

## Contributing

1. Create a feature branch from `develop`
2. Follow ABP DDD conventions (see `.agents/AGENTS.md` and `.agents/skills/`)
3. Ensure all tests pass: `dotnet test`
4. Add tests for new domain logic
5. Update localization files (`en.json` + `ms-MY.json` at minimum)
6. Submit a PR to `develop`

---

## License

See [LICENSE](LICENSE) for details.
