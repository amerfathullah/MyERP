using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Accounting;
using MyERP.Accounting.Entities;
using MyERP.Assets.Entities;
using MyERP.Core.Entities;
using MyERP.Inventory;
using MyERP.Inventory.Entities;
using MyERP.Purchasing;
using MyERP.Purchasing.Entities;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Xunit;

namespace MyERP.Assets;

/// <summary>
/// Regression coverage for a real gap found via ERPNext validate() parity: asset_repair.py
/// validate_purchase_invoice_repair_cost() / get_unallocated_repair_cost() cap the repair cost
/// claimed against a (Purchase Invoice, Expense Account) pair to what's actually posted to GL for
/// that pair, minus whatever other submitted Asset Repair documents already claimed against it.
/// AssetRepairAppService had no such cap — only an intra-request duplicate-row check — so two
/// separate Asset Repair documents could each claim the full invoice expense and both capitalize it
/// onto their own asset's book value, double-counting the same underlying Purchase Invoice cost.
/// </summary>
public abstract class AssetRepairCostAllocationTests<TStartupModule> : MyERPApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    private sealed record Fixture(Company Company, Guid ExpenseAccountId, Guid Asset1Id, Guid Asset2Id, Guid InvoiceId);

    /// <summary>Seeds a company with a real, GL-posted Purchase Invoice (net total = 100) and two assets to repair.</summary>
    private async Task<Fixture> SeedAsync(string tag)
    {
        var companyRepository = GetRequiredService<IRepository<Company, Guid>>();
        var supplierRepository = GetRequiredService<IRepository<Supplier, Guid>>();
        var accountRepository = GetRequiredService<IRepository<Account, Guid>>();
        var fiscalYearRepository = GetRequiredService<IRepository<FiscalYear, Guid>>();
        var ruleRepository = GetRequiredService<IRepository<AccountingRule, Guid>>();
        var seriesRepository = GetRequiredService<IRepository<DocumentSeries, Guid>>();
        var costCenterRepository = GetRequiredService<IRepository<CostCenter, Guid>>();
        var itemRepository = GetRequiredService<IRepository<Item, Guid>>();
        var assetRepository = GetRequiredService<IRepository<Asset, Guid>>();
        var invoiceAppService = GetRequiredService<IPurchaseInvoiceAppService>();

        var company = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), $"Asset Repair Cap Co {tag}"), autoSave: true);
        var supplier = await supplierRepository.InsertAsync(new Supplier(Guid.NewGuid(), company.Id, $"Asset Repair Cap Supplier {tag}"), autoSave: true);

        var expenseAccount = await accountRepository.InsertAsync(
            new Account(Guid.NewGuid(), company.Id, $"5920-{tag}", "Repair Expense", AccountType.Expense), autoSave: true);
        var payableAccount = await accountRepository.InsertAsync(
            new Account(Guid.NewGuid(), company.Id, $"2020-{tag}", "Creditors", AccountType.Liability), autoSave: true);

        var costCenter = await costCenterRepository.InsertAsync(
            new CostCenter(Guid.NewGuid(), company.Id, $"Asset Repair Cap Cost Center {tag}"), autoSave: true);

        company.DefaultExpenseAccountId = expenseAccount.Id;
        company.DefaultPayableAccountId = payableAccount.Id;
        company.DefaultCostCenterId = costCenter.Id;
        await companyRepository.UpdateAsync(company, autoSave: true);

        await fiscalYearRepository.InsertAsync(
            new FiscalYear(Guid.NewGuid(), company.Id, $"FY {tag}", DateTime.UtcNow.Date.AddYears(-1), DateTime.UtcNow.Date.AddYears(1)),
            autoSave: true);
        await seriesRepository.InsertAsync(
            new DocumentSeries(Guid.NewGuid(), company.Id, $"AR Cap Series {tag}", "PurchaseInvoice", $"ARCAP{tag}-"), autoSave: true);

        // Same rules a real company is seeded with: expense debit, payable credit.
        await ruleRepository.InsertAsync(
            new AccountingRule(Guid.NewGuid(), company.Id, "PI DR Expense", "PurchaseInvoice", true, AccountSource.ItemExpense, AmountSource.NetTotal)
            { SortOrder = 1 }, autoSave: true);
        await ruleRepository.InsertAsync(
            new AccountingRule(Guid.NewGuid(), company.Id, "PI CR Payable", "PurchaseInvoice", false, AccountSource.SupplierPayable, AmountSource.GrandTotal)
            { SortOrder = 2 }, autoSave: true);

        var serviceItem = new Item(Guid.NewGuid(), company.Id, $"AR-CAP-{tag}", "Repair Service", ItemType.Service) { MaintainStock = false };
        await itemRepository.InsertAsync(serviceItem, autoSave: true);

        var invoice = await invoiceAppService.CreateAsync(new CreatePurchaseInvoiceDto
        {
            CompanyId = company.Id,
            SupplierId = supplier.Id,
            IssueDate = DateTime.UtcNow.Date,
            DueDate = DateTime.UtcNow.Date.AddDays(30),
            Items = new List<CreatePurchaseInvoiceItemDto>
            {
                new() { ItemId = serviceItem.Id, Description = "Repair", Quantity = 1m, UnitPrice = 100m, Uom = "Unit" },
            },
        });
        await invoiceAppService.SubmitAsync(invoice.Id);
        await invoiceAppService.PostAsync(invoice.Id);

        var asset1 = new Asset(Guid.NewGuid(), company.Id, $"AST-CAP1-{tag}", $"Asset Repair Cap Asset 1 {tag}", DateTime.UtcNow, 5000m);
        asset1.Submit();
        await assetRepository.InsertAsync(asset1, autoSave: true);

        var asset2 = new Asset(Guid.NewGuid(), company.Id, $"AST-CAP2-{tag}", $"Asset Repair Cap Asset 2 {tag}", DateTime.UtcNow, 5000m);
        asset2.Submit();
        await assetRepository.InsertAsync(asset2, autoSave: true);

        return new Fixture(company, expenseAccount.Id, asset1.Id, asset2.Id, invoice.Id);
    }

    [Fact]
    public async Task CreateAsync_ClaimExceedsPostedGlAmount_Throws()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var f = await SeedAsync("A");
            var assetRepairAppService = GetRequiredService<IAssetRepairAppService>();

            // Only 100 was ever posted to the expense account for this invoice; claiming 150 exceeds it.
            await Should.ThrowAsync<BusinessException>(() =>
                assetRepairAppService.CreateAsync(new CreateUpdateAssetRepairDto
                {
                    CompanyId = f.Company.Id,
                    AssetId = f.Asset1Id,
                    FailureDate = DateTime.Today.AddDays(-5),
                    RepairCost = 150m,
                    Invoices = new List<CreateUpdateAssetRepairPurchaseInvoiceDto>
                    {
                        new() { PurchaseInvoiceId = f.InvoiceId, RepairCost = 150m, ExpenseAccountId = f.ExpenseAccountId }
                    }
                }));
        });
    }

    [Fact]
    public async Task CreateAsync_ClaimWithinPostedGlAmount_Succeeds()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var f = await SeedAsync("B");
            var assetRepairAppService = GetRequiredService<IAssetRepairAppService>();

            var dto = await assetRepairAppService.CreateAsync(new CreateUpdateAssetRepairDto
            {
                CompanyId = f.Company.Id,
                AssetId = f.Asset1Id,
                FailureDate = DateTime.Today.AddDays(-5),
                RepairCost = 100m,
                Invoices = new List<CreateUpdateAssetRepairPurchaseInvoiceDto>
                {
                    new() { PurchaseInvoiceId = f.InvoiceId, RepairCost = 100m, ExpenseAccountId = f.ExpenseAccountId }
                }
            });

            dto.Id.ShouldNotBe(Guid.Empty);
        });
    }

    [Fact]
    public async Task CompleteAsync_SecondRepairClaimingSameInvoiceAndAccount_ThrowsOnceFirstIsCompleted()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var f = await SeedAsync("C");
            var assetRepairAppService = GetRequiredService<IAssetRepairAppService>();

            // Both created while still Pending: neither counts as "allocated" against the other yet,
            // so both creations succeed even though their combined claim (60 + 60 = 120) exceeds the
            // invoice's posted GL amount (100). This mirrors the exact race the missing cap allowed.
            var repair1 = await assetRepairAppService.CreateAsync(new CreateUpdateAssetRepairDto
            {
                CompanyId = f.Company.Id,
                AssetId = f.Asset1Id,
                FailureDate = DateTime.Today.AddDays(-5),
                RepairCost = 60m,
                CapitalizeRepairCost = true,
                Invoices = new List<CreateUpdateAssetRepairPurchaseInvoiceDto>
                {
                    new() { PurchaseInvoiceId = f.InvoiceId, RepairCost = 60m, ExpenseAccountId = f.ExpenseAccountId }
                }
            });

            var repair2 = await assetRepairAppService.CreateAsync(new CreateUpdateAssetRepairDto
            {
                CompanyId = f.Company.Id,
                AssetId = f.Asset2Id,
                FailureDate = DateTime.Today.AddDays(-5),
                RepairCost = 60m,
                CapitalizeRepairCost = true,
                Invoices = new List<CreateUpdateAssetRepairPurchaseInvoiceDto>
                {
                    new() { PurchaseInvoiceId = f.InvoiceId, RepairCost = 60m, ExpenseAccountId = f.ExpenseAccountId }
                }
            });

            // Whichever completes first locks in its allocation.
            await assetRepairAppService.CompleteAsync(repair1.Id);

            // The second now finds 60 of the 100 already allocated by the first (now Completed)
            // repair — its own 60 would push the total to 120, over the posted 100 — and is blocked.
            await Should.ThrowAsync<BusinessException>(() => assetRepairAppService.CompleteAsync(repair2.Id));
        });
    }

    [Fact]
    public async Task CompleteAsync_TwoRepairsSplittingInvoiceWithinGlAmount_BothSucceed()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var f = await SeedAsync("D");
            var assetRepairAppService = GetRequiredService<IAssetRepairAppService>();

            var repair1 = await assetRepairAppService.CreateAsync(new CreateUpdateAssetRepairDto
            {
                CompanyId = f.Company.Id,
                AssetId = f.Asset1Id,
                FailureDate = DateTime.Today.AddDays(-5),
                RepairCost = 40m,
                CapitalizeRepairCost = true,
                Invoices = new List<CreateUpdateAssetRepairPurchaseInvoiceDto>
                {
                    new() { PurchaseInvoiceId = f.InvoiceId, RepairCost = 40m, ExpenseAccountId = f.ExpenseAccountId }
                }
            });

            var repair2 = await assetRepairAppService.CreateAsync(new CreateUpdateAssetRepairDto
            {
                CompanyId = f.Company.Id,
                AssetId = f.Asset2Id,
                FailureDate = DateTime.Today.AddDays(-5),
                RepairCost = 60m,
                CapitalizeRepairCost = true,
                Invoices = new List<CreateUpdateAssetRepairPurchaseInvoiceDto>
                {
                    new() { PurchaseInvoiceId = f.InvoiceId, RepairCost = 60m, ExpenseAccountId = f.ExpenseAccountId }
                }
            });

            await assetRepairAppService.CompleteAsync(repair1.Id);

            // 40 + 60 = 100, exactly the posted GL amount — the boundary must not be rejected.
            var completed2 = await assetRepairAppService.CompleteAsync(repair2.Id);
            completed2.Status.ShouldBe(AssetRepairStatus.Completed);
        });
    }
}
