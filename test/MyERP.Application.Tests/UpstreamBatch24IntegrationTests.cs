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
using MyERP.Purchasing;
using MyERP.Purchasing.Entities;
using MyERP.Sales;
using MyERP.Sales.Entities;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Xunit;

namespace MyERP;

/// <summary>
/// End-to-end Application + EF Core SQLite integration tests for Batch 24 upstream parity:
/// 1) Party account billing currency selection (PR #64936 / commit 9ecfac159f).
/// 2) Subcontracting receipt return row sends back rejected qty only while flagged and in rejected warehouse (PR #64938 / commit addb262ffc).
/// 3) Delivery Note bundle return decomposes at exact delivered SLE quantities when pick list absent (PR #65005 / commit ea7e451bcf).
/// </summary>
public abstract class UpstreamBatch24IntegrationTests<TStartupModule> : MyERPApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    [Fact]
    public async Task PartyDetailsAppService_ResolvesAcceptedPartyAccountCurrency_FromBillingCurrency()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var customerRepo = GetRequiredService<IRepository<Customer, Guid>>();
            var partyDetailsAppService = GetRequiredService<IPartyDetailsAppService>();

            var company = await companyRepo.InsertAsync(
                new Company(Guid.NewGuid(), "Party Details Co B24") { CurrencyCode = "MYR" },
                autoSave: true);

            // Customer with foreign billing currency (USD)
            var customer = await customerRepo.InsertAsync(
                new Customer(Guid.NewGuid(), company.Id, "Foreign Customer B24")
                {
                    DefaultCurrency = "USD"
                },
                autoSave: true);

            var details = await partyDetailsAppService.GetCustomerDetailsAsync(new GetPartyDetailsInput
            {
                PartyId = customer.Id,
                CompanyId = company.Id
            });

            details.PartyCurrency.ShouldBe("USD");
            details.PartyAccountCurrency.ShouldBe("USD");
            details.CompanyCurrency.ShouldBe("MYR");
        });
    }

    [Fact]
    public async Task SubcontractingAppService_AcceptedReturnFromMatchingRejectedWarehouse_RetainsFinishedGoodRate()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var supplierRepo = GetRequiredService<IRepository<Supplier, Guid>>();
            var warehouseRepo = GetRequiredService<IRepository<Warehouse, Guid>>();
            var itemRepo = GetRequiredService<IRepository<Item, Guid>>();
            var scrRepo = GetRequiredService<IRepository<SubcontractingReceipt, Guid>>();
            var scoRepo = GetRequiredService<IRepository<SubcontractingOrder, Guid>>();
            var seriesRepo = GetRequiredService<IRepository<DocumentSeries, Guid>>();
            var subAppService = GetRequiredService<ISubcontractingAppService>();

            var company = await companyRepo.InsertAsync(
                new Company(Guid.NewGuid(), "Subcontracting Co B24") { CurrencyCode = "MYR" },
                autoSave: true);

            await seriesRepo.InsertAsync(
                new DocumentSeries(Guid.NewGuid(), company.Id, "SCR-RET", "SCR-RET", "SCR-RET-2026-"),
                autoSave: true);

            var supplier = await supplierRepo.InsertAsync(
                new Supplier(Guid.NewGuid(), company.Id, "Subcontracting Supplier B24"),
                autoSave: true);

            var fgWarehouse = await warehouseRepo.InsertAsync(
                new Warehouse(Guid.NewGuid(), company.Id, "Finished Goods Wh B24"),
                autoSave: true);

            var fgItem = await itemRepo.InsertAsync(
                new Item(Guid.NewGuid(), company.Id, "FG-ITEM-B24", "Finished Good B24", ItemType.Goods)
                {
                    MaintainStock = true
                },
                autoSave: true);

            var sco = new SubcontractingOrder(
                Guid.NewGuid(),
                company.Id,
                "SCO-B24-001",
                DateTime.UtcNow.Date,
                supplier.Id);
            sco.AddItem(new SubcontractingOrderItem(Guid.NewGuid(), sco.Id, fgItem.Id, "Finished Good B24", 10m, 150m));
            await scoRepo.InsertAsync(sco, autoSave: true);

            // Create submitted original SubcontractingReceipt:
            // 8 accepted at rate 150, 2 rejected in fgWarehouse
            var origScr = new SubcontractingReceipt(
                Guid.NewGuid(),
                company.Id,
                "SCR-B24-001",
                DateTime.UtcNow.Date,
                supplier.Id,
                sco.Id);
            origScr.AddItem(new SubcontractingReceiptItem(
                Guid.NewGuid(),
                origScr.Id,
                fgItem.Id,
                "Finished Good B24",
                8m,
                150m)
            {
                RejectedQty = 2m,
                WarehouseId = fgWarehouse.Id,
                RejectedWarehouseId = fgWarehouse.Id, // Original rejected warehouse matches FG warehouse
                ReturnQtyFromRejectedWarehouse = false
            });
            origScr.Submit();
            await scrRepo.InsertAsync(origScr, autoSave: true);

            // Now create return of 2 accepted items from fgWarehouse (which happens to match rejected warehouse)
            // Per PR #64938 (commit addb262ffc): because ReturnQtyFromRejectedWarehouse is false,
            // this is an accepted return, so it retains the finished goods rate 150 (amount = -300)
            var returnDto = await subAppService.CreateReceiptReturnAsync(new CreateSubcontractingReceiptReturnDto
            {
                ReturnAgainstReceiptId = origScr.Id,
                PostingDate = DateTime.UtcNow.Date,
                WarehouseId = fgWarehouse.Id,
                Items = new List<CreateScrReturnItemDto>
                {
                    new()
                    {
                        ItemId = fgItem.Id,
                        ItemName = "Finished Good B24",
                        Qty = 2m,
                        WarehouseId = fgWarehouse.Id,
                        ReturnQtyFromRejectedWarehouse = false // Accepted return!
                    }
                }
            });

            returnDto.Items.Count.ShouldBe(1);
            var retItem = returnDto.Items[0];
            retItem.Qty.ShouldBe(-2m);
            retItem.Rate.ShouldBe(150m);
            retItem.Amount.ShouldBe(-300m);
        });
    }

    [Fact]
    public async Task DeliveryNoteAppService_BundleReturnFallbackToDeliveredSles_DecomposesDeliveredStockQty()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var customerRepo = GetRequiredService<IRepository<Customer, Guid>>();
            var warehouseRepo = GetRequiredService<IRepository<Warehouse, Guid>>();
            var itemRepo = GetRequiredService<IRepository<Item, Guid>>();
            var bundleRepo = GetRequiredService<IRepository<ProductBundle, Guid>>();
            var dnRepo = GetRequiredService<IRepository<DeliveryNote, Guid>>();
            var sleRepo = GetRequiredService<IRepository<StockLedgerEntry, Guid>>();
            var binRepo = GetRequiredService<IRepository<Bin, Guid>>();
            var dnAppService = GetRequiredService<IDeliveryNoteAppService>();

            var company = await companyRepo.InsertAsync(
                new Company(Guid.NewGuid(), "DN Bundle Co B24") { CurrencyCode = "MYR" },
                autoSave: true);

            await SeedGlAndSeriesFixtureAsync(
                company,
                GetRequiredService<IRepository<Account, Guid>>(),
                GetRequiredService<IRepository<CostCenter, Guid>>(),
                GetRequiredService<IRepository<FiscalYear, Guid>>(),
                GetRequiredService<IRepository<AccountingRule, Guid>>(),
                GetRequiredService<IRepository<Company, Guid>>(),
                GetRequiredService<IRepository<DocumentSeries, Guid>>(),
                "B24");

            var customer = await customerRepo.InsertAsync(
                new Customer(Guid.NewGuid(), company.Id, "DN Bundle Customer B24"),
                autoSave: true);

            var warehouse = await warehouseRepo.InsertAsync(
                new Warehouse(Guid.NewGuid(), company.Id, "DN Bundle Warehouse B24"),
                autoSave: true);

            // Component item
            var componentItem = await itemRepo.InsertAsync(
                new Item(Guid.NewGuid(), company.Id, "COMP-B24", "Bundle Component B24", ItemType.Goods)
                {
                    MaintainStock = true,
                    StandardBuyingPrice = 10m
                },
                autoSave: true);

            // Parent bundle item (virtual/non-stock)
            var bundleItem = await itemRepo.InsertAsync(
                new Item(Guid.NewGuid(), company.Id, "BUNDLE-B24", "Parent Bundle B24", ItemType.Goods)
                {
                    MaintainStock = false,
                    StandardSellingPrice = 100m
                },
                autoSave: true);

            // Product bundle: 1 Unit bundle contains 10 Nos of component
            var bundle = new ProductBundle(Guid.NewGuid(), bundleItem.Id);
            bundle.AddItem(componentItem.Id, 10m, "Bundle Component B24", "Nos");
            await bundleRepo.InsertAsync(bundle, autoSave: true);

            // Seed stock in warehouse for component: 50 Nos
            var bin = new Bin(Guid.NewGuid(), componentItem.Id, warehouse.Id);
            bin.UpdateActualQty(50m, 500m);
            await binRepo.InsertAsync(bin, autoSave: true);

            // Original Delivery Note: delivered 2 bundles (so 20 Nos of component)
            var origDn = new DeliveryNote(
                Guid.NewGuid(),
                company.Id,
                customer.Id,
                warehouse.Id,
                "DN-B24-001",
                DateTime.UtcNow.Date);
            origDn.AddItem(bundleItem.Id, "Parent Bundle B24", 2m, 100m, 0m, "Unit");
            origDn.Submit();
            await dnRepo.InsertAsync(origDn, autoSave: true);

            // Simulate the original DN's posted SLE: -20 Nos of component
            var origSle = new StockLedgerEntry(
                Guid.NewGuid(),
                company.Id,
                componentItem.Id,
                warehouse.Id,
                origDn.PostingDate,
                TimeSpan.FromHours(10),
                -20m, // 20 Nos delivered for 2 bundles -> 10 Nos per bundle
                10m,
                30m,
                300m,
                voucherType: "DeliveryNote",
                voucherId: origDn.Id);
            await sleRepo.InsertAsync(origSle, autoSave: true);

            // Now create a return against origDn: returning 1 bundle
            var returnDn = new DeliveryNote(
                Guid.NewGuid(),
                company.Id,
                customer.Id,
                warehouse.Id,
                "DN-RET-B24-001",
                DateTime.UtcNow.Date)
            {
                IsReturn = true,
                ReturnAgainstId = origDn.Id
            };
            returnDn.AddItem(bundleItem.Id, "Parent Bundle B24", -1m, 100m, 0m, "Unit");
            await dnRepo.InsertAsync(returnDn, autoSave: true);

            // Submit return via DeliveryNoteAppService
            await dnAppService.SubmitAsync(returnDn.Id);

            // Verify that a stock ledger entry for the returned component was created with +10 Nos (1 bundle × 10 Nos per unit from SLE)
            var returnSles = (await sleRepo.GetQueryableAsync())
                .Where(s => s.VoucherType == "DeliveryNote" && s.VoucherId == returnDn.Id)
                .ToList();

            returnSles.Count.ShouldBe(1);
            var retSle = returnSles[0];
            retSle.ItemId.ShouldBe(componentItem.Id);
            retSle.QuantityChange.ShouldBe(10m); // Inflow of 10 Nos
        });
    }

    private static async Task SeedGlAndSeriesFixtureAsync(
        Company company,
        IRepository<Account, Guid> accountRepo,
        IRepository<CostCenter, Guid> costCenterRepo,
        IRepository<FiscalYear, Guid> fiscalYearRepo,
        IRepository<AccountingRule, Guid> ruleRepo,
        IRepository<Company, Guid> companyRepo,
        IRepository<DocumentSeries, Guid> seriesRepo,
        string tag)
    {
        var stockAccount = await accountRepo.InsertAsync(
            new Account(Guid.NewGuid(), company.Id, $"1140-{tag}", $"Test Stock {tag}", AccountType.Asset), autoSave: true);
        var cogsAccount = await accountRepo.InsertAsync(
            new Account(Guid.NewGuid(), company.Id, $"5000-{tag}", $"Test COGS {tag}", AccountType.Expense), autoSave: true);
        var costCenter = await costCenterRepo.InsertAsync(
            new CostCenter(Guid.NewGuid(), company.Id, $"Cost Center {tag}"), autoSave: true);

        company.DefaultInventoryAccountId = stockAccount.Id;
        company.DefaultExpenseAccountId = cogsAccount.Id;
        company.DefaultCostCenterId = costCenter.Id;
        await companyRepo.UpdateAsync(company, autoSave: true);

        await fiscalYearRepo.InsertAsync(
            new FiscalYear(Guid.NewGuid(), company.Id, $"FY {tag}", DateTime.UtcNow.Date.AddYears(-1), DateTime.UtcNow.Date.AddYears(1)),
            autoSave: true);

        await ruleRepo.InsertAsync(
            new AccountingRule(Guid.NewGuid(), company.Id, $"DN DR COGS {tag}", "DeliveryNote", true, AccountSource.ItemExpense, AmountSource.NetTotal) { SortOrder = 1 },
            autoSave: true);
        await ruleRepo.InsertAsync(
            new AccountingRule(Guid.NewGuid(), company.Id, $"DN CR Stock {tag}", "DeliveryNote", false, AccountSource.FixedAccount, AmountSource.NetTotal) { SortOrder = 2, FixedAccountId = stockAccount.Id },
            autoSave: true);

        await seriesRepo.InsertAsync(
            new DocumentSeries(Guid.NewGuid(), company.Id, $"DN Series {tag}", "DeliveryNote", $"DN{tag}-"), autoSave: true);
        await seriesRepo.InsertAsync(
            new DocumentSeries(Guid.NewGuid(), company.Id, $"SI Series {tag}", "SalesInvoice", $"SI{tag}-"), autoSave: true);
    }
}
