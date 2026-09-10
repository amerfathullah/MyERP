using System;
using System.Collections.Generic;
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
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Xunit;

namespace MyERP.Assets;

/// <summary>
/// Regression coverage for a real gap found via ERPNext validate() parity: asset_repair.py
/// validate_purchase_invoice_status() requires every referenced Purchase Invoice to be submitted.
/// AssetRepairAppService.CreateAsync/UpdateAsync never checked this (nor company ownership) — a
/// repair could capitalize a cost claimed against a Draft/Cancelled invoice, or one belonging to a
/// different company, straight onto the asset's book value.
/// </summary>
public abstract class AssetRepairInvoiceValidationTests<TStartupModule> : MyERPApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    [Fact]
    public async Task CreateAsync_UnsubmittedPurchaseInvoice_Throws()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepository = GetRequiredService<IRepository<Company, Guid>>();
            var assetRepository = GetRequiredService<IRepository<Asset, Guid>>();
            var invoiceRepository = GetRequiredService<IRepository<PurchaseInvoice, Guid>>();
            var supplierRepository = GetRequiredService<IRepository<Supplier, Guid>>();
            var accountRepository = GetRequiredService<IRepository<Account, Guid>>();
            var assetRepairAppService = GetRequiredService<IAssetRepairAppService>();

            var company = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "Asset Repair Guard Co"), autoSave: true);
            var supplier = await supplierRepository.InsertAsync(new Supplier(Guid.NewGuid(), company.Id, "Asset Repair Guard Supplier"), autoSave: true);
            var expenseAccount = await accountRepository.InsertAsync(
                new Account(Guid.NewGuid(), company.Id, "5910", "Repair Expense", AccountType.Expense), autoSave: true);

            var asset = new Asset(Guid.NewGuid(), company.Id, "AST-REP-001", "Asset Repair Guard Asset", DateTime.UtcNow, 8000m);
            asset.Submit();
            await assetRepository.InsertAsync(asset, autoSave: true);

            var invoice = new PurchaseInvoice(Guid.NewGuid(), company.Id, supplier.Id, "PI-REP-001", DateTime.Today);
            // Deliberately left in Draft — never submitted.
            await invoiceRepository.InsertAsync(invoice, autoSave: true);

            await Should.ThrowAsync<Volo.Abp.BusinessException>(() =>
                assetRepairAppService.CreateAsync(new CreateUpdateAssetRepairDto
                {
                    CompanyId = company.Id,
                    AssetId = asset.Id,
                    FailureDate = DateTime.Today.AddDays(-5),
                    RepairCost = 100m,
                    Invoices = new List<CreateUpdateAssetRepairPurchaseInvoiceDto>
                    {
                        new() { PurchaseInvoiceId = invoice.Id, RepairCost = 100m, ExpenseAccountId = expenseAccount.Id }
                    }
                }));
        });
    }

    [Fact]
    public async Task CreateAsync_PurchaseInvoiceFromDifferentCompany_Throws()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepository = GetRequiredService<IRepository<Company, Guid>>();
            var assetRepository = GetRequiredService<IRepository<Asset, Guid>>();
            var invoiceRepository = GetRequiredService<IRepository<PurchaseInvoice, Guid>>();
            var supplierRepository = GetRequiredService<IRepository<Supplier, Guid>>();
            var accountRepository = GetRequiredService<IRepository<Account, Guid>>();
            var assetRepairAppService = GetRequiredService<IAssetRepairAppService>();

            var ownerCompany = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "Asset Repair Guard Owner Co"), autoSave: true);
            var otherCompany = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "Asset Repair Guard Other Co"), autoSave: true);
            var supplier = await supplierRepository.InsertAsync(new Supplier(Guid.NewGuid(), otherCompany.Id, "Asset Repair Guard Supplier 2"), autoSave: true);
            var expenseAccount = await accountRepository.InsertAsync(
                new Account(Guid.NewGuid(), ownerCompany.Id, "5911", "Repair Expense 2", AccountType.Expense), autoSave: true);
            var itemRepository = GetRequiredService<IRepository<Item, Guid>>();
            var lineItem = await itemRepository.InsertAsync(
                new Item(Guid.NewGuid(), otherCompany.Id, "AR-ITEM-002", "Asset Repair Guard Line Item 2", ItemType.Goods), autoSave: true);

            var asset = new Asset(Guid.NewGuid(), ownerCompany.Id, "AST-REP-002", "Asset Repair Guard Asset 2", DateTime.UtcNow, 6000m);
            asset.Submit();
            await assetRepository.InsertAsync(asset, autoSave: true);

            var invoice = new PurchaseInvoice(Guid.NewGuid(), otherCompany.Id, supplier.Id, "PI-REP-002", DateTime.Today);
            invoice.AddItem(lineItem.Id, "Repair line item", 1m, 200m, 0m);
            invoice.Submit();
            await invoiceRepository.InsertAsync(invoice, autoSave: true);

            await Should.ThrowAsync<Volo.Abp.BusinessException>(() =>
                assetRepairAppService.CreateAsync(new CreateUpdateAssetRepairDto
                {
                    CompanyId = ownerCompany.Id,
                    AssetId = asset.Id,
                    FailureDate = DateTime.Today.AddDays(-5),
                    RepairCost = 200m,
                    Invoices = new List<CreateUpdateAssetRepairPurchaseInvoiceDto>
                    {
                        new() { PurchaseInvoiceId = invoice.Id, RepairCost = 200m, ExpenseAccountId = expenseAccount.Id }
                    }
                }));
        });
    }

    [Fact]
    public async Task CreateAsync_SubmittedSameCompanyInvoice_Succeeds()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepository = GetRequiredService<IRepository<Company, Guid>>();
            var assetRepository = GetRequiredService<IRepository<Asset, Guid>>();
            var supplierRepository = GetRequiredService<IRepository<Supplier, Guid>>();
            var accountRepository = GetRequiredService<IRepository<Account, Guid>>();
            var fiscalYearRepository = GetRequiredService<IRepository<FiscalYear, Guid>>();
            var ruleRepository = GetRequiredService<IRepository<AccountingRule, Guid>>();
            var seriesRepository = GetRequiredService<IRepository<DocumentSeries, Guid>>();
            var costCenterRepository = GetRequiredService<IRepository<CostCenter, Guid>>();
            var itemRepository = GetRequiredService<IRepository<Item, Guid>>();
            var invoiceAppService = GetRequiredService<IPurchaseInvoiceAppService>();
            var assetRepairAppService = GetRequiredService<IAssetRepairAppService>();

            var company = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "Asset Repair Happy Co"), autoSave: true);
            var supplier = await supplierRepository.InsertAsync(new Supplier(Guid.NewGuid(), company.Id, "Asset Repair Happy Supplier"), autoSave: true);
            var expenseAccount = await accountRepository.InsertAsync(
                new Account(Guid.NewGuid(), company.Id, "5912", "Repair Expense 3", AccountType.Expense), autoSave: true);
            var payableAccount = await accountRepository.InsertAsync(
                new Account(Guid.NewGuid(), company.Id, "2012", "Creditors 3", AccountType.Liability), autoSave: true);

            var costCenter = await costCenterRepository.InsertAsync(
                new CostCenter(Guid.NewGuid(), company.Id, "Asset Repair Happy Cost Center"), autoSave: true);

            company.DefaultExpenseAccountId = expenseAccount.Id;
            company.DefaultPayableAccountId = payableAccount.Id;
            company.DefaultCostCenterId = costCenter.Id;
            await companyRepository.UpdateAsync(company, autoSave: true);

            await fiscalYearRepository.InsertAsync(
                new FiscalYear(Guid.NewGuid(), company.Id, "FY Happy", DateTime.UtcNow.Date.AddYears(-1), DateTime.UtcNow.Date.AddYears(1)),
                autoSave: true);
            await seriesRepository.InsertAsync(
                new DocumentSeries(Guid.NewGuid(), company.Id, "AR Happy Series", "PurchaseInvoice", "ARHAPPY-"), autoSave: true);
            await ruleRepository.InsertAsync(
                new AccountingRule(Guid.NewGuid(), company.Id, "PI DR Expense", "PurchaseInvoice", true, AccountSource.ItemExpense, AmountSource.NetTotal)
                { SortOrder = 1 }, autoSave: true);
            await ruleRepository.InsertAsync(
                new AccountingRule(Guid.NewGuid(), company.Id, "PI CR Payable", "PurchaseInvoice", false, AccountSource.SupplierPayable, AmountSource.GrandTotal)
                { SortOrder = 2 }, autoSave: true);

            var lineItem = new Item(Guid.NewGuid(), company.Id, "AR-ITEM-003", "Asset Repair Happy Line Item", ItemType.Service) { MaintainStock = false };
            await itemRepository.InsertAsync(lineItem, autoSave: true);

            var asset = new Asset(Guid.NewGuid(), company.Id, "AST-REP-003", "Asset Repair Happy Asset", DateTime.UtcNow, 4000m);
            asset.Submit();
            await assetRepository.InsertAsync(asset, autoSave: true);

            // Real posting pipeline so a JournalEntry actually exists for the new GL-allocation cap
            // (AssetRepairAppService.ValidateRepairCostAllocationAsync) to sum against.
            var invoice = await invoiceAppService.CreateAsync(new CreatePurchaseInvoiceDto
            {
                CompanyId = company.Id,
                SupplierId = supplier.Id,
                IssueDate = DateTime.UtcNow.Date,
                DueDate = DateTime.UtcNow.Date.AddDays(30),
                Items = new List<CreatePurchaseInvoiceItemDto>
                {
                    new() { ItemId = lineItem.Id, Description = "Repair line item", Quantity = 1m, UnitPrice = 150m, Uom = "Unit" },
                },
            });
            await invoiceAppService.SubmitAsync(invoice.Id);
            await invoiceAppService.PostAsync(invoice.Id);

            var dto = await assetRepairAppService.CreateAsync(new CreateUpdateAssetRepairDto
            {
                CompanyId = company.Id,
                AssetId = asset.Id,
                FailureDate = DateTime.Today.AddDays(-5),
                RepairCost = 150m,
                Invoices = new List<CreateUpdateAssetRepairPurchaseInvoiceDto>
                {
                    new() { PurchaseInvoiceId = invoice.Id, RepairCost = 150m, ExpenseAccountId = expenseAccount.Id }
                }
            });

            dto.Id.ShouldNotBe(Guid.Empty);
        });
    }
}
