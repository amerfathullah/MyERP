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
using MyERP.Purchasing.DTOs;
using MyERP.Purchasing.Entities;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Xunit;

namespace MyERP.Purchasing;

/// <summary>
/// Integration tests verifying combined Material Request receipt quantities across mixed receipts
/// (Purchase Receipt and stock-updating Purchase Invoice) per ERPNext PR #60208 / commit ba48a9d0ad.
/// </summary>
public abstract class MaterialRequestMixedReceiptsAppTests<TStartupModule> : MyERPApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    private sealed record Fixture(
        Company Company,
        Supplier Supplier,
        Warehouse Warehouse,
        Item Item,
        MaterialRequest MaterialRequest,
        MaterialRequestItem MrItem);

    private async Task<Fixture> SeedAsync(string tag, decimal requestQty = 20m)
    {
        var companyRepository = GetRequiredService<IRepository<Company, Guid>>();
        var supplierRepository = GetRequiredService<IRepository<Supplier, Guid>>();
        var warehouseRepository = GetRequiredService<IRepository<Warehouse, Guid>>();
        var warehouseAccountRepository = GetRequiredService<IRepository<WarehouseAccount, Guid>>();
        var accountRepository = GetRequiredService<IRepository<Account, Guid>>();
        var fiscalYearRepository = GetRequiredService<IRepository<FiscalYear, Guid>>();
        var ruleRepository = GetRequiredService<IRepository<AccountingRule, Guid>>();
        var costCenterRepository = GetRequiredService<IRepository<CostCenter, Guid>>();
        var seriesRepository = GetRequiredService<IRepository<DocumentSeries, Guid>>();
        var itemRepository = GetRequiredService<IRepository<Item, Guid>>();
        var mrRepository = GetRequiredService<IRepository<MaterialRequest, Guid>>();

        var company = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), $"Mixed MR Co {tag}"), autoSave: true);
        var supplier = await supplierRepository.InsertAsync(new Supplier(Guid.NewGuid(), company.Id, $"Mixed MR Supplier {tag}"), autoSave: true);
        var warehouse = await warehouseRepository.InsertAsync(new Warehouse(Guid.NewGuid(), company.Id, $"Mixed MR Wh {tag}"), autoSave: true);

        var stockAccount = await accountRepository.InsertAsync(
            new Account(Guid.NewGuid(), company.Id, $"1410-MR-{tag}", "Stock In Hand", AccountType.Asset), autoSave: true);
        var srbnbAccount = await accountRepository.InsertAsync(
            new Account(Guid.NewGuid(), company.Id, $"2110-MR-{tag}", "Stock Received But Not Billed", AccountType.Liability), autoSave: true);
        var payableAccount = await accountRepository.InsertAsync(
            new Account(Guid.NewGuid(), company.Id, $"2010-MR-{tag}", "Accounts Payable", AccountType.Liability), autoSave: true);
        var expenseAccount = await accountRepository.InsertAsync(
            new Account(Guid.NewGuid(), company.Id, $"5010-MR-{tag}", "Cost of Goods Sold", AccountType.Expense), autoSave: true);

        await warehouseAccountRepository.InsertAsync(
            new WarehouseAccount(Guid.NewGuid(), warehouse.Id, company.Id, stockAccount.Id), autoSave: true);

        var costCenter = await costCenterRepository.InsertAsync(
            new CostCenter(Guid.NewGuid(), company.Id, $"Mixed MR CC {tag}"), autoSave: true);

        company.DefaultInventoryAccountId = stockAccount.Id;
        company.StockReceivedButNotBilledAccountId = srbnbAccount.Id;
        company.DefaultPayableAccountId = payableAccount.Id;
        company.DefaultCostCenterId = costCenter.Id;
        await companyRepository.UpdateAsync(company, autoSave: true);

        await fiscalYearRepository.InsertAsync(
            new FiscalYear(Guid.NewGuid(), company.Id, $"FY MR {tag}", DateTime.UtcNow.Date.AddYears(-1), DateTime.UtcNow.Date.AddYears(1)),
            autoSave: true);

        await seriesRepository.InsertAsync(
            new DocumentSeries(Guid.NewGuid(), company.Id, "PR Series", "PurchaseReceipt", $"PRMR{tag}-"), autoSave: true);
        await seriesRepository.InsertAsync(
            new DocumentSeries(Guid.NewGuid(), company.Id, "PI Series", "PurchaseInvoice", $"PIMR{tag}-"), autoSave: true);

        await ruleRepository.InsertAsync(
            new AccountingRule(Guid.NewGuid(), company.Id, "PR DR Stock", "PurchaseReceipt", true, AccountSource.WarehouseStock, AmountSource.NetTotal)
            { SortOrder = 1, FixedAccountId = stockAccount.Id }, autoSave: true);
        await ruleRepository.InsertAsync(
            new AccountingRule(Guid.NewGuid(), company.Id, "PR CR SRBNB", "PurchaseReceipt", false, AccountSource.FixedAccount, AmountSource.NetTotal)
            { SortOrder = 2, FixedAccountId = srbnbAccount.Id }, autoSave: true);

        await ruleRepository.InsertAsync(
            new AccountingRule(Guid.NewGuid(), company.Id, "PI DR Inventory", "PurchaseInvoice", true, AccountSource.WarehouseStock, AmountSource.NetTotal)
            { SortOrder = 1, FixedAccountId = stockAccount.Id }, autoSave: true);
        await ruleRepository.InsertAsync(
            new AccountingRule(Guid.NewGuid(), company.Id, "PI CR Payable", "PurchaseInvoice", false, AccountSource.SupplierPayable, AmountSource.GrandTotal)
            { SortOrder = 2, FixedAccountId = payableAccount.Id }, autoSave: true);

        var item = await itemRepository.InsertAsync(
            new Item(Guid.NewGuid(), company.Id, $"ITEM-MR-{tag}", "Mixed MR Item", ItemType.Goods), autoSave: true);

        var mr = new MaterialRequest(Guid.NewGuid(), company.Id, $"MR-{tag}-001", MaterialRequestType.Purchase, DateTime.UtcNow.Date, company.TenantId);
        mr.AddItem(item.Id, "Mixed MR Item", quantity: requestQty, uom: "Unit", warehouseId: warehouse.Id);
        var mrItem = mr.Items.Single();
        mr.Submit();
        await mrRepository.InsertAsync(mr, autoSave: true);

        return new Fixture(company, supplier, warehouse, item, mr, mrItem);
    }

    [Fact]
    public async Task MixedReceipts_PR_And_StockUpdatingPI_CombinesReceivedQty_AndCompletesMr()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var f = await SeedAsync("COMBINE", 20m);
            var prAppService = GetRequiredService<IPurchaseReceiptAppService>();
            var piAppService = GetRequiredService<IPurchaseInvoiceAppService>();
            var mrAppService = GetRequiredService<IMaterialRequestAppService>();
            var mrRepository = GetRequiredService<IRepository<MaterialRequest, Guid>>();

            // 1. Create and submit Purchase Receipt for 12 units
            var pr = await prAppService.CreateAsync(new CreatePurchaseReceiptDto
            {
                CompanyId = f.Company.Id,
                SupplierId = f.Supplier.Id,
                WarehouseId = f.Warehouse.Id,
                PostingDate = DateTime.UtcNow.Date,
                Items = new List<CreatePurchaseReceiptItemDto>
                {
                    new()
                    {
                        ItemId = f.Item.Id,
                        Description = "Mixed MR Item",
                        Quantity = 12m,
                        UnitPrice = 10m,
                        Uom = "Unit",
                        WarehouseId = f.Warehouse.Id,
                        MaterialRequestItemId = f.MrItem.Id,
                    },
                },
            });
            await prAppService.SubmitAsync(pr.Id);

            var mrAfterPr = await mrRepository.GetAsync(f.MaterialRequest.Id);
            var mrItemAfterPr = mrAfterPr.Items.Single(i => i.Id == f.MrItem.Id);
            mrItemAfterPr.ReceivedQuantity.ShouldBe(12m);
            mrAfterPr.PerReceived.ShouldBe(60m);
            mrAfterPr.Status.ShouldBe(DocumentStatus.Submitted);

            // 2. Create and submit stock-updating Purchase Invoice for remaining 8 units
            var pi = await piAppService.CreateAsync(new CreatePurchaseInvoiceDto
            {
                CompanyId = f.Company.Id,
                SupplierId = f.Supplier.Id,
                WarehouseId = f.Warehouse.Id,
                IssueDate = DateTime.UtcNow.Date,
                DueDate = DateTime.UtcNow.Date.AddDays(30),
                UpdateStock = true,
                Items = new List<CreatePurchaseInvoiceItemDto>
                {
                    new()
                    {
                        ItemId = f.Item.Id,
                        Description = "Mixed MR Item",
                        Quantity = 8m,
                        UnitPrice = 10m,
                        Uom = "Unit",
                        WarehouseId = f.Warehouse.Id,
                        MaterialRequestItemId = f.MrItem.Id,
                    },
                },
            });
            await piAppService.SubmitAsync(pi.Id);

            var mrAfterPi = await mrRepository.GetAsync(f.MaterialRequest.Id);
            var mrItemAfterPi = mrAfterPi.Items.Single(i => i.Id == f.MrItem.Id);
            mrItemAfterPi.ReceivedQuantity.ShouldBe(20m);
            mrAfterPi.PerReceived.ShouldBe(100m);
            mrAfterPi.Status.ShouldBe(DocumentStatus.Completed);

            // Verify GetFulfillmentStatusAsync returns populated ReceivedQty and PerReceived
            var fulfillment = await mrAppService.GetFulfillmentStatusAsync(f.MaterialRequest.Id);
            fulfillment.IsFullyFulfilled.ShouldBeTrue();
            var itemFulfillment = fulfillment.Items.Single(i => i.ItemId == f.Item.Id);
            itemFulfillment.ReceivedQty.ShouldBe(20m);
            itemFulfillment.PerReceived.ShouldBe(100m);
        });
    }

    [Fact]
    public async Task MixedReceipts_CancelReversesQuantities_AndRevertsStatus()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var f = await SeedAsync("CANCEL", 20m);
            var prAppService = GetRequiredService<IPurchaseReceiptAppService>();
            var piAppService = GetRequiredService<IPurchaseInvoiceAppService>();
            var mrRepository = GetRequiredService<IRepository<MaterialRequest, Guid>>();

            // 1. Submit PR (12)
            var pr = await prAppService.CreateAsync(new CreatePurchaseReceiptDto
            {
                CompanyId = f.Company.Id,
                SupplierId = f.Supplier.Id,
                WarehouseId = f.Warehouse.Id,
                PostingDate = DateTime.UtcNow.Date,
                Items = new List<CreatePurchaseReceiptItemDto>
                {
                    new()
                    {
                        ItemId = f.Item.Id,
                        Description = "Mixed MR Item",
                        Quantity = 12m,
                        UnitPrice = 10m,
                        Uom = "Unit",
                        WarehouseId = f.Warehouse.Id,
                        MaterialRequestItemId = f.MrItem.Id,
                    },
                },
            });
            await prAppService.SubmitAsync(pr.Id);

            // 2. Submit PI with update_stock (8) -> MR completed
            var pi = await piAppService.CreateAsync(new CreatePurchaseInvoiceDto
            {
                CompanyId = f.Company.Id,
                SupplierId = f.Supplier.Id,
                WarehouseId = f.Warehouse.Id,
                IssueDate = DateTime.UtcNow.Date,
                DueDate = DateTime.UtcNow.Date.AddDays(30),
                UpdateStock = true,
                Items = new List<CreatePurchaseInvoiceItemDto>
                {
                    new()
                    {
                        ItemId = f.Item.Id,
                        Description = "Mixed MR Item",
                        Quantity = 8m,
                        UnitPrice = 10m,
                        Uom = "Unit",
                        WarehouseId = f.Warehouse.Id,
                        MaterialRequestItemId = f.MrItem.Id,
                    },
                },
            });
            await piAppService.SubmitAsync(pi.Id);
            await piAppService.PostAsync(pi.Id);

            var mrCompleted = await mrRepository.GetAsync(f.MaterialRequest.Id);
            mrCompleted.Status.ShouldBe(DocumentStatus.Completed);
            mrCompleted.PerReceived.ShouldBe(100m);

            // 3. Cancel PI (8) -> MR reverts to Submitted, ReceivedQty back to 12
            await piAppService.CancelAsync(pi.Id);

            var mrAfterPiCancel = await mrRepository.GetAsync(f.MaterialRequest.Id);
            var mrItemAfterPiCancel = mrAfterPiCancel.Items.Single(i => i.Id == f.MrItem.Id);
            mrItemAfterPiCancel.ReceivedQuantity.ShouldBe(12m);
            mrAfterPiCancel.PerReceived.ShouldBe(60m);
            mrAfterPiCancel.Status.ShouldBe(DocumentStatus.Submitted);

            // 4. Cancel PR (12) -> ReceivedQty back to 0, PerReceived back to 0
            await prAppService.CancelAsync(pr.Id);

            var mrAfterPrCancel = await mrRepository.GetAsync(f.MaterialRequest.Id);
            var mrItemAfterPrCancel = mrAfterPrCancel.Items.Single(i => i.Id == f.MrItem.Id);
            mrItemAfterPrCancel.ReceivedQuantity.ShouldBe(0m);
            mrAfterPrCancel.PerReceived.ShouldBe(0m);
            mrAfterPrCancel.Status.ShouldBe(DocumentStatus.Submitted);
        });
    }
}
