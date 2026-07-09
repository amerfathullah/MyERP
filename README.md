# MyERP

A modular Enterprise Resource Planning (ERP) system built with [ABP Framework](https://abp.io/) 10.x, .NET 10, and Angular 21.

## Modules

| Module | Description |
|--------|-------------|
| Accounting | General ledger, chart of accounts, journal entries |
| Sales | Sales orders, quotations, invoicing |
| Purchasing | Purchase orders, vendor management |
| Inventory | Stock management, warehousing |
| Human Resources | Employee management, payroll |
| Tax | Tax calculations, reporting |
| E-Invoice | Electronic invoicing |
| Import/Export | Data import and export utilities |
| Automation | Workflow automation |
| Notification | Email, SMS, and in-app notifications |

## Tech Stack

- **Backend:** .NET 10 / ASP.NET Core / ABP Framework 10.5
- **Frontend:** Angular 21 / ABP Angular UI / Lepton X Theme
- **Database:** PostgreSQL 16
- **Cache:** Redis 7
- **Package Manager:** pnpm

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Node.js](https://nodejs.org/) (LTS)
- [pnpm](https://pnpm.io/)
- [Docker](https://www.docker.com/) (for infrastructure services)

## Getting Started

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

### Quick Setup (All-in-One)

```powershell
./setup-dev.ps1
```

## URLs

| Service | URL |
|---------|-----|
| Angular App | http://localhost:4200 |
| API | http://localhost:5000 |
| Swagger UI | http://localhost:5000/swagger |

**Default Login:** `admin` / `1q2w3E*`

## Docker (Full Stack)

```bash
docker compose up -d
```

This starts PostgreSQL, Redis, the API, and the Angular app.

## Project Structure

```
src/
├── MyERP.Domain.Shared        # Shared constants, enums, localization
├── MyERP.Domain               # Entities, domain services, repository interfaces
├── MyERP.Application.Contracts # DTOs, application service interfaces, permissions
├── MyERP.Application          # Application service implementations
├── MyERP.EntityFrameworkCore  # EF Core DbContext, migrations, repository implementations
├── MyERP.HttpApi              # API controllers
├── MyERP.HttpApi.Host         # API host (startup, configuration)
├── MyERP.HttpApi.Client       # HTTP client proxies
└── MyERP.DbMigrator           # Database migration console app

angular/                       # Angular frontend application
test/                          # Unit and integration tests
```

## Running Tests

```bash
dotnet test
```

## License

See [LICENSE](LICENSE) for details.
