using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Accounting;
using MyERP.Accounting.Entities;
using MyERP.Core;
using MyERP.Core.Entities;
using MyERP.Inventory;
using MyERP.Inventory.Entities;
using MyERP.Manufacturing;
using MyERP.Manufacturing.Entities;
using MyERP.Sales;
using MyERP.Sales.Entities;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Xunit;

namespace MyERP;

/// <summary>
/// End-to-end Application + EF Core SQLite integration tests for Batch 10 upstream ERPNext parity:
/// 1) Recalculate operating cost after draft reloads operations from BOM and reset costs when BOM has no operations (00a1268b74, c3f3c96a2c).
/// 2) General Ledger report convert at reporting date exchange rate for foreign currency accounts (e8ae93e729).
/// </summary>
public abstract class UpstreamBatch10IntegrationTests<TStartupModule> : MyERPApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    [Fact]
    public async Task WorkOrder_UpdateWorkOrderAsync_RecalculatesAndResetsOperatingCostsWhenBOMChanges()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var seriesRepo = GetRequiredService<IRepository<DocumentSeries, Guid>>();
            var itemRepo = GetRequiredService<IRepository<Item, Guid>>();
            var bomRepo = GetRequiredService<IRepository<BillOfMaterials, Guid>>();
            var operationRepo = GetRequiredService<IRepository<Operation, Guid>>();
            var workstationRepo = GetRequiredService<IRepository<Workstation, Guid>>();
            var mfgAppService = GetRequiredService<IManufacturingAppService>();

            var company = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "Mfg Batch 10 Co"), autoSave: true);
            await seriesRepo.InsertAsync(new DocumentSeries(Guid.NewGuid(), company.Id, "WO", "WO", "WO-B10-"), autoSave: true);
            var fgItem = await itemRepo.InsertAsync(new Item(Guid.NewGuid(), company.Id, "FG-BATCH10", "Finished Good", ItemType.Goods), autoSave: true);
            var rmItem = await itemRepo.InsertAsync(new Item(Guid.NewGuid(), company.Id, "RM-BATCH10", "Raw Material", ItemType.Goods), autoSave: true);

            var workstation = new Workstation(Guid.NewGuid(), company.Id, "WS-1");
            workstation.AddCost("Labor", 120m);
            await workstationRepo.InsertAsync(workstation, autoSave: true);

            var operation = await operationRepo.InsertAsync(new Operation(Guid.NewGuid(), "Assemble"), autoSave: true);

            // BOM 1: with operations (30 mins @ 120/hr = 60 operating cost per unit)
            var bomWithOps = new BillOfMaterials(Guid.NewGuid(), company.Id, "BOM-WITH-OPS", fgItem.Id)
            {
                Quantity = 1m,
                WithOperations = true,
                IsActive = true,
                IsDefault = true
            };
            bomWithOps.AddItem(new BomItem(Guid.NewGuid(), bomWithOps.Id, rmItem.Id, "RM", 1m, 10m));
            var bomOp = new BomOperation(Guid.NewGuid(), bomWithOps.Id, operation.Id, sequenceId: 1, timeInMins: 30m, workstationId: workstation.Id)
            {
                OperatingCost = 60m
            };
            bomWithOps.AddOperation(bomOp);
            bomWithOps.RecalculateCost();
            await bomRepo.InsertAsync(bomWithOps, autoSave: true);

            // BOM 2: without operations (0 operating cost)
            var bomWithoutOps = new BillOfMaterials(Guid.NewGuid(), company.Id, "BOM-NO-OPS", fgItem.Id)
            {
                Quantity = 1m,
                WithOperations = false,
                IsActive = true,
                IsDefault = false
            };
            bomWithoutOps.AddItem(new BomItem(Guid.NewGuid(), bomWithoutOps.Id, rmItem.Id, "RM", 1m, 10m));
            bomWithoutOps.RecalculateCost();
            await bomRepo.InsertAsync(bomWithoutOps, autoSave: true);

            // Create Work Order using BOM 1
            var createdWo = await mfgAppService.CreateWorkOrderAsync(new CreateWorkOrderDto
            {
                CompanyId = company.Id,
                ItemId = fgItem.Id,
                BomId = bomWithOps.Id,
                Quantity = 1m,
                AdditionalOperatingCost = 20m
            });

            createdWo.PlannedOperatingCost.ShouldBe(60m);
            createdWo.TotalOperatingCost.ShouldBe(80m); // 60 + 20

            // Update Work Order to BOM 2 (without operations)
            var updatedWo = await mfgAppService.UpdateWorkOrderAsync(createdWo.Id, new CreateWorkOrderDto
            {
                CompanyId = company.Id,
                ItemId = fgItem.Id,
                BomId = bomWithoutOps.Id,
                Quantity = 1m,
                AdditionalOperatingCost = 20m
            });

            // Operating cost must reset to 0, TotalOperatingCost becomes AdditionalOperatingCost
            updatedWo.PlannedOperatingCost.ShouldBe(0m);
            updatedWo.TotalOperatingCost.ShouldBe(20m);
        });
    }

    [Fact]
    public async Task GeneralLedger_ConvertAtReportingDateRate_ConvertsForeignCurrencyAccountAtToDateExchangeRate()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var accountRepo = GetRequiredService<IRepository<Account, Guid>>();
            var fyRepo = GetRequiredService<IRepository<FiscalYear, Guid>>();
            var jeRepo = GetRequiredService<IRepository<JournalEntry, Guid>>();
            var exchangeRepo = GetRequiredService<IRepository<CurrencyExchange, Guid>>();
            var glAppService = GetRequiredService<IGeneralLedgerAppService>();

            var company = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "GL Reporting Rate Co")
            {
                CurrencyCode = "MYR"
            }, autoSave: true);

            var today = DateTime.UtcNow.Date;
            var fy = await fyRepo.InsertAsync(new FiscalYear(Guid.NewGuid(), company.Id, "FY-2026", new DateTime(2026, 1, 1), new DateTime(2026, 12, 31)), autoSave: true);

            var usdAccount = await accountRepo.InsertAsync(new Account(Guid.NewGuid(), company.Id, "1110-USD", "USD Bank Account", AccountType.Asset)
            {
                Currency = "USD"
            }, autoSave: true);

            var cashAccount = await accountRepo.InsertAsync(new Account(Guid.NewGuid(), company.Id, "1120-MYR", "MYR Cash Account", AccountType.Asset)
            {
                Currency = "MYR"
            }, autoSave: true);

            // Post JE 10 days ago: 100 USD @ 4.0 MYR = 400 MYR
            var postingDate = today.AddDays(-10);
            var je = new JournalEntry(Guid.NewGuid(), company.Id, fy.Id, postingDate);
            je.AddLine(usdAccount.Id, 400m, isDebit: true);
            je.Lines[^1].AccountCurrency = "USD";
            je.Lines[^1].AmountInAccountCurrency = 100m;
            je.Lines[^1].ExchangeRate = 4.0m;

            je.AddLine(cashAccount.Id, 400m, isDebit: false);
            je.Lines[^1].AccountCurrency = "MYR";
            je.Lines[^1].AmountInAccountCurrency = 400m;
            je.Lines[^1].ExchangeRate = 1.0m;

            je.Validate();
            je.Post();
            await jeRepo.InsertAsync(je, autoSave: true);

            // Exchange rate today (ToDate): 1 USD = 5.0 MYR (was 4.0)
            await exchangeRepo.InsertAsync(new CurrencyExchange(Guid.NewGuid(), "USD", "MYR", 5.0m, today), autoSave: true);

            // Case 1: Standard report without reporting date conversion (in company currency MYR)
            var standardReport = await glAppService.GetReportAsync(new GeneralLedgerFilterDto
            {
                CompanyId = company.Id,
                AccountId = usdAccount.Id,
                FromDate = today.AddDays(-15),
                ToDate = today,
                ConvertAtReportingDateRate = false
            });

            standardReport.Entries.Count.ShouldBe(1);
            standardReport.Entries[0].DebitAmount.ShouldBe(400m);
            standardReport.TotalDebit.ShouldBe(400m);

            // Case 2: Report with ConvertAtReportingDateRate enabled:
            // Converts 400 MYR / 5.0 MYR/USD = 80 USD
            var convertedReport = await glAppService.GetReportAsync(new GeneralLedgerFilterDto
            {
                CompanyId = company.Id,
                AccountId = usdAccount.Id,
                FromDate = today.AddDays(-15),
                ToDate = today,
                ConvertAtReportingDateRate = true
            });

            convertedReport.PresentationCurrency.ShouldBe("USD");
            convertedReport.Entries.Count.ShouldBe(1);
            convertedReport.Entries[0].DebitAmount.ShouldBe(80m);
            convertedReport.TotalDebit.ShouldBe(80m);
            convertedReport.Balance.ShouldBe(80m);
        });
    }
}
