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
using MyERP.Sales.Entities;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Xunit;

namespace MyERP.Sales;

/// <summary>
/// Integration tests for ERPNext PR #60607 settled Return Delivery Note billing status:
/// - Returns deducted on the original DN invoice or full returns of unbilled DNs mark Completed (100%).
/// - Returns made after the original DN was already fully billed still require a Credit Note.
/// - Cancelling the original invoice reverts settled return status back to 0% / Return.
/// - Multi-UOM returns convert accurately between original and return row UOMs.
/// - Direct Sales Order over-billing prevents over-billed returns from settling and recalculates when sibling deliveries arrive.
/// </summary>
public abstract class DeliveryNoteSettledReturnBillingTests<TStartupModule> : MyERPApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    private async Task<(Company Co, Customer Cust, Warehouse Wh, Item Item)> SeedFixtureAsync(string tag)
    {
        var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
        var custRepo = GetRequiredService<IRepository<Customer, Guid>>();
        var whRepo = GetRequiredService<IRepository<Warehouse, Guid>>();
        var itemRepo = GetRequiredService<IRepository<Item, Guid>>();
        var acctRepo = GetRequiredService<IRepository<Account, Guid>>();
        var ccRepo = GetRequiredService<IRepository<CostCenter, Guid>>();
        var ruleRepo = GetRequiredService<IRepository<AccountingRule, Guid>>();
        var fyRepo = GetRequiredService<IRepository<FiscalYear, Guid>>();
        var seriesRepo = GetRequiredService<IRepository<DocumentSeries, Guid>>();
        var binRepo = GetRequiredService<IRepository<Bin, Guid>>();

        var company = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), $"SRB Co {tag}"), autoSave: true);
        var ar = await acctRepo.InsertAsync(new Account(Guid.NewGuid(), company.Id, $"1200-{tag}", "AR", AccountType.Asset) { AccountSubType = AccountSubType.AccountsReceivable }, autoSave: true);
        var rev = await acctRepo.InsertAsync(new Account(Guid.NewGuid(), company.Id, $"4000-{tag}", "Sales", AccountType.Revenue), autoSave: true);
        var stock = await acctRepo.InsertAsync(new Account(Guid.NewGuid(), company.Id, $"1400-{tag}", "Stock", AccountType.Asset) { AccountSubType = AccountSubType.Stock }, autoSave: true);
        var cogs = await acctRepo.InsertAsync(new Account(Guid.NewGuid(), company.Id, $"5000-{tag}", "COGS", AccountType.Expense) { AccountSubType = AccountSubType.CostOfGoodsSold }, autoSave: true);
        var costCenter = await ccRepo.InsertAsync(new CostCenter(Guid.NewGuid(), company.Id, $"CC-{tag}"), autoSave: true);

        company.DefaultReceivableAccountId = ar.Id;
        company.DefaultIncomeAccountId = rev.Id;
        company.DefaultInventoryAccountId = stock.Id;
        company.DefaultExpenseAccountId = cogs.Id;
        company.DefaultCostCenterId = costCenter.Id;
        await companyRepo.UpdateAsync(company, autoSave: true);

        await ruleRepo.InsertAsync(new AccountingRule(Guid.NewGuid(), company.Id, $"DN DR COGS {tag}", "DeliveryNote", true, AccountSource.ItemExpense, AmountSource.NetTotal) { SortOrder = 1 }, autoSave: true);
        await ruleRepo.InsertAsync(new AccountingRule(Guid.NewGuid(), company.Id, $"DN CR Stock {tag}", "DeliveryNote", false, AccountSource.FixedAccount, AmountSource.NetTotal) { SortOrder = 2, FixedAccountId = stock.Id }, autoSave: true);
        await ruleRepo.InsertAsync(new AccountingRule(Guid.NewGuid(), company.Id, $"SI DR AR {tag}", "SalesInvoice", true, AccountSource.FixedAccount, AmountSource.GrandTotal) { SortOrder = 1, FixedAccountId = ar.Id }, autoSave: true);
        await ruleRepo.InsertAsync(new AccountingRule(Guid.NewGuid(), company.Id, $"SI CR Rev {tag}", "SalesInvoice", false, AccountSource.FixedAccount, AmountSource.GrandTotal) { SortOrder = 2, FixedAccountId = rev.Id }, autoSave: true);

        await fyRepo.InsertAsync(new FiscalYear(
            Guid.NewGuid(), company.Id, $"FY-{tag}",
            DateTime.UtcNow.Date.AddYears(-1), DateTime.UtcNow.Date.AddYears(1)), autoSave: true);

        await seriesRepo.InsertAsync(new DocumentSeries(Guid.NewGuid(), company.Id, $"DN-{tag}", "DeliveryNote", $"DN{tag}-"), autoSave: true);
        await seriesRepo.InsertAsync(new DocumentSeries(Guid.NewGuid(), company.Id, $"SI-{tag}", "SalesInvoice", $"SI{tag}-"), autoSave: true);

        var cust = await custRepo.InsertAsync(new Customer(Guid.NewGuid(), company.Id, $"SRB Cust {tag}"), autoSave: true);
        var wh = await whRepo.InsertAsync(new Warehouse(Guid.NewGuid(), company.Id, $"SRB WH {tag}"), autoSave: true);
        var item = await itemRepo.InsertAsync(new Item(Guid.NewGuid(), company.Id, $"SRB-ITEM-{tag}", "Settled Return Item", ItemType.Goods)
        {
            MaintainStock = true,
            StandardBuyingPrice = 50m,
            StandardSellingPrice = 100m,
            Uom = "Nos"
        }, autoSave: true);

        var bin = new Bin(Guid.NewGuid(), item.Id, wh.Id);
        bin.ApplyStockMovement(100m, 5000m);
        await binRepo.InsertAsync(bin, autoSave: true);

        var valuationService = GetRequiredService<Inventory.DomainServices.StockValuationService>();
        await valuationService.CreateLedgerEntryAsync(
            company.Id, item.Id, wh.Id,
            DateTime.UtcNow.Date.AddDays(-5), 100m, 50m,
            voucherType: "PurchaseReceipt", voucherId: Guid.NewGuid());

        return (company, cust, wh, item);
    }

    [Fact]
    public async Task ReturnDeductedOnOriginalInvoice_BecomesCompleted_AndRevertsOnInvoiceCancel()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var (company, cust, wh, item) = await SeedFixtureAsync("DED1");
            var dnAppService = GetRequiredService<IDeliveryNoteAppService>();
            var siAppService = GetRequiredService<ISalesInvoiceAppService>();
            var conversionService = GetRequiredService<DocumentConversionAppService>();
            var dnRepo = GetRequiredService<IRepository<DeliveryNote, Guid>>();
            var siRepo = GetRequiredService<IRepository<SalesInvoice, Guid>>();

            var dn = await dnAppService.CreateAsync(new CreateDeliveryNoteDto
            {
                CompanyId = company.Id,
                CustomerId = cust.Id,
                WarehouseId = wh.Id,
                PostingDate = DateTime.UtcNow.Date,
                Items = [new() { ItemId = item.Id, Description = "Item", Quantity = 10m, UnitPrice = 100m, Uom = "Nos" }]
            });
            await dnAppService.SubmitAsync(dn.Id);

            // Partial return before invoicing DN
            var retDraft = await conversionService.MakeDeliveryNoteReturnAsync(dn.Id);
            var retEntity = await dnRepo.GetAsync(retDraft.Id, includeDetails: true);
            retEntity.Items.First().Quantity = -2m;
            await dnRepo.UpdateAsync(retEntity, autoSave: true);
            await dnAppService.SubmitAsync(retDraft.Id);

            // Invoice remaining net 8 on original DN
            var invDraft = await conversionService.ConvertDeliveryNoteToSalesInvoiceAsync(dn.Id);
            invDraft.Items.Single().Quantity.ShouldBe(8m);
            await siAppService.SubmitAsync(invDraft.Id);
            await siAppService.PostAsync(invDraft.Id);

            var settledReturn = await dnRepo.GetAsync(retDraft.Id, includeDetails: true);
            settledReturn.Items.First().SettledQty.ShouldBe(2m);
            settledReturn.PerBilled.ShouldBe(100m);
            settledReturn.BillingStatus.ShouldBe("Completed");

            // Settled return cannot produce another Credit Note
            var ex = await Should.ThrowAsync<BusinessException>(() =>
                conversionService.ConvertDeliveryNoteToSalesInvoiceAsync(retDraft.Id));
            ex.Code.ShouldBe(MyERPDomainErrorCodes.DocumentAlreadyConverted);

            // A return created AFTER the invoice already billed 8 is NOT settled and still needs a Credit Note
            var laterRet = await dnAppService.CreateAsync(new CreateDeliveryNoteDto
            {
                CompanyId = company.Id,
                CustomerId = cust.Id,
                WarehouseId = wh.Id,
                IsReturn = true,
                ReturnAgainstId = dn.Id,
                PostingDate = DateTime.UtcNow.Date,
                Items =
                [
                    new()
                    {
                        ItemId = item.Id,
                        Description = "Later Return",
                        Quantity = 3m,
                        UnitPrice = 100m,
                        Uom = "Nos",
                        OriginalDeliveryNoteItemId = dn.Items.First().Id
                    }
                ]
            });
            await dnAppService.SubmitAsync(laterRet.Id);
            var submittedLaterRet = await dnRepo.GetAsync(laterRet.Id, includeDetails: true);
            submittedLaterRet.Items.First().SettledQty.ShouldBe(0m);
            var laterCn = await conversionService.ConvertDeliveryNoteToSalesInvoiceAsync(laterRet.Id);
            laterCn.IsReturn.ShouldBeTrue();
            laterCn.Items.Single().Quantity.ShouldBe(-3m);

            // Cancelling the original invoice reverts the earlier deducted return back to unsettled (0% / Return)
            await siAppService.CancelAsync(invDraft.Id);
            settledReturn = await dnRepo.GetAsync(retDraft.Id, includeDetails: true);
            settledReturn.Items.First().SettledQty.ShouldBe(0m);
            settledReturn.PerBilled.ShouldBe(0m);
            settledReturn.BillingStatus.ShouldBe("Return");
        });
    }

    [Fact]
    public async Task FullReturnOfUnbilledDeliveryNote_IsSettledImmediately_WithNoCreditNoteDue()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var (company, cust, wh, item) = await SeedFixtureAsync("FULL1");
            var dnAppService = GetRequiredService<IDeliveryNoteAppService>();
            var conversionService = GetRequiredService<DocumentConversionAppService>();
            var dnRepo = GetRequiredService<IRepository<DeliveryNote, Guid>>();
            var siRepo = GetRequiredService<IRepository<SalesInvoice, Guid>>();

            var dn = await dnAppService.CreateAsync(new CreateDeliveryNoteDto
            {
                CompanyId = company.Id,
                CustomerId = cust.Id,
                WarehouseId = wh.Id,
                PostingDate = DateTime.UtcNow.Date,
                Items = [new() { ItemId = item.Id, Description = "Item", Quantity = 10m, UnitPrice = 100m, Uom = "Nos" }]
            });
            await dnAppService.SubmitAsync(dn.Id);

            var fullRet = await conversionService.MakeDeliveryNoteReturnAsync(dn.Id);
            await dnAppService.SubmitAsync(fullRet.Id);

            var submittedRet = await dnRepo.GetAsync(fullRet.Id, includeDetails: true);
            submittedRet.Items.First().SettledQty.ShouldBe(10m);
            submittedRet.PerBilled.ShouldBe(100m);
            submittedRet.BillingStatus.ShouldBe("Completed");

            // Original unbilled fully returned DN stays "Return Issued" with PerBilled = 0
            var orig = await dnRepo.GetAsync(dn.Id, includeDetails: true);
            orig.PerBilled.ShouldBe(0m);
            orig.BillingStatus.ShouldBe("Return Issued");

            // No bogus standalone Credit Note auto-created for an unbilled delivery note
            var allSis = await siRepo.GetListAsync(s => s.CompanyId == company.Id);
            allSis.ShouldBeEmpty();

            await Should.ThrowAsync<BusinessException>(() =>
                conversionService.ConvertDeliveryNoteToSalesInvoiceAsync(fullRet.Id));
        });
    }

    [Fact]
    public async Task ReturnInDifferentUom_DeductedOnOriginalInvoice_BecomesSettledAndCompleted()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var (company, cust, wh, item) = await SeedFixtureAsync("UOM1");
            var dnAppService = GetRequiredService<IDeliveryNoteAppService>();
            var siAppService = GetRequiredService<ISalesInvoiceAppService>();
            var conversionService = GetRequiredService<DocumentConversionAppService>();
            var dnRepo = GetRequiredService<IRepository<DeliveryNote, Guid>>();
            var uomRepo = GetRequiredService<IRepository<Uom, Guid>>();

            await uomRepo.InsertAsync(new Uom(Guid.NewGuid(), "Box") { MustBeWholeNumber = true }, autoSave: true);

            var dn = await dnAppService.CreateAsync(new CreateDeliveryNoteDto
            {
                CompanyId = company.Id,
                CustomerId = cust.Id,
                WarehouseId = wh.Id,
                PostingDate = DateTime.UtcNow.Date,
                Items = [new() { ItemId = item.Id, Description = "Item", Quantity = 2m, UnitPrice = 1000m, Uom = "Box", ConversionFactor = 10m }]
            });
            await dnAppService.SubmitAsync(dn.Id);

            // Return 10 Nos (= 1 Box) against the 2 Box delivery
            var ret = await dnAppService.CreateAsync(new CreateDeliveryNoteDto
            {
                CompanyId = company.Id,
                CustomerId = cust.Id,
                WarehouseId = wh.Id,
                IsReturn = true,
                ReturnAgainstId = dn.Id,
                PostingDate = DateTime.UtcNow.Date,
                Items =
                [
                    new()
                    {
                        ItemId = item.Id,
                        Description = "Item",
                        Quantity = 10m,
                        UnitPrice = 100m,
                        Uom = "Nos",
                        ConversionFactor = 1m,
                        OriginalDeliveryNoteItemId = dn.Items.First().Id
                    }
                ]
            });
            await dnAppService.SubmitAsync(ret.Id);

            var inv = await conversionService.ConvertDeliveryNoteToSalesInvoiceAsync(dn.Id);
            inv.Items.Single().Quantity.ShouldBe(1m);
            await siAppService.SubmitAsync(inv.Id);

            var settledRet = await dnRepo.GetAsync(ret.Id, includeDetails: true);
            settledRet.Items.First().SettledQty.ShouldBe(10m);
            settledRet.PerBilled.ShouldBe(100m);
            settledRet.BillingStatus.ShouldBe("Completed");
        });
    }

    [Fact]
    public async Task OverbilledSalesOrder_WithholdsSettlementUntilSiblingDeliveryCoversOverbilling()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var (company, cust, wh, item) = await SeedFixtureAsync("SOOV1");
            var soRepo = GetRequiredService<IRepository<SalesOrder, Guid>>();
            var dnRepo = GetRequiredService<IRepository<DeliveryNote, Guid>>();
            var dnAppService = GetRequiredService<IDeliveryNoteAppService>();
            var siAppService = GetRequiredService<ISalesInvoiceAppService>();
            var conversionService = GetRequiredService<DocumentConversionAppService>();

            var so = new SalesOrder(Guid.NewGuid(), company.Id, cust.Id, "SO-SRB-1", DateTime.UtcNow.Date);
            so.AddItem(item.Id, "SO Item", 10m, 100m, 0m);
            so.Submit();
            await soRepo.InsertAsync(so, autoSave: true);
            var soItemId = so.Items.First().Id;

            // DN 1 delivers 5 against SO; return 2 against DN 1; bill DN 1 for net 3 -> settled = 2
            var dn1 = await dnAppService.CreateAsync(new CreateDeliveryNoteDto
            {
                CompanyId = company.Id,
                CustomerId = cust.Id,
                WarehouseId = wh.Id,
                SalesOrderId = so.Id,
                PostingDate = DateTime.UtcNow.Date,
                Items = [new() { ItemId = item.Id, Description = "SO Item", Quantity = 5m, UnitPrice = 100m, Uom = "Nos", SalesOrderItemId = soItemId }]
            });
            await dnAppService.SubmitAsync(dn1.Id);

            var ret1 = await conversionService.MakeDeliveryNoteReturnAsync(dn1.Id);
            var ret1Entity = await dnRepo.GetAsync(ret1.Id, includeDetails: true);
            ret1Entity.Items.First().Quantity = -2m;
            await dnRepo.UpdateAsync(ret1Entity, autoSave: true);
            await dnAppService.SubmitAsync(ret1.Id);

            var dn1Inv = await conversionService.ConvertDeliveryNoteToSalesInvoiceAsync(dn1.Id);
            await siAppService.SubmitAsync(dn1Inv.Id);

            (await dnRepo.GetAsync(ret1.Id)).PerBilled.ShouldBe(100m);

            // Direct SO invoice bills 5 on top (8 total billed vs net delivered 3 => overbilled across SO)
            var soInv = await siAppService.CreateAsync(new CreateSalesInvoiceDto
            {
                CompanyId = company.Id,
                CustomerId = cust.Id,
                IssueDate = DateTime.UtcNow.Date,
                UpdateStock = false,
                Items = [new() { ItemId = item.Id, Description = "SO Item", Quantity = 5m, UnitPrice = 100m, Uom = "Nos", SalesOrderItemId = soItemId }]
            });
            await siAppService.SubmitAsync(soInv.Id);

            // Overbilled SO covers ret1, so ret1 becomes unsettled (0% / Return)
            var retAfterOverbill = await dnRepo.GetAsync(ret1.Id, includeDetails: true);
            retAfterOverbill.Items.First().SettledQty.ShouldBe(0m);
            retAfterOverbill.PerBilled.ShouldBe(0m);

            // Submitting sibling DN 2 (5 more units delivered against SO => net delivered = 5 - 2 + 5 = 8 == billed 8)
            // absorbs the SO billing so ret1 is settled again (100% Completed)
            var dn2 = await dnAppService.CreateAsync(new CreateDeliveryNoteDto
            {
                CompanyId = company.Id,
                CustomerId = cust.Id,
                WarehouseId = wh.Id,
                SalesOrderId = so.Id,
                PostingDate = DateTime.UtcNow.Date,
                Items = [new() { ItemId = item.Id, Description = "SO Item", Quantity = 5m, UnitPrice = 100m, Uom = "Nos", SalesOrderItemId = soItemId }]
            });
            await dnAppService.SubmitAsync(dn2.Id);

            var retAfterSiblingDn = await dnRepo.GetAsync(ret1.Id, includeDetails: true);
            retAfterSiblingDn.Items.First().SettledQty.ShouldBe(2m);
            retAfterSiblingDn.PerBilled.ShouldBe(100m);
            retAfterSiblingDn.BillingStatus.ShouldBe("Completed");
        });
    }
}
