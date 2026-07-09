# MyERP — Testing Guide

## Test Structure

```
test/
├── MyERP.Domain.Tests/              → Unit tests for domain entities & services
├── MyERP.Application.Tests/         → Integration tests for AppServices
├── MyERP.EntityFrameworkCore.Tests/  → Repository & query tests
└── MyERP.TestBase/                  → Shared test infrastructure

angular/
├── e2e/                             → Playwright E2E tests
│   ├── playwright.config.ts
│   └── tests/
│       ├── auth.setup.ts            → Authentication setup (shared state)
│       ├── home.spec.ts             → Dashboard tests
│       ├── sales-invoices.spec.ts   → Sales invoice CRUD flow
│       ├── accounting-inventory.spec.ts
│       ├── modules.spec.ts          → All module page loads
│       └── navigation.spec.ts       → Menu & authorization tests
└── src/**/*.spec.ts                 → Component unit tests (Vitest)
```

---

## Running Tests

### Backend (Domain + Application + EF Core)

```bash
cd MyERP

# Run all tests
dotnet test

# Run specific project
dotnet test test/MyERP.Domain.Tests

# Run with coverage
dotnet test --collect:"XPlat Code Coverage"

# Run specific test class
dotnet test --filter "FullyQualifiedName~JournalEntryTests"
```

### Frontend Unit Tests (Vitest)

```bash
cd MyERP/angular

# Run all tests
pnpm test

# Watch mode
pnpm test -- --watch

# Coverage
pnpm test -- --coverage
```

### E2E Tests (Playwright)

```bash
cd MyERP/angular

# Install Playwright browsers (first time)
npx playwright install --with-deps

# Run all E2E tests (requires API + Angular running)
npx playwright test --config=e2e/playwright.config.ts

# Run specific test file
npx playwright test --config=e2e/playwright.config.ts tests/sales-invoices.spec.ts

# Run with UI mode (interactive)
npx playwright test --config=e2e/playwright.config.ts --ui

# View report after run
npx playwright show-report e2e/playwright-report
```

**Prerequisites for E2E:**
- PostgreSQL running with seeded data (via DbMigrator)
- API server running at `http://localhost:5000`
- Angular app running at `http://localhost:4200`

---

## Test Coverage by Module (115+ domain tests)

| Module | Tests | Coverage Focus |
|--------|-------|---------------|
| Accounting | 6 | Journal entry balance validation, post/cancel lifecycle |
| Tax | 2 | Effective date ranges, rate calculations |
| Sales | 12 | Invoice/DeliveryNote lifecycle, totals recalculation |
| Purchasing | 8 | PurchaseInvoice/PurchaseReceipt workflows |
| HR | 7 | PayrollEntry lifecycle, contribution rules |
| CRM | 20 | Lead/Opportunity lifecycle, state transitions |
| Projects | 14 | Project/Task lifecycle, progress calculation |
| Assets | 11 | Depreciation schedules, asset lifecycle |
| Manufacturing | 11 | BOM costing, WorkOrder production tracking |
| Stock Valuation | 6 | Weighted average calculations |
| Integration | 5 | Document conversion flows |

---

## Writing New Tests

### Domain Test Pattern

```csharp
public class SalesInvoiceTests : MyERPDomainTestBase
{
    [Fact]
    public void Should_Submit_Draft_Invoice()
    {
        // Arrange
        var invoice = new SalesInvoice(Guid.NewGuid(), /* ... */);
        invoice.AddItem(/* ... */);

        // Act
        invoice.Submit();

        // Assert
        invoice.Status.ShouldBe(DocumentStatus.Submitted);
    }

    [Fact]
    public void Should_Not_Submit_Without_Items()
    {
        var invoice = new SalesInvoice(Guid.NewGuid(), /* ... */);

        Should.Throw<BusinessException>(() => invoice.Submit())
            .Code.ShouldBe(MyERPDomainErrorCodes.InvoiceHasNoItems);
    }
}
```

### Application Test Pattern

```csharp
public class SalesInvoiceAppService_Tests : MyERPApplicationTestBase
{
    private readonly ISalesInvoiceAppService _appService;

    public SalesInvoiceAppService_Tests()
    {
        _appService = GetRequiredService<ISalesInvoiceAppService>();
    }

    [Fact]
    public async Task Should_Create_Invoice()
    {
        var result = await _appService.CreateAsync(new CreateSalesInvoiceDto { /* ... */ });
        result.Id.ShouldNotBe(Guid.Empty);
        result.Status.ShouldBe("Draft");
    }
}
```

### E2E Test Pattern

```typescript
import { test, expect } from '@playwright/test';

test.describe('Feature Name', () => {
  test('user can perform action', async ({ page }) => {
    await page.goto('/path');
    await page.getByRole('button', { name: 'Action' }).click();
    await expect(page.locator('.result')).toBeVisible();
  });
});
```

---

## Mandatory Test Rules

1. **All accounting logic must prove double-entry balance** — every test that creates journal entries must assert `totalDebit === totalCredit`.
2. **All tax calculations must have tests with known expected results** — never rely on approximate assertions.
3. **All workflow state transitions must test both valid and invalid transitions** — ensure invalid transitions throw `BusinessException`.
4. **E2E tests must not depend on specific data** — use `isVisible()` guards before interacting with dynamic content.
