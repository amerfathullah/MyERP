using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Accounting;
using MyERP.Accounting.Entities;
using MyERP.Core.Entities;
using MyERP.Inventory;
using MyERP.Inventory.DomainServices;
using MyERP.Inventory.Entities;
using MyERP.Sales;
using MyERP.Sales.Entities;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Xunit;

namespace MyERP;

public abstract class UpstreamBatch35IntegrationTests<TStartupModule> : MyERPApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    [Fact]
    public async Task SalesInvoice_UpdateStock_ProductBundle_DeductsComponentStock()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var customerRepo = GetRequiredService<IRepository<Customer, Guid>>();
            var whRepo = GetRequiredService<IRepository<Warehouse, Guid>>();
            var itemRepo = GetRequiredService<IRepository<Item, Guid>>();
            var bundleRepo = GetRequiredService<IRepository<ProductBundle, Guid>>();
            var seriesRepo = GetRequiredService<IRepository<DocumentSeries, Guid>>();
            var fyRepo = GetRequiredService<IRepository<FiscalYear, Guid>>();
            var sleRepo = GetRequiredService<IRepository<StockLedgerEntry, Guid>>();
            var siAppService = GetRequiredService<ISalesInvoiceAppService>();
            var stockValuation = GetRequiredService<StockValuationService>();
            var binService = GetRequiredService<BinService>();

            var company = await companyRepo.InsertAsync(
                new Company(Guid.NewGuid(), "SI Bundle Co B35") { CurrencyCode = "MYR" },
                autoSave: true);

            var accountRepo = GetRequiredService<IRepository<Account, Guid>>();
            var ruleRepo = GetRequiredService<IRepository<AccountingRule, Guid>>();
            var ccRepo = GetRequiredService<IRepository<CostCenter, Guid>>();

            var recvAcc = await accountRepo.InsertAsync(
                new Account(Guid.NewGuid(), company.Id, "1200-C", "Debtors", AccountType.Asset), autoSave: true);
            var incAcc = await accountRepo.InsertAsync(
                new Account(Guid.NewGuid(), company.Id, "4000-C", "Sales", AccountType.Revenue), autoSave: true);
            var costCenter = await ccRepo.InsertAsync(
                new CostCenter(Guid.NewGuid(), company.Id, "Main CC"), autoSave: true);

            company.DefaultReceivableAccountId = recvAcc.Id;
            company.DefaultIncomeAccountId = incAcc.Id;
            company.DefaultCostCenterId = costCenter.Id;
            await companyRepo.UpdateAsync(company, autoSave: true);

            await ruleRepo.InsertAsync(
                new AccountingRule(Guid.NewGuid(), company.Id, "SI DR Recv", "SalesInvoice", true, AccountSource.CustomerReceivable, AmountSource.GrandTotal) { SortOrder = 1 }, autoSave: true);
            await ruleRepo.InsertAsync(
                new AccountingRule(Guid.NewGuid(), company.Id, "SI CR Income", "SalesInvoice", false, AccountSource.ItemIncome, AmountSource.NetTotal) { SortOrder = 2 }, autoSave: true);

            await fyRepo.InsertAsync(
                new FiscalYear(Guid.NewGuid(), company.Id, "FY2026", new DateTime(2020, 1, 1), new DateTime(2030, 12, 31)),
                autoSave: true);

            await seriesRepo.InsertAsync(
                new DocumentSeries(Guid.NewGuid(), company.Id, "SI Series", "SalesInvoice", "INV-B35-"),
                autoSave: true);

            var customer = await customerRepo.InsertAsync(
                new Customer(Guid.NewGuid(), company.Id, "Customer B35"),
                autoSave: true);

            var warehouse = await whRepo.InsertAsync(
                new Warehouse(Guid.NewGuid(), company.Id, "Stores B35"),
                autoSave: true);

            // Component stock item
            var compItem = await itemRepo.InsertAsync(
                new Item(Guid.NewGuid(), company.Id, "RM-B35-1", "Raw Material B35-1", ItemType.Goods)
                {
                    MaintainStock = true,
                    StandardBuyingPrice = 10m
                },
                autoSave: true);

            // Parent virtual bundle item
            var bundleItem = await itemRepo.InsertAsync(
                new Item(Guid.NewGuid(), company.Id, "BUNDLE-B35", "Bundle B35", ItemType.Goods)
                {
                    MaintainStock = false
                },
                autoSave: true);

            // Product Bundle: 1 Bundle = 2 of RM-B35-1
            var bundle = new ProductBundle(Guid.NewGuid(), bundleItem.Id);
            bundle.AddItem(compItem.Id, 2m, "Raw Material B35-1", "Nos");
            await bundleRepo.InsertAsync(bundle, autoSave: true);

            // Seed 50 units @ 10 MYR into warehouse
            await stockValuation.CreateLedgerEntryAsync(
                company.Id, compItem.Id, warehouse.Id, DateTime.UtcNow.Date,
                50m, 10m, voucherType: "StockEntry", voucherId: Guid.NewGuid());
            await binService.ApplyStockMovementAsync(
                compItem.Id, warehouse.Id, 50m, 500m);

            // Create Sales Invoice with UpdateStock = true
            var createDto = new CreateSalesInvoiceDto
            {
                CompanyId = company.Id,
                CustomerId = customer.Id,
                WarehouseId = warehouse.Id,
                UpdateStock = true,
                IssueDate = DateTime.UtcNow.Date,
                Items = new List<CreateSalesInvoiceItemDto>
                {
                    new()
                    {
                        ItemId = bundleItem.Id,
                        Description = "Bundle Item",
                        Quantity = 5m,
                        UnitPrice = 50m,
                        Uom = "Nos"
                    }
                }
            };

            var created = await siAppService.CreateAsync(createDto);
            created.Items[0].ProductBundleId.ShouldBe(bundle.Id);

            // Submit Sales Invoice
            var submitted = await siAppService.SubmitAsync(created.Id);
            submitted.Status.ShouldBe(Core.DocumentStatus.Submitted.ToString());

            // Component stock deducted: 50 - (5 * 2) = 40
            var compBal = await stockValuation.GetCurrentBalanceAsync(compItem.Id, warehouse.Id);
            compBal.Quantity.ShouldBe(40m);

            // Check SLE for component item
            var compSles = (await sleRepo.GetQueryableAsync())
                .Where(s => s.VoucherType == "SalesInvoice" && s.VoucherId == submitted.Id && s.ItemId == compItem.Id)
                .ToList();
            compSles.Count.ShouldBe(1);
            compSles[0].QuantityChange.ShouldBe(-10m);
            compSles[0].ValuationRate.ShouldBe(10m);

            // Check NO SLE for virtual parent bundle item
            var parentSles = (await sleRepo.GetQueryableAsync())
                .Where(s => s.VoucherType == "SalesInvoice" && s.VoucherId == submitted.Id && s.ItemId == bundleItem.Id)
                .ToList();
            parentSles.ShouldBeEmpty();

            // Line valuation rate set to component total cost: 2 * 10m = 20m
            var loadedSiRepo = GetRequiredService<IRepository<SalesInvoice, Guid>>();
            var loadedSi = await loadedSiRepo.GetAsync(submitted.Id);
            loadedSi.Items[0].ValuationRate.ShouldBe(20m);
        });
    }

    [Fact]
    public async Task SalesInvoice_UpdateStock_ProductBundle_Return_RestoresOriginalComponentQty()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var customerRepo = GetRequiredService<IRepository<Customer, Guid>>();
            var whRepo = GetRequiredService<IRepository<Warehouse, Guid>>();
            var itemRepo = GetRequiredService<IRepository<Item, Guid>>();
            var bundleRepo = GetRequiredService<IRepository<ProductBundle, Guid>>();
            var seriesRepo = GetRequiredService<IRepository<DocumentSeries, Guid>>();
            var fyRepo = GetRequiredService<IRepository<FiscalYear, Guid>>();
            var sleRepo = GetRequiredService<IRepository<StockLedgerEntry, Guid>>();
            var siAppService = GetRequiredService<ISalesInvoiceAppService>();
            var stockValuation = GetRequiredService<StockValuationService>();
            var binService = GetRequiredService<BinService>();

            var company = await companyRepo.InsertAsync(
                new Company(Guid.NewGuid(), "SI Bundle Return Co B35") { CurrencyCode = "MYR" },
                autoSave: true);

            var accountRepo = GetRequiredService<IRepository<Account, Guid>>();
            var ruleRepo = GetRequiredService<IRepository<AccountingRule, Guid>>();
            var ccRepo = GetRequiredService<IRepository<CostCenter, Guid>>();

            var recvAcc = await accountRepo.InsertAsync(
                new Account(Guid.NewGuid(), company.Id, "1200-C", "Debtors", AccountType.Asset), autoSave: true);
            var incAcc = await accountRepo.InsertAsync(
                new Account(Guid.NewGuid(), company.Id, "4000-C", "Sales", AccountType.Revenue), autoSave: true);
            var costCenter = await ccRepo.InsertAsync(
                new CostCenter(Guid.NewGuid(), company.Id, "Main CC"), autoSave: true);

            company.DefaultReceivableAccountId = recvAcc.Id;
            company.DefaultIncomeAccountId = incAcc.Id;
            company.DefaultCostCenterId = costCenter.Id;
            await companyRepo.UpdateAsync(company, autoSave: true);

            await ruleRepo.InsertAsync(
                new AccountingRule(Guid.NewGuid(), company.Id, "SI DR Recv", "SalesInvoice", true, AccountSource.CustomerReceivable, AmountSource.GrandTotal) { SortOrder = 1 }, autoSave: true);
            await ruleRepo.InsertAsync(
                new AccountingRule(Guid.NewGuid(), company.Id, "SI CR Income", "SalesInvoice", false, AccountSource.ItemIncome, AmountSource.NetTotal) { SortOrder = 2 }, autoSave: true);

            await fyRepo.InsertAsync(
                new FiscalYear(Guid.NewGuid(), company.Id, "FY2026", new DateTime(2020, 1, 1), new DateTime(2030, 12, 31)),
                autoSave: true);

            await seriesRepo.InsertAsync(
                new DocumentSeries(Guid.NewGuid(), company.Id, "SI Series", "SalesInvoice", "INV-B35R-"),
                autoSave: true);

            var customer = await customerRepo.InsertAsync(
                new Customer(Guid.NewGuid(), company.Id, "Customer B35 Return"),
                autoSave: true);

            var warehouse = await whRepo.InsertAsync(
                new Warehouse(Guid.NewGuid(), company.Id, "Stores B35 Return"),
                autoSave: true);

            var compItem = await itemRepo.InsertAsync(
                new Item(Guid.NewGuid(), company.Id, "RM-B35-R1", "Raw Material B35 Return", ItemType.Goods)
                {
                    MaintainStock = true,
                    StandardBuyingPrice = 15m
                },
                autoSave: true);

            var bundleItem = await itemRepo.InsertAsync(
                new Item(Guid.NewGuid(), company.Id, "BUNDLE-B35-R", "Bundle Return", ItemType.Goods)
                {
                    MaintainStock = false
                },
                autoSave: true);

            var bundle = new ProductBundle(Guid.NewGuid(), bundleItem.Id);
            bundle.AddItem(compItem.Id, 3m, "Raw Material B35 Return", "Nos");
            await bundleRepo.InsertAsync(bundle, autoSave: true);

            // Seed 100 units @ 15 MYR
            await stockValuation.CreateLedgerEntryAsync(
                company.Id, compItem.Id, warehouse.Id, DateTime.UtcNow.Date,
                100m, 15m, voucherType: "StockEntry", voucherId: Guid.NewGuid());
            await binService.ApplyStockMovementAsync(
                compItem.Id, warehouse.Id, 100m, 1500m);

            // Submit original Sales Invoice for 4 bundles (12 components)
            var createDto = new CreateSalesInvoiceDto
            {
                CompanyId = company.Id,
                CustomerId = customer.Id,
                WarehouseId = warehouse.Id,
                UpdateStock = true,
                IssueDate = DateTime.UtcNow.Date,
                Items = new List<CreateSalesInvoiceItemDto>
                {
                    new()
                    {
                        ItemId = bundleItem.Id,
                        Description = "Bundle Item",
                        Quantity = 4m,
                        UnitPrice = 80m,
                        Uom = "Nos"
                    }
                }
            };
            var origSi = await siAppService.CreateAsync(createDto);
            await siAppService.SubmitAsync(origSi.Id);

            var afterSubmitBal = await stockValuation.GetCurrentBalanceAsync(compItem.Id, warehouse.Id);
            afterSubmitBal.Quantity.ShouldBe(88m); // 100 - 12 = 88

            // Create Return Sales Invoice for 2 bundles (6 components returned)
            var returnDto = new CreateSalesInvoiceDto
            {
                CompanyId = company.Id,
                CustomerId = customer.Id,
                WarehouseId = warehouse.Id,
                UpdateStock = true,
                IsReturn = true,
                ReturnAgainstId = origSi.Id,
                IssueDate = DateTime.UtcNow.Date,
                Items = new List<CreateSalesInvoiceItemDto>
                {
                    new()
                    {
                        ItemId = bundleItem.Id,
                        Description = "Bundle Return Item",
                        Quantity = -2m,
                        UnitPrice = 80m,
                        Uom = "Nos"
                    }
                }
            };
            var returnSi = await siAppService.CreateAsync(returnDto);
            await siAppService.SubmitAsync(returnSi.Id);

            // Component stock restored: 88 + 6 = 94
            var compBal = await stockValuation.GetCurrentBalanceAsync(compItem.Id, warehouse.Id);
            compBal.Quantity.ShouldBe(94m);

            // Verify return SLE incoming rate is restored from original invoice component rate (15m)
            var retSles = (await sleRepo.GetQueryableAsync())
                .Where(s => s.VoucherType == "SalesInvoice" && s.VoucherId == returnSi.Id && s.ItemId == compItem.Id)
                .ToList();
            retSles.Count.ShouldBe(1);
            retSles[0].QuantityChange.ShouldBe(6m);
            retSles[0].IncomingRate.ShouldBe(15m);
        });
    }

    [Fact]
    public async Task SalesInvoice_UpdateStock_ProductBundle_Cancel_ReversesComponentStock()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var customerRepo = GetRequiredService<IRepository<Customer, Guid>>();
            var whRepo = GetRequiredService<IRepository<Warehouse, Guid>>();
            var itemRepo = GetRequiredService<IRepository<Item, Guid>>();
            var bundleRepo = GetRequiredService<IRepository<ProductBundle, Guid>>();
            var seriesRepo = GetRequiredService<IRepository<DocumentSeries, Guid>>();
            var fyRepo = GetRequiredService<IRepository<FiscalYear, Guid>>();
            var sleRepo = GetRequiredService<IRepository<StockLedgerEntry, Guid>>();
            var siAppService = GetRequiredService<ISalesInvoiceAppService>();
            var stockValuation = GetRequiredService<StockValuationService>();
            var binService = GetRequiredService<BinService>();

            var company = await companyRepo.InsertAsync(
                new Company(Guid.NewGuid(), "SI Bundle Cancel Co B35") { CurrencyCode = "MYR" },
                autoSave: true);

            var accountRepo = GetRequiredService<IRepository<Account, Guid>>();
            var ruleRepo = GetRequiredService<IRepository<AccountingRule, Guid>>();
            var ccRepo = GetRequiredService<IRepository<CostCenter, Guid>>();

            var recvAcc = await accountRepo.InsertAsync(
                new Account(Guid.NewGuid(), company.Id, "1200-C", "Debtors", AccountType.Asset), autoSave: true);
            var incAcc = await accountRepo.InsertAsync(
                new Account(Guid.NewGuid(), company.Id, "4000-C", "Sales", AccountType.Revenue), autoSave: true);
            var costCenter = await ccRepo.InsertAsync(
                new CostCenter(Guid.NewGuid(), company.Id, "Main CC"), autoSave: true);

            company.DefaultReceivableAccountId = recvAcc.Id;
            company.DefaultIncomeAccountId = incAcc.Id;
            company.DefaultCostCenterId = costCenter.Id;
            await companyRepo.UpdateAsync(company, autoSave: true);

            await ruleRepo.InsertAsync(
                new AccountingRule(Guid.NewGuid(), company.Id, "SI DR Recv", "SalesInvoice", true, AccountSource.CustomerReceivable, AmountSource.GrandTotal) { SortOrder = 1 }, autoSave: true);
            await ruleRepo.InsertAsync(
                new AccountingRule(Guid.NewGuid(), company.Id, "SI CR Income", "SalesInvoice", false, AccountSource.ItemIncome, AmountSource.NetTotal) { SortOrder = 2 }, autoSave: true);

            await fyRepo.InsertAsync(
                new FiscalYear(Guid.NewGuid(), company.Id, "FY2026", new DateTime(2020, 1, 1), new DateTime(2030, 12, 31)),
                autoSave: true);

            await seriesRepo.InsertAsync(
                new DocumentSeries(Guid.NewGuid(), company.Id, "SI Series", "SalesInvoice", "INV-B35C-"),
                autoSave: true);

            var customer = await customerRepo.InsertAsync(
                new Customer(Guid.NewGuid(), company.Id, "Customer B35 Cancel"),
                autoSave: true);

            var warehouse = await whRepo.InsertAsync(
                new Warehouse(Guid.NewGuid(), company.Id, "Stores B35 Cancel"),
                autoSave: true);

            var compItem = await itemRepo.InsertAsync(
                new Item(Guid.NewGuid(), company.Id, "RM-B35-C1", "Raw Material B35 Cancel", ItemType.Goods)
                {
                    MaintainStock = true,
                    StandardBuyingPrice = 25m
                },
                autoSave: true);

            var bundleItem = await itemRepo.InsertAsync(
                new Item(Guid.NewGuid(), company.Id, "BUNDLE-B35-C", "Bundle Cancel", ItemType.Goods)
                {
                    MaintainStock = false
                },
                autoSave: true);

            var bundle = new ProductBundle(Guid.NewGuid(), bundleItem.Id);
            bundle.AddItem(compItem.Id, 1m, "Raw Material B35 Cancel", "Nos");
            await bundleRepo.InsertAsync(bundle, autoSave: true);

            // Seed 30 units @ 25 MYR
            await stockValuation.CreateLedgerEntryAsync(
                company.Id, compItem.Id, warehouse.Id, DateTime.UtcNow.Date,
                30m, 25m, voucherType: "StockEntry", voucherId: Guid.NewGuid());
            await binService.ApplyStockMovementAsync(
                compItem.Id, warehouse.Id, 30m, 750m);

            // Create and Submit Sales Invoice for 5 bundles (5 components)
            var createDto = new CreateSalesInvoiceDto
            {
                CompanyId = company.Id,
                CustomerId = customer.Id,
                WarehouseId = warehouse.Id,
                UpdateStock = true,
                IssueDate = DateTime.UtcNow.Date,
                Items = new List<CreateSalesInvoiceItemDto>
                {
                    new()
                    {
                        ItemId = bundleItem.Id,
                        Description = "Bundle Item",
                        Quantity = 5m,
                        UnitPrice = 50m,
                        Uom = "Nos"
                    }
                }
            };
            var si = await siAppService.CreateAsync(createDto);
            await siAppService.SubmitAsync(si.Id);
            await siAppService.PostAsync(si.Id);

            var afterSubmitBal = await stockValuation.GetCurrentBalanceAsync(compItem.Id, warehouse.Id);
            afterSubmitBal.Quantity.ShouldBe(25m); // 30 - 5 = 25

            // Cancel the Sales Invoice
            await siAppService.CancelAsync(si.Id);

            // Component stock restored back to 30
            var compBal = await stockValuation.GetCurrentBalanceAsync(compItem.Id, warehouse.Id);
            compBal.Quantity.ShouldBe(30m);

            // Cancellation SLE exists with +5 qty
            var cancelSles = (await sleRepo.GetQueryableAsync())
                .Where(s => s.VoucherType == "SalesInvoice" && s.VoucherId == si.Id && s.ItemId == compItem.Id && s.QuantityChange > 0)
                .ToList();
            cancelSles.Count.ShouldBe(1);
            cancelSles[0].QuantityChange.ShouldBe(5m);
        });
    }
}
