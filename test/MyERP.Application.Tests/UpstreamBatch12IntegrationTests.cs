using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MyERP.Accounting;
using MyERP.Accounting.Entities;
using MyERP.Core.Entities;
using MyERP.Inventory;
using MyERP.Inventory.Entities;
using MyERP.Purchasing;
using MyERP.Purchasing.Entities;
using MyERP.Sales.Entities;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Xunit;

namespace MyERP;

/// <summary>
/// End-to-end Application + EF Core SQLite integration tests for Batch 12 upstream ERPNext parity:
/// 1) Cash Flow accumulated values custom date range only counts movements in range (PR #64939 / commit 461d586d8b).
/// 2) Pick List preserves ordered row conversion factor from Sales Order (PR #64935 / commit 0de4680ea9).
/// 3) Subcontracting Receipt Return properly classifies rejected returns vs accepted returns (PR #64938 / commit aeb5278994).
/// </summary>
public abstract class UpstreamBatch12IntegrationTests<TStartupModule> : MyERPApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    [Fact]
    public async Task CashFlowStatementAppService_AccumulatedDateRange_CountsOnlyMovementsInRange()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var accountRepo = GetRequiredService<IRepository<Account, Guid>>();
            var journalRepo = GetRequiredService<IRepository<JournalEntry, Guid>>();
            var fyRepo = GetRequiredService<IRepository<FiscalYear, Guid>>();
            var cashFlowService = GetRequiredService<ICashFlowStatementAppService>();

            var company = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "Cash Flow Range Co")
            {
                CurrencyCode = "MYR"
            }, autoSave: true);

            var fyStart = new DateTime(2026, 1, 1);
            var fyEnd = new DateTime(2026, 12, 31);
            var rangeStart = new DateTime(2026, 4, 1); // 90 days in

            var fy = await fyRepo.InsertAsync(new FiscalYear(Guid.NewGuid(), company.Id, "FY 2026", fyStart, fyEnd), autoSave: true);

            var cashAccount = await accountRepo.InsertAsync(new Account(Guid.NewGuid(), company.Id, "1110-CASH", "Cash Account", AccountType.Asset)
            {
                AccountSubType = AccountSubType.CashAccount,
                Currency = "MYR",
                IsActive = true
            }, autoSave: true);

            var equityAccount = await accountRepo.InsertAsync(new Account(Guid.NewGuid(), company.Id, "3100-CAP", "Capital Stock", AccountType.Equity)
            {
                Currency = "MYR",
                IsActive = true
            }, autoSave: true);

            // 1) Movement at fiscal year start date (2026-01-01): RM 1000 — BEFORE the date range
            var jeBefore = new JournalEntry(Guid.NewGuid(), company.Id, fy.Id, fyStart);
            jeBefore.AddLine(cashAccount.Id, 1000m, isDebit: true);
            jeBefore.AddLine(equityAccount.Id, 1000m, isDebit: false);
            jeBefore.Post();
            await journalRepo.InsertAsync(jeBefore, autoSave: true);

            // 2) Movement at range start date (2026-04-01): RM 500 — INSIDE the date range
            var jeDuring = new JournalEntry(Guid.NewGuid(), company.Id, fy.Id, rangeStart);
            jeDuring.AddLine(cashAccount.Id, 500m, isDebit: true);
            jeDuring.AddLine(equityAccount.Id, 500m, isDebit: false);
            jeDuring.Post();
            await journalRepo.InsertAsync(jeDuring, autoSave: true);

            // Query cash flow with range 2026-04-01 to 2026-12-31, quarterly, accumulated values enabled
            var result = await cashFlowService.GetCashFlowStatementAsync(new CashFlowRequestDto
            {
                CompanyId = company.Id,
                FromDate = rangeStart,
                ToDate = fyEnd,
                Periodicity = "Quarterly",
                AccumulatedValues = true
            });

            // Opening cash on rangeStart (2026-04-01 - 1 day = 2026-03-31) must be 1000 (from first JE)
            result.OpeningCashBalance.ShouldBe(1000m);

            // Net change inside range must be 500 (from second JE, excluding first JE)
            result.NetCashChange.ShouldBe(500m);
            result.ClosingCashBalance.ShouldBe(1500m);

            // Each period column should have EffectiveStartDate = rangeStart and NetCashChange reflecting movements from rangeStart
            result.Periods.ShouldNotBeEmpty();
            foreach (var p in result.Periods)
            {
                p.EffectiveStartDate.ShouldBe(rangeStart);
                p.NetCashChange.ShouldBe(500m);
            }
        });
    }

    [Fact]
    public async Task PickListAppService_CreateAsync_PreservesOrderedConversionFactor()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var itemRepo = GetRequiredService<IRepository<Item, Guid>>();
            var whRepo = GetRequiredService<IRepository<Warehouse, Guid>>();
            var soRepo = GetRequiredService<IRepository<SalesOrder, Guid>>();
            var pickListAppService = GetRequiredService<IPickListAppService>();

            var company = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "Pick List Co")
            {
                CurrencyCode = "MYR"
            }, autoSave: true);

            var warehouse = await whRepo.InsertAsync(new Warehouse(Guid.NewGuid(), company.Id, "WH-MAIN")
            {
                IsActive = true
            }, autoSave: true);

            var item = await itemRepo.InsertAsync(new Item(Guid.NewGuid(), company.Id, "ITEM-BOX", "Boxed Item", ItemType.Goods)
            {
                Uom = "Nos",
                IsActive = true
            }, autoSave: true);

            // Create Sales Order with ordered UOM = "Box" and ConversionFactor = 10
            var so = new SalesOrder(Guid.NewGuid(), company.Id, Guid.NewGuid(), "SO-PICK-01", DateTime.Today);
            so.AddItem(item.Id, "Boxed Item", 2m, 50m, 0m, uom: "Box");
            var soItem = so.Items[0];
            soItem.ConversionFactor = 10m;
            so.Submit();
            await soRepo.InsertAsync(so, autoSave: true);

            // Now create Pick List for this Sales Order
            var pickListDto = await pickListAppService.CreateAsync(new CreatePickListDto
            {
                CompanyId = company.Id,
                Purpose = "Delivery",
                SalesOrderId = so.Id,
                Items = new[]
                {
                    new CreatePickListItemDto
                    {
                        ItemId = item.Id,
                        WarehouseId = warehouse.Id,
                        Qty = 2m,
                        Uom = "Box",
                        ConversionFactor = 15m, // e.g. Master conversion factor was changed to 15
                        SourceDocumentItemId = soItem.Id
                    }
                }
            });

            pickListDto.ShouldNotBeNull();
            pickListDto.Items.Length.ShouldBe(1);
            var pickRow = pickListDto.Items[0];

            // Must preserve ordered conversion factor = 10 and StockQty = 20 (not 15 and 30)
            pickRow.ConversionFactor.ShouldBe(10m);
            pickRow.StockQty.ShouldBe(20m);
        });
    }

    [Fact]
    public async Task SubcontractingAppService_CreateReturnAsync_AcceptedReturnFromRejectedWarehouse_PricesAtOriginalRate()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var supplierRepo = GetRequiredService<IRepository<Supplier, Guid>>();
            var itemRepo = GetRequiredService<IRepository<Item, Guid>>();
            var whRepo = GetRequiredService<IRepository<Warehouse, Guid>>();
            var scoRepo = GetRequiredService<IRepository<SubcontractingOrder, Guid>>();
            var scrRepo = GetRequiredService<IRepository<SubcontractingReceipt, Guid>>();
            var seriesRepo = GetRequiredService<IRepository<DocumentSeries, Guid>>();
            var subcontractingService = GetRequiredService<ISubcontractingAppService>();

            var company = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "Subcon Return Co")
            {
                CurrencyCode = "MYR"
            }, autoSave: true);

            await seriesRepo.InsertAsync(new DocumentSeries(Guid.NewGuid(), company.Id, "SCR-RET Series", "SCR-RET", "SCR-RET-"), autoSave: true);

            var supplier = await supplierRepo.InsertAsync(new Supplier(Guid.NewGuid(), company.Id, "Subcon Supplier")
            {
                IsActive = true
            }, autoSave: true);

            var acceptedWh = await whRepo.InsertAsync(new Warehouse(Guid.NewGuid(), company.Id, "WH-ACC")
            {
                IsActive = true
            }, autoSave: true);

            var rejectedWh = await whRepo.InsertAsync(new Warehouse(Guid.NewGuid(), company.Id, "WH-REJ")
            {
                IsActive = true
            }, autoSave: true);

            var fgItem = await itemRepo.InsertAsync(new Item(Guid.NewGuid(), company.Id, "FG-001", "Finished Good", ItemType.Goods)
            {
                Uom = "Nos",
                IsActive = true
            }, autoSave: true);

            var sco = new SubcontractingOrder(Guid.NewGuid(), company.Id, "SCO-001", DateTime.Today, supplier.Id);
            await scoRepo.InsertAsync(sco, autoSave: true);

            // Subcontracting Receipt: received 8 accepted (RM 100/unit) and 2 rejected
            var scr = new SubcontractingReceipt(Guid.NewGuid(), company.Id, "SCR-001", DateTime.Today, supplier.Id, sco.Id);
            var scrItem = new SubcontractingReceiptItem(Guid.NewGuid(), scr.Id, fgItem.Id, "Finished Good", 8m, 100m)
            {
                WarehouseId = acceptedWh.Id,
                RejectedWarehouseId = rejectedWh.Id,
                RejectedQty = 2m
            };
            scr.AddItem(scrItem);
            scr.Submit();
            await scrRepo.InsertAsync(scr, autoSave: true);

            // Return from rejected warehouse, but returnQtyFromRejectedWarehouse is FALSE (accepted goods returned from rejected warehouse)
            // Per upstream commit addb262ffc: must be treated as accepted return at original item rate (100)
            var returnDto = await subcontractingService.CreateReceiptReturnAsync(new CreateSubcontractingReceiptReturnDto
            {
                ReturnAgainstReceiptId = scr.Id,
                PostingDate = DateTime.Today,
                Items = new List<CreateScrReturnItemDto>
                {
                    new CreateScrReturnItemDto
                    {
                        ItemId = fgItem.Id,
                        ItemName = "Finished Good",
                        Qty = -2m,
                        WarehouseId = rejectedWh.Id, // Warehouse is rejected warehouse
                        ReturnQtyFromRejectedWarehouse = false // BUT flag is false
                    }
                }
            });

            returnDto.ShouldNotBeNull();
            returnDto.Items.Count.ShouldBe(1);
            var retItem = returnDto.Items[0];

            // Must use origItem.Rate (100) because it's an accepted return
            retItem.Rate.ShouldBe(100m);
            retItem.Amount.ShouldBe(-200m);
        });
    }
}
