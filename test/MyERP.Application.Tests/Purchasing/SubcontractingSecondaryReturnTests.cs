using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Accounting;
using MyERP.Accounting.Entities;
using MyERP.Core.Entities;
using MyERP.Inventory;
using MyERP.Inventory.Entities;
using MyERP.Purchasing.Entities;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Xunit;

namespace MyERP.Purchasing;

/// <summary>
/// Regression tests for Subcontracting Receipt secondary items &amp; return rebuilding
/// per ERPNext PR #66400 (excludes secondary items from RM consumption &amp; SCO fulfillment;
/// rebuilds negative secondary rows on partial/final/rejected returns).
/// </summary>
public abstract class SubcontractingSecondaryReturnTests<TStartupModule> : MyERPApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    private static async Task SetupAccountingDefaultsAsync(
        IRepository<Account, Guid> accountRepo,
        IRepository<FiscalYear, Guid> fyRepo,
        IRepository<AccountingRule, Guid> ruleRepo,
        Guid companyId,
        string prefix)
    {
        await fyRepo.InsertAsync(new FiscalYear(
            Guid.NewGuid(), companyId, $"{prefix} FY26",
            new DateTime(2026, 1, 1), new DateTime(2026, 12, 31)), autoSave: true);

        var stockAcc = await accountRepo.InsertAsync(
            new Account(Guid.NewGuid(), companyId, $"{prefix}-1400", "Stock In Hand", AccountType.Asset),
            autoSave: true);
        var srbnbAcc = await accountRepo.InsertAsync(
            new Account(Guid.NewGuid(), companyId, $"{prefix}-2100", "Stock Received But Not Billed", AccountType.Liability),
            autoSave: true);

        await ruleRepo.InsertAsync(new AccountingRule(
            Guid.NewGuid(), companyId, "SCR Stock Dr", "SubcontractingReceipt",
            isDebit: true, AccountSource.FixedAccount, AmountSource.NetTotal)
        {
            FixedAccountId = stockAcc.Id,
            SortOrder = 1
        }, autoSave: true);
        await ruleRepo.InsertAsync(new AccountingRule(
            Guid.NewGuid(), companyId, "SCR SRBNB Cr", "SubcontractingReceipt",
            isDebit: false, AccountSource.FixedAccount, AmountSource.NetTotal)
        {
            FixedAccountId = srbnbAcc.Id,
            SortOrder = 2
        }, autoSave: true);
    }

    [Fact]
    public async Task SubmitAndCancelReceipt_ExcludesSecondaryItems_FromRmConsumption_And_ScoFulfillment()
    {
        Guid scrId = default, scoId = default;
        decimal fgRate = 100m;

        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var supplierRepo = GetRequiredService<IRepository<Supplier, Guid>>();
            var whRepo = GetRequiredService<IRepository<Warehouse, Guid>>();
            var itemRepo = GetRequiredService<IRepository<Item, Guid>>();
            var seriesRepo = GetRequiredService<IRepository<DocumentSeries, Guid>>();
            var accountRepo = GetRequiredService<IRepository<Account, Guid>>();
            var fyRepo = GetRequiredService<IRepository<FiscalYear, Guid>>();
            var ruleRepo = GetRequiredService<IRepository<AccountingRule, Guid>>();
            var scoRepo = GetRequiredService<IRepository<SubcontractingOrder, Guid>>();
            var scrRepo = GetRequiredService<IRepository<SubcontractingReceipt, Guid>>();

            var company = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "SCR Sec RM Co") { CurrencyCode = "MYR" }, autoSave: true);
            await SetupAccountingDefaultsAsync(accountRepo, fyRepo, ruleRepo, company.Id, "SCRRM");
            await seriesRepo.InsertAsync(new DocumentSeries(Guid.NewGuid(), company.Id, "SCR Series", "SubcontractingReceipt", "SCRRM-"), autoSave: true);

            var supplier = await supplierRepo.InsertAsync(new Supplier(Guid.NewGuid(), company.Id, "SCR Sec Supp"), autoSave: true);
            var wh = await whRepo.InsertAsync(new Warehouse(Guid.NewGuid(), company.Id, "FG Wh RM"), autoSave: true);
            var suppWh = await whRepo.InsertAsync(new Warehouse(Guid.NewGuid(), company.Id, "Supp Wh RM"), autoSave: true);

            var fgItem = await itemRepo.InsertAsync(new Item(Guid.NewGuid(), company.Id, "FG-SEC-1", "FG Item", ItemType.Goods) { MaintainStock = false, AllowNegativeStock = true }, autoSave: true);
            var scrapItem = await itemRepo.InsertAsync(new Item(Guid.NewGuid(), company.Id, "SCRAP-SEC-1", "Scrap Item", ItemType.Goods) { MaintainStock = false, AllowNegativeStock = true }, autoSave: true);
            var rmItem = await itemRepo.InsertAsync(new Item(Guid.NewGuid(), company.Id, "RM-SEC-1", "RM Item", ItemType.Goods) { MaintainStock = false, AllowNegativeStock = true }, autoSave: true);

            // SCO for 10 FG requiring 20 RM (2 RM per FG)
            var sco = new SubcontractingOrder(Guid.NewGuid(), company.Id, "SCO-RM-01", new DateTime(2026, 6, 1), supplier.Id, company.TenantId)
            {
                SupplierWarehouseId = suppWh.Id
            };
            sco.AddItem(new SubcontractingOrderItem(Guid.NewGuid(), sco.Id, fgItem.Id, "FG Item", 10m, fgRate));
            sco.AddSuppliedItem(new SubcontractingOrderSuppliedItem(Guid.NewGuid(), sco.Id, rmItem.Id, "RM Item", 20m)
            {
                ReserveWarehouseId = suppWh.Id
            });
            sco.Submit();
            await scoRepo.InsertAsync(sco, autoSave: true);
            scoId = sco.Id;

            // Receipt receives 5 FG + 3 Scrap secondary item
            var scr = new SubcontractingReceipt(Guid.NewGuid(), company.Id, "SCR-RM-01", new DateTime(2026, 6, 2), supplier.Id, sco.Id, company.TenantId)
            {
                WarehouseId = wh.Id,
                SupplierWarehouseId = suppWh.Id
            };
            scr.AddItem(new SubcontractingReceiptItem(Guid.NewGuid(), scr.Id, fgItem.Id, "FG Item", 5m, fgRate) { WarehouseId = wh.Id });
            scr.AddItem(new SubcontractingReceiptItem(Guid.NewGuid(), scr.Id, scrapItem.Id, "Scrap Item", 3m, 10m)
            {
                WarehouseId = wh.Id,
                SecondaryItemType = "Scrap"
            });
            await scrRepo.InsertAsync(scr, autoSave: true);
            scrId = scr.Id;
        });

        var subAppService = GetRequiredService<ISubcontractingAppService>();
        await subAppService.SubmitReceiptAsync(scrId);

        // RM consumed must be 5/10 * 20 = 10 (NOT (5+3)/10 * 20 = 16); SCO PerReceived must be 50%
        await WithUnitOfWorkAsync(async () =>
        {
            var scoRepo = GetRequiredService<IRepository<SubcontractingOrder, Guid>>();
            var sco = await scoRepo.GetAsync(scoId, includeDetails: true);
            sco.Items.Single().ReceivedQty.ShouldBe(5m);
            sco.PerReceived.ShouldBe(50m);
            sco.SuppliedItems.Single().ConsumedQty.ShouldBe(10m);
        });

        await subAppService.CancelReceiptAsync(scrId);

        await WithUnitOfWorkAsync(async () =>
        {
            var scoRepo = GetRequiredService<IRepository<SubcontractingOrder, Guid>>();
            var sco = await scoRepo.GetAsync(scoId, includeDetails: true);
            sco.Items.Single().ReceivedQty.ShouldBe(0m);
            sco.SuppliedItems.Single().ConsumedQty.ShouldBe(0m);
        });
    }

    [Fact]
    public async Task CreateReceiptReturnAsync_RebuildsSecondaryItems_AcrossPartialFinalRejectedAndCancelledReturns()
    {
        Guid scrId = default, fgItemId = default, scrapItemId = default, whId = default, rejWhId = default, suppWhId = default;

        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var supplierRepo = GetRequiredService<IRepository<Supplier, Guid>>();
            var whRepo = GetRequiredService<IRepository<Warehouse, Guid>>();
            var itemRepo = GetRequiredService<IRepository<Item, Guid>>();
            var seriesRepo = GetRequiredService<IRepository<DocumentSeries, Guid>>();
            var accountRepo = GetRequiredService<IRepository<Account, Guid>>();
            var fyRepo = GetRequiredService<IRepository<FiscalYear, Guid>>();
            var ruleRepo = GetRequiredService<IRepository<AccountingRule, Guid>>();
            var scoRepo = GetRequiredService<IRepository<SubcontractingOrder, Guid>>();
            var scrRepo = GetRequiredService<IRepository<SubcontractingReceipt, Guid>>();

            var company = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "SCR Sec Return Co") { CurrencyCode = "MYR" }, autoSave: true);
            await SetupAccountingDefaultsAsync(accountRepo, fyRepo, ruleRepo, company.Id, "SCRRET");
            await seriesRepo.InsertAsync(new DocumentSeries(Guid.NewGuid(), company.Id, "SCR RET Series", "SCR-RET", "SCRRET-"), autoSave: true);

            var supplier = await supplierRepo.InsertAsync(new Supplier(Guid.NewGuid(), company.Id, "SCR Sec Ret Supp"), autoSave: true);
            var wh = await whRepo.InsertAsync(new Warehouse(Guid.NewGuid(), company.Id, "FG Wh Sec"), autoSave: true);
            var rejWh = await whRepo.InsertAsync(new Warehouse(Guid.NewGuid(), company.Id, "Rej Wh Sec"), autoSave: true);
            var suppWh = await whRepo.InsertAsync(new Warehouse(Guid.NewGuid(), company.Id, "Supp Wh Sec"), autoSave: true);

            var fgItem = await itemRepo.InsertAsync(new Item(Guid.NewGuid(), company.Id, "FG-RET-1", "FG Ret", ItemType.Goods) { MaintainStock = false, AllowNegativeStock = true }, autoSave: true);
            var scrapItem = await itemRepo.InsertAsync(new Item(Guid.NewGuid(), company.Id, "SCRAP-RET-1", "Scrap Ret", ItemType.Goods) { MaintainStock = false, AllowNegativeStock = true }, autoSave: true);

            var sco = new SubcontractingOrder(Guid.NewGuid(), company.Id, "SCO-RET-01", new DateTime(2026, 6, 1), supplier.Id, company.TenantId)
            {
                SupplierWarehouseId = suppWh.Id
            };
            sco.AddItem(new SubcontractingOrderItem(Guid.NewGuid(), sco.Id, fgItem.Id, "FG Ret", 3m, 100m));
            sco.Submit();
            await scoRepo.InsertAsync(sco, autoSave: true);

            // Original receipt: 3 accepted FG + 1 rejected FG + 1 secondary Scrap (BomSecondaryItemId links BOM secondary row)
            var scr = new SubcontractingReceipt(Guid.NewGuid(), company.Id, "SCR-RET-ORIG", new DateTime(2026, 6, 2), supplier.Id, sco.Id, company.TenantId)
            {
                WarehouseId = wh.Id,
                SupplierWarehouseId = suppWh.Id
            };
            scr.AddItem(new SubcontractingReceiptItem(Guid.NewGuid(), scr.Id, fgItem.Id, "FG Ret", 3m, 100m)
            {
                WarehouseId = wh.Id,
                RejectedQty = 1m,
                RejectedWarehouseId = rejWh.Id
            });
            scr.AddItem(new SubcontractingReceiptItem(Guid.NewGuid(), scr.Id, scrapItem.Id, "Scrap Ret", 1m, 25m)
            {
                WarehouseId = wh.Id,
                SecondaryItemType = "Scrap",
                BomSecondaryItemId = Guid.NewGuid()
            });
            scr.Submit();
            await scrRepo.InsertAsync(scr, autoSave: true);

            scrId = scr.Id;
            fgItemId = fgItem.Id;
            scrapItemId = scrapItem.Id;
            whId = wh.Id;
            rejWhId = rejWh.Id;
            suppWhId = suppWh.Id;
        });

        var subAppService = GetRequiredService<ISubcontractingAppService>();

        // 1. Rejected-warehouse return yields NO secondary item
        var rejReturn = await subAppService.CreateReceiptReturnAsync(new CreateSubcontractingReceiptReturnDto
        {
            ReturnAgainstReceiptId = scrId,
            PostingDate = new DateTime(2026, 6, 3),
            WarehouseId = rejWhId,
            SupplierWarehouseId = suppWhId,
            Items = new List<CreateScrReturnItemDto>
            {
                new() { ItemId = fgItemId, ItemName = "FG Ret", Qty = 1m, Rate = 0m, WarehouseId = rejWhId, ReturnQtyFromRejectedWarehouse = true }
            }
        });
        rejReturn.Items.Count.ShouldBe(1);
        rejReturn.Items.Any(i => i.ItemId == scrapItemId).ShouldBeFalse();

        // 2. Partial accepted return 1 of 3 FG -> 0.3333 secondary qty; submit then cancel it
        var partialCancelled = await subAppService.CreateReceiptReturnAsync(new CreateSubcontractingReceiptReturnDto
        {
            ReturnAgainstReceiptId = scrId,
            PostingDate = new DateTime(2026, 6, 4),
            WarehouseId = whId,
            SupplierWarehouseId = suppWhId,
            Items = new List<CreateScrReturnItemDto>
            {
                new() { ItemId = fgItemId, ItemName = "FG Ret", Qty = 1m, Rate = 100m, WarehouseId = whId }
            }
        });
        partialCancelled.Items.Single(i => i.ItemId == scrapItemId).Qty.ShouldBe(-0.3333m);
        await subAppService.SubmitReceiptAsync(partialCancelled.Id);
        await subAppService.CancelReceiptAsync(partialCancelled.Id);

        // 3. Re-do partial accepted return 2 of 3 FG (since prior was cancelled, starts from 0 -> 2/3 = 0.6667)
        var partialRet1 = await subAppService.CreateReceiptReturnAsync(new CreateSubcontractingReceiptReturnDto
        {
            ReturnAgainstReceiptId = scrId,
            PostingDate = new DateTime(2026, 6, 5),
            WarehouseId = whId,
            SupplierWarehouseId = suppWhId,
            Items = new List<CreateScrReturnItemDto>
            {
                new() { ItemId = fgItemId, ItemName = "FG Ret", Qty = 2m, Rate = 100m, WarehouseId = whId }
            }
        });
        var sec1 = partialRet1.Items.Single(i => i.ItemId == scrapItemId);
        sec1.Qty.ShouldBe(-0.6667m);
        sec1.SecondaryItemType.ShouldBe("Scrap");
        await subAppService.SubmitReceiptAsync(partialRet1.Id);

        // 4. Final partial return (last 1 of 3 FG) takes exact remaining secondary qty (-0.3333 => -0.6667 + -0.3333 = -1.0000)
        var partialRet2 = await subAppService.CreateReceiptReturnAsync(new CreateSubcontractingReceiptReturnDto
        {
            ReturnAgainstReceiptId = scrId,
            PostingDate = new DateTime(2026, 6, 6),
            WarehouseId = whId,
            SupplierWarehouseId = suppWhId,
            Items = new List<CreateScrReturnItemDto>
            {
                new() { ItemId = fgItemId, ItemName = "FG Ret", Qty = 1m, Rate = 100m, WarehouseId = whId }
            }
        });
        var sec2 = partialRet2.Items.Single(i => i.ItemId == scrapItemId);
        sec2.Qty.ShouldBe(-0.3333m);
        (Math.Abs(sec1.Qty) + Math.Abs(sec2.Qty)).ShouldBe(1m);
    }

    [Fact]
    public async Task SubmitAndCancelReturn_ReturnsActualReceiptConsumedRm_AndCapsAcrossPartialReturns()
    {
        Guid scrId = default, scoId = default, rmItemId = default, fgItemId = default, whId = default, suppWhId = default;

        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var supplierRepo = GetRequiredService<IRepository<Supplier, Guid>>();
            var whRepo = GetRequiredService<IRepository<Warehouse, Guid>>();
            var itemRepo = GetRequiredService<IRepository<Item, Guid>>();
            var seriesRepo = GetRequiredService<IRepository<DocumentSeries, Guid>>();
            var accountRepo = GetRequiredService<IRepository<Account, Guid>>();
            var fyRepo = GetRequiredService<IRepository<FiscalYear, Guid>>();
            var ruleRepo = GetRequiredService<IRepository<AccountingRule, Guid>>();
            var scoRepo = GetRequiredService<IRepository<SubcontractingOrder, Guid>>();
            var scrRepo = GetRequiredService<IRepository<SubcontractingReceipt, Guid>>();
            var sleRepo = GetRequiredService<IRepository<StockLedgerEntry, Guid>>();

            var company = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "SCR PR66545 Co") { CurrencyCode = "MYR" }, autoSave: true);
            await SetupAccountingDefaultsAsync(accountRepo, fyRepo, ruleRepo, company.Id, "SCR66545");
            await seriesRepo.InsertAsync(new DocumentSeries(Guid.NewGuid(), company.Id, "SCR Series", "SubcontractingReceipt", "SCR66545-"), autoSave: true);

            var supplier = await supplierRepo.InsertAsync(new Supplier(Guid.NewGuid(), company.Id, "SCR 66545 Supp"), autoSave: true);
            var wh = await whRepo.InsertAsync(new Warehouse(Guid.NewGuid(), company.Id, "FG Wh 66545"), autoSave: true);
            var suppWh = await whRepo.InsertAsync(new Warehouse(Guid.NewGuid(), company.Id, "Supp Wh 66545"), autoSave: true);
            whId = wh.Id;
            suppWhId = suppWh.Id;

            var fgItem = await itemRepo.InsertAsync(new Item(Guid.NewGuid(), company.Id, "FG-66545", "FG 66545", ItemType.Goods) { MaintainStock = false, AllowNegativeStock = true }, autoSave: true);
            var rmItem = await itemRepo.InsertAsync(new Item(Guid.NewGuid(), company.Id, "RM-66545", "RM 66545", ItemType.Goods) { MaintainStock = true, AllowNegativeStock = true }, autoSave: true);
            fgItemId = fgItem.Id;
            rmItemId = rmItem.Id;

            // SCO BOM requires 10 RM for 10 FG, but actual receipt consumed 20 RM (e.g. extra transfer / non-BOM consumption)
            var sco = new SubcontractingOrder(Guid.NewGuid(), company.Id, "SCO-66545-01", new DateTime(2026, 6, 1), supplier.Id, company.TenantId)
            {
                SupplierWarehouseId = suppWhId
            };
            sco.AddItem(new SubcontractingOrderItem(Guid.NewGuid(), sco.Id, fgItemId, "FG 66545", 10m, 100m));
            var suppRow = new SubcontractingOrderSuppliedItem(Guid.NewGuid(), sco.Id, rmItemId, "RM 66545", 10m)
            {
                ReserveWarehouseId = suppWhId,
                ConsumedQty = 20m
            };
            sco.AddSuppliedItem(suppRow);
            sco.Submit();
            scoId = sco.Id;
            await scoRepo.InsertAsync(sco, autoSave: true);

            var scr = new SubcontractingReceipt(Guid.NewGuid(), company.Id, "SCR-66545-01", new DateTime(2026, 6, 2), supplier.Id, sco.Id, company.TenantId)
            {
                WarehouseId = whId,
                SupplierWarehouseId = suppWhId
            };
            scr.AddItem(new SubcontractingReceiptItem(Guid.NewGuid(), scr.Id, fgItemId, "FG 66545", 10m, 100m) { WarehouseId = whId });
            scr.Submit();
            scrId = scr.Id;
            await scrRepo.InsertAsync(scr, autoSave: true);

            // Original receipt actually consumed 20 RM (not SCO BOM 10 RM)
            await sleRepo.InsertAsync(new StockLedgerEntry(
                Guid.NewGuid(), company.Id, rmItemId, suppWhId,
                new DateTime(2026, 6, 2), TimeSpan.Zero, -20m, 5m, -20m, -100m,
                "SubcontractingReceipt", scr.Id, tenantId: company.TenantId), autoSave: true);
        });

        var subAppService = GetRequiredService<ISubcontractingAppService>();

        // Partial return of 4 out of 10 FG must take back 40% of receipt's 20 consumed RM = +8 RM (not BOM's 4 RM)
        var ret1 = await subAppService.CreateReceiptReturnAsync(new CreateSubcontractingReceiptReturnDto
        {
            ReturnAgainstReceiptId = scrId,
            PostingDate = new DateTime(2026, 6, 3),
            WarehouseId = whId,
            SupplierWarehouseId = suppWhId,
            Items = new List<CreateScrReturnItemDto>
            {
                new() { ItemId = fgItemId, ItemName = "FG Ret 1", Qty = 4m, Rate = 100m, WarehouseId = whId }
            }
        });
        await subAppService.SubmitReceiptAsync(ret1.Id);

        await WithUnitOfWorkAsync(async () =>
        {
            var sleRepo = GetRequiredService<IRepository<StockLedgerEntry, Guid>>();
            var scoRepo = GetRequiredService<IRepository<SubcontractingOrder, Guid>>();

            var ret1RmSle = (await sleRepo.GetListAsync(s => s.VoucherId == ret1.Id && s.ItemId == rmItemId)).Single();
            ret1RmSle.QuantityChange.ShouldBe(8m);

            var sco = await scoRepo.GetAsync(scoId);
            sco.SuppliedItems.Single(i => i.ItemId == rmItemId).ConsumedQty.ShouldBe(12m);
        });

        // Second partial return of 6 FG takes remaining 12 RM -> SCO ConsumedQty becomes 0
        var ret2 = await subAppService.CreateReceiptReturnAsync(new CreateSubcontractingReceiptReturnDto
        {
            ReturnAgainstReceiptId = scrId,
            PostingDate = new DateTime(2026, 6, 4),
            WarehouseId = whId,
            SupplierWarehouseId = suppWhId,
            Items = new List<CreateScrReturnItemDto>
            {
                new() { ItemId = fgItemId, ItemName = "FG Ret 2", Qty = 6m, Rate = 100m, WarehouseId = whId }
            }
        });
        await subAppService.SubmitReceiptAsync(ret2.Id);

        await WithUnitOfWorkAsync(async () =>
        {
            var sleRepo = GetRequiredService<IRepository<StockLedgerEntry, Guid>>();
            var scoRepo = GetRequiredService<IRepository<SubcontractingOrder, Guid>>();

            var ret2RmSle = (await sleRepo.GetListAsync(s => s.VoucherId == ret2.Id && s.ItemId == rmItemId)).Single();
            ret2RmSle.QuantityChange.ShouldBe(12m);

            var sco = await scoRepo.GetAsync(scoId);
            sco.SuppliedItems.Single(i => i.ItemId == rmItemId).ConsumedQty.ShouldBe(0m);
        });

        // Cancelling ret2 must reverse the exact 12 RM it returned (restoring ConsumedQty to 12m)
        await subAppService.CancelReceiptAsync(ret2.Id);

        await WithUnitOfWorkAsync(async () =>
        {
            var scoRepo = GetRequiredService<IRepository<SubcontractingOrder, Guid>>();
            var sco = await scoRepo.GetAsync(scoId);
            sco.SuppliedItems.Single(i => i.ItemId == rmItemId).ConsumedQty.ShouldBe(12m);
        });
    }
}
