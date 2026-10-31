using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Accounting;
using MyERP.Accounting.DomainServices;
using MyERP.Accounting.Entities;
using MyERP.Core;
using MyERP.Core.Entities;
using MyERP.Inventory;
using MyERP.Inventory.Entities;
using MyERP.Manufacturing;
using MyERP.Manufacturing.Entities;
using MyERP.Sales.Entities;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Xunit;

namespace MyERP;

/// <summary>
/// End-to-end Application + EF Core SQLite integration tests for Batch 4 upstream ERPNext parity:
/// 1) AccountingRuleEngine multi-currency Cost Center Allocation preserves transaction-currency precision &amp; balanced totals (4a66be27ac).
/// 2) Standalone StockEntryAppService.GetManufactureItemsAsync(bomId, produceQty) without Work Order follows BOM even when BackflushRawMaterialsBasedOn is Material Transferred (8f40bec4f5).
/// 3) DocumentConnectionsAppService.GetConnectionsAsync("PaymentEntry", id) exposes linked GL &amp; Exchange Gain/Loss Journal Entries (d21ffbde86).
/// 4) PaymentEntryAppService.PostAsync / SubmitAsync rejects mixed-sign [+50, -10] reference rows without mutating invoice outstanding (95704805df).
/// 5) ManufacturingAppService.CreateBomAsync / GetBomAsync persists and calculates FgBasedOperatingCost &amp; OperatingCostPerBomQuantity (7fb16ab015).
/// </summary>
public abstract class UpstreamBatch4IntegrationTests<TStartupModule> : MyERPApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    private sealed class MultiCurrencyTestDocument : IAccountableDocument
    {
        public Guid Id { get; init; }
        public Guid CompanyId { get; init; }
        public string DocumentType => "SalesInvoice";
        public decimal NetTotal { get; init; }
        public decimal GrandTotal { get; init; }
        public decimal TaxAmount => 0m;
        public Guid? CustomerId => null;
        public Guid? SupplierId => null;
        public DateTime PostingDate { get; init; }
        public string CurrencyCode { get; init; } = "USD";
        public decimal ExchangeRate { get; init; } = 1m;
        public Guid? CostCenterId { get; init; }
    }

    [Fact]
    public async Task AccountingRuleEngine_MultiCurrencyCostCenterAllocation_PreservesTransactionCurrencyPrecision_4a66be27ac()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var fyRepo = GetRequiredService<IRepository<FiscalYear, Guid>>();
            var accountRepo = GetRequiredService<IRepository<Account, Guid>>();
            var ruleRepo = GetRequiredService<IRepository<AccountingRule, Guid>>();
            var ccRepo = GetRequiredService<IRepository<CostCenter, Guid>>();
            var allocRepo = GetRequiredService<IRepository<CostCenterAllocation, Guid>>();
            var engine = GetRequiredService<AccountingRuleEngine>();

            var company = await companyRepo.InsertAsync(
                new Company(Guid.NewGuid(), "CC Precision Co") { CurrencyCode = "MYR" }, autoSave: true);
            await fyRepo.InsertAsync(
                new FiscalYear(Guid.NewGuid(), company.Id, "FY2026", new DateTime(2020, 1, 1), new DateTime(2030, 12, 31)), autoSave: true);

            var arAcc = await accountRepo.InsertAsync(
                new Account(Guid.NewGuid(), company.Id, "1130-CC", "USD Debtors", AccountType.Asset) { Currency = "USD" }, autoSave: true);
            var incAcc = await accountRepo.InsertAsync(
                new Account(Guid.NewGuid(), company.Id, "4100-CC", "Sales Income", AccountType.Revenue), autoSave: true);

            await ruleRepo.InsertAsync(
                new AccountingRule(Guid.NewGuid(), company.Id, "SI DR Debtors", "SalesInvoice", true, AccountSource.FixedAccount, AmountSource.GrandTotal)
                {
                    SortOrder = 1,
                    FixedAccountId = arAcc.Id
                }, autoSave: true);
            await ruleRepo.InsertAsync(
                new AccountingRule(Guid.NewGuid(), company.Id, "SI CR Income", "SalesInvoice", false, AccountSource.FixedAccount, AmountSource.NetTotal)
                {
                    SortOrder = 2,
                    FixedAccountId = incAcc.Id
                }, autoSave: true);

            var mainCc = await ccRepo.InsertAsync(new CostCenter(Guid.NewGuid(), company.Id, "Main CC"), autoSave: true);
            var childCc1Id = Guid.Parse("00000000-0000-0000-0000-000000000011");
            var childCc2Id = Guid.Parse("00000000-0000-0000-0000-000000000022");
            await ccRepo.InsertAsync(new CostCenter(childCc1Id, company.Id, "Child CC 1"), autoSave: true);
            await ccRepo.InsertAsync(new CostCenter(childCc2Id, company.Id, "Child CC 2"), autoSave: true);

            var allocation = new CostCenterAllocation(Guid.NewGuid(), company.Id, mainCc.Id, new DateTime(2025, 1, 1));
            allocation.AddEntry(childCc1Id, 50m);
            allocation.AddEntry(childCc2Id, 50m);
            allocation.ValidatePercentages();
            await allocRepo.InsertAsync(allocation, autoSave: true);

            var doc = new MultiCurrencyTestDocument
            {
                Id = Guid.NewGuid(),
                CompanyId = company.Id,
                NetTotal = 100.01m,
                GrandTotal = 100.01m,
                PostingDate = new DateTime(2026, 6, 15),
                CurrencyCode = "USD",
                ExchangeRate = 4.50m,
                CostCenterId = mainCc.Id
            };

            var journal = await engine.PostDocumentAsync(doc);

            journal.Status.ShouldBe(DocumentStatus.Posted);
            journal.Lines.Count.ShouldBe(4);
            journal.TotalDebit.ShouldBe(journal.TotalCredit);

            var debitLines = journal.Lines.Where(l => l.IsDebit).OrderBy(l => l.CostCenterId).ToList();
            debitLines.Count.ShouldBe(2);
            debitLines.All(l => l.AccountCurrency == "USD").ShouldBeTrue();
            debitLines[0].AmountInAccountCurrency.ShouldBe(50.01m);
            debitLines[1].AmountInAccountCurrency.ShouldBe(50.00m);
            debitLines.Sum(l => l.AmountInAccountCurrency).ShouldBe(100.01m);
            debitLines.Sum(l => l.Amount).ShouldBe(journal.TotalDebit);
        });
    }

    [Fact]
    public async Task StockEntry_GetManufactureItems_StandaloneBomWithoutWorkOrder_FollowsBomEvenWhenBackflushIsMaterialTransferred_8f40bec4f5()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var mfgSettingsRepo = GetRequiredService<IRepository<ManufacturingSettings, Guid>>();
            var itemRepo = GetRequiredService<IRepository<Item, Guid>>();
            var whRepo = GetRequiredService<IRepository<Warehouse, Guid>>();
            var bomRepo = GetRequiredService<IRepository<BillOfMaterials, Guid>>();
            var seAppService = GetRequiredService<IStockEntryAppService>();

            var company = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "Standalone BOM SE Co"), autoSave: true);
            await mfgSettingsRepo.InsertAsync(
                new ManufacturingSettings(Guid.NewGuid(), company.Id)
                {
                    BackflushRawMaterialsBasedOn = "Material Transferred for Manufacture"
                }, autoSave: true);

            var srcWh = await whRepo.InsertAsync(new Warehouse(Guid.NewGuid(), company.Id, "Raw Stores"), autoSave: true);
            var fgWh = await whRepo.InsertAsync(new Warehouse(Guid.NewGuid(), company.Id, "Finished Goods"), autoSave: true);

            var fgItem = await itemRepo.InsertAsync(new Item(Guid.NewGuid(), company.Id, "FG-SA-01", "Finished Widget", ItemType.Goods), autoSave: true);
            var rm1 = await itemRepo.InsertAsync(new Item(Guid.NewGuid(), company.Id, "RM-SA-01", "Steel Plate", ItemType.Goods), autoSave: true);
            var rm2 = await itemRepo.InsertAsync(new Item(Guid.NewGuid(), company.Id, "RM-SA-02", "Fastener Bolt", ItemType.Goods), autoSave: true);

            var bom = new BillOfMaterials(Guid.NewGuid(), company.Id, "BOM-SA-001", fgItem.Id)
            {
                Quantity = 2m,
                SourceWarehouseId = srcWh.Id,
                TargetWarehouseId = fgWh.Id
            };
            bom.AddItem(new BomItem(Guid.NewGuid(), bom.Id, rm1.Id, rm1.ItemName, quantity: 3m, rate: 20m));
            bom.AddItem(new BomItem(Guid.NewGuid(), bom.Id, rm2.Id, rm2.ItemName, quantity: 5m, rate: 4m));
            bom.RecalculateCost();
            await bomRepo.InsertAsync(bom, autoSave: true);

            var dto = await seAppService.GetManufactureItemsAsync(bom.Id, produceQty: 4m);

            dto.WorkOrderId.ShouldBe(Guid.Empty);
            dto.BomId.ShouldBe(bom.Id);
            dto.FgItemId.ShouldBe(fgItem.Id);
            dto.SourceWarehouseId.ShouldBe(srcWh.Id);
            dto.FgWarehouseId.ShouldBe(fgWh.Id);

            var rmLines = dto.Items.Where(i => i.IsRawMaterial).OrderBy(i => i.ItemName).ToList();
            rmLines.Count.ShouldBe(2);
            rmLines.Single(i => i.ItemId == rm1.Id).RequiredQty.ShouldBe(6m);
            rmLines.Single(i => i.ItemId == rm2.Id).RequiredQty.ShouldBe(10m);
            dto.Items.Single(i => !i.IsRawMaterial).RequiredQty.ShouldBe(4m);
        });
    }

    [Fact]
    public async Task DocumentConnections_PaymentEntry_ReturnsLinkedMainAndExchangeGainLossJournalEntries_d21ffbde86()
    {
        Guid peId = Guid.Empty;

        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var fyRepo = GetRequiredService<IRepository<FiscalYear, Guid>>();
            var accountRepo = GetRequiredService<IRepository<Account, Guid>>();
            var ruleRepo = GetRequiredService<IRepository<AccountingRule, Guid>>();
            var seriesRepo = GetRequiredService<IRepository<DocumentSeries, Guid>>();
            var customerRepo = GetRequiredService<IRepository<Customer, Guid>>();
            var itemRepo = GetRequiredService<IRepository<Item, Guid>>();
            var siRepo = GetRequiredService<IRepository<SalesInvoice, Guid>>();
            var peAppService = GetRequiredService<IPaymentEntryAppService>();

            var company = await companyRepo.InsertAsync(
                new Company(Guid.NewGuid(), "PE Dashboard JE Co") { CurrencyCode = "MYR" }, autoSave: true);
            await fyRepo.InsertAsync(
                new FiscalYear(Guid.NewGuid(), company.Id, "FY2026", new DateTime(2020, 1, 1), new DateTime(2030, 12, 31)), autoSave: true);

            var receivableAcc = await accountRepo.InsertAsync(
                new Account(Guid.NewGuid(), company.Id, "1130-PED", "Debtors", AccountType.Asset)
                {
                    AccountSubType = AccountSubType.AccountsReceivable
                }, autoSave: true);
            var cashAcc = await accountRepo.InsertAsync(
                new Account(Guid.NewGuid(), company.Id, "1110-PED", "Cash", AccountType.Asset)
                {
                    AccountSubType = AccountSubType.CashAccount
                }, autoSave: true);
            var fxGainAcc = await accountRepo.InsertAsync(
                new Account(Guid.NewGuid(), company.Id, "7100-PED", "FX Gain", AccountType.Revenue), autoSave: true);
            var fxLossAcc = await accountRepo.InsertAsync(
                new Account(Guid.NewGuid(), company.Id, "8100-PED", "FX Loss", AccountType.Expense), autoSave: true);

            company.DefaultReceivableAccountId = receivableAcc.Id;
            company.ExchangeGainAccountId = fxGainAcc.Id;
            company.ExchangeLossAccountId = fxLossAcc.Id;
            await companyRepo.UpdateAsync(company, autoSave: true);

            await ruleRepo.InsertAsync(
                new AccountingRule(Guid.NewGuid(), company.Id, "PE DR Cash", "PaymentEntry", true, AccountSource.FixedAccount, AmountSource.GrandTotal)
                {
                    SortOrder = 1,
                    FixedAccountId = cashAcc.Id
                }, autoSave: true);
            await ruleRepo.InsertAsync(
                new AccountingRule(Guid.NewGuid(), company.Id, "PE CR Debtors", "PaymentEntry", false, AccountSource.CustomerReceivable, AmountSource.GrandTotal)
                {
                    SortOrder = 2,
                    FixedAccountId = receivableAcc.Id
                }, autoSave: true);

            await seriesRepo.InsertAsync(new DocumentSeries(Guid.NewGuid(), company.Id, "PE Series", "PaymentEntry", "PE-CON-"), autoSave: true);

            var customer = await customerRepo.InsertAsync(new Customer(Guid.NewGuid(), company.Id, "FX Customer"), autoSave: true);
            var srvItem = await itemRepo.InsertAsync(new Item(Guid.NewGuid(), company.Id, "SRV-FX", "Consulting", ItemType.Service) { MaintainStock = false }, autoSave: true);
            var si = new SalesInvoice(Guid.NewGuid(), company.Id, customer.Id, "SINV-FX-001", new DateTime(2026, 6, 1))
            {
                CurrencyCode = "USD",
                ExchangeRate = 4.40m,
                DueDate = new DateTime(2026, 6, 30)
            };
            si.AddItem(srvItem.Id, "Consulting", quantity: 1m, unitPrice: 100m, taxAmount: 0m);
            si.Submit();
            await siRepo.InsertAsync(si, autoSave: true);

            var created = await peAppService.CreateAsync(new CreatePaymentEntryDto
            {
                CompanyId = company.Id,
                PaymentType = PaymentType.Receive,
                PostingDate = new DateTime(2026, 6, 15),
                PartyType = "Customer",
                PartyId = customer.Id,
                PaidFromAccountId = receivableAcc.Id,
                PaidToAccountId = cashAcc.Id,
                PaidAmount = 100m,
                PaymentCurrency = "USD",
                ExchangeRate = 4.50m,
                AgainstInvoiceType = "SalesInvoice",
                AgainstInvoiceId = si.Id
            });
            peId = created.Id;

            await peAppService.PostAsync(peId);
        });

        await WithUnitOfWorkAsync(async () =>
        {
            var connectionsAppService = GetRequiredService<IDocumentConnectionsAppService>();
            var connections = await connectionsAppService.GetConnectionsAsync("PaymentEntry", peId);

            var accGroup = connections.Groups.SingleOrDefault(g => g.Label == "Accounting");
            accGroup.ShouldNotBeNull();

            var jeItem = accGroup.Items.SingleOrDefault(i => i.DocumentType == "Journal Entry");
            jeItem.ShouldNotBeNull();
            jeItem.Count.ShouldBeGreaterThanOrEqualTo(2);
            jeItem.Documents.All(d => d.Route.StartsWith("/accounting/journal-entries/")).ShouldBeTrue();
        });
    }

    [Fact]
    public async Task PaymentEntry_MixedSignReferenceRowsAgainstPositiveInvoice_ThrowsOverAllocationWithoutMutatingInvoice_95704805df()
    {
        Guid siId = Guid.Empty;
        Guid peId = Guid.Empty;

        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var accountRepo = GetRequiredService<IRepository<Account, Guid>>();
            var seriesRepo = GetRequiredService<IRepository<DocumentSeries, Guid>>();
            var customerRepo = GetRequiredService<IRepository<Customer, Guid>>();
            var itemRepo = GetRequiredService<IRepository<Item, Guid>>();
            var siRepo = GetRequiredService<IRepository<SalesInvoice, Guid>>();
            var scheduleRepo = GetRequiredService<IRepository<PaymentScheduleEntry, Guid>>();
            var peAppService = GetRequiredService<IPaymentEntryAppService>();

            var company = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "PE Mixed Sign Co"), autoSave: true);
            var receivableAcc = await accountRepo.InsertAsync(
                new Account(Guid.NewGuid(), company.Id, "1130-MIX", "Debtors", AccountType.Asset)
                {
                    AccountSubType = AccountSubType.AccountsReceivable
                }, autoSave: true);
            var cashAcc = await accountRepo.InsertAsync(
                new Account(Guid.NewGuid(), company.Id, "1110-MIX", "Cash", AccountType.Asset)
                {
                    AccountSubType = AccountSubType.CashAccount
                }, autoSave: true);
            await seriesRepo.InsertAsync(new DocumentSeries(Guid.NewGuid(), company.Id, "PE Series", "PaymentEntry", "PE-MIX-"), autoSave: true);

            var customer = await customerRepo.InsertAsync(new Customer(Guid.NewGuid(), company.Id, "Mixed Sign Cust"), autoSave: true);
            var srvItem = await itemRepo.InsertAsync(new Item(Guid.NewGuid(), company.Id, "SRV-MIX", "Item 118", ItemType.Service) { MaintainStock = false }, autoSave: true);
            var si = new SalesInvoice(Guid.NewGuid(), company.Id, customer.Id, "SINV-MIX-118", new DateTime(2026, 6, 1))
            {
                DueDate = new DateTime(2026, 6, 30)
            };
            si.AddItem(srvItem.Id, "Item 118", quantity: 1m, unitPrice: 118m, taxAmount: 0m);
            si.Submit();
            await siRepo.InsertAsync(si, autoSave: true);
            siId = si.Id;

            var term1 = await scheduleRepo.InsertAsync(
                new PaymentScheduleEntry(Guid.NewGuid(), "SalesInvoice", si.Id, new DateTime(2026, 6, 15), 50m, 59m, "Term 1"), autoSave: true);
            var term2 = await scheduleRepo.InsertAsync(
                new PaymentScheduleEntry(Guid.NewGuid(), "SalesInvoice", si.Id, new DateTime(2026, 6, 30), 50m, 59m, "Term 2"), autoSave: true);

            var created = await peAppService.CreateAsync(new CreatePaymentEntryDto
            {
                CompanyId = company.Id,
                PaymentType = PaymentType.Receive,
                PostingDate = new DateTime(2026, 6, 10),
                PartyType = "Customer",
                PartyId = customer.Id,
                PaidFromAccountId = receivableAcc.Id,
                PaidToAccountId = cashAcc.Id,
                PaidAmount = 40m,
                References = new List<PaymentReferenceDto>
                {
                    new() { ReferenceType = "SalesInvoice", ReferenceId = si.Id, PaymentTermId = term1.Id, AllocatedAmount = 50m },
                    new() { ReferenceType = "SalesInvoice", ReferenceId = si.Id, PaymentTermId = term2.Id, AllocatedAmount = -10m }
                }
            });
            peId = created.Id;
        });

        await WithUnitOfWorkAsync(async () =>
        {
            var peAppService = GetRequiredService<IPaymentEntryAppService>();
            var ex = await Should.ThrowAsync<BusinessException>(() => peAppService.PostAsync(peId));
            (ex.Code == MyERPDomainErrorCodes.OverAllocation || ex.Code == MyERPDomainErrorCodes.PaymentTermOutstandingExceeded).ShouldBeTrue();
        });

        await WithUnitOfWorkAsync(async () =>
        {
            var siRepo = GetRequiredService<IRepository<SalesInvoice, Guid>>();
            var si = await siRepo.GetAsync(siId);
            si.OutstandingAmount.ShouldBe(118m);
        });
    }

    [Fact]
    public async Task Manufacturing_BomFgBasedOperatingCost_PersistsAndComputesOperatingCost_7fb16ab015()
    {
        Guid bomId = Guid.Empty;

        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var seriesRepo = GetRequiredService<IRepository<DocumentSeries, Guid>>();
            var itemRepo = GetRequiredService<IRepository<Item, Guid>>();
            var mfgAppService = GetRequiredService<IManufacturingAppService>();

            var company = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "BOM FG OpCost Co"), autoSave: true);
            await seriesRepo.InsertAsync(new DocumentSeries(Guid.NewGuid(), company.Id, "BOM Series", "BOM", "BOM-FG-"), autoSave: true);

            var fgItem = await itemRepo.InsertAsync(new Item(Guid.NewGuid(), company.Id, "FG-BOM-01", "Finished Assembly", ItemType.Goods), autoSave: true);
            var rmItem = await itemRepo.InsertAsync(new Item(Guid.NewGuid(), company.Id, "RM-BOM-01", "Raw Resin", ItemType.Goods), autoSave: true);

            var created = await mfgAppService.CreateBomAsync(new CreateBomDto
            {
                CompanyId = company.Id,
                ItemId = fgItem.Id,
                Quantity = 4m,
                WithOperations = false,
                IsPhantomBom = false,
                FgBasedOperatingCost = true,
                OperatingCostPerBomQuantity = 12.5m,
                Items = new List<CreateBomItemDto>
                {
                    new() { ItemId = rmItem.Id, ItemName = rmItem.ItemName, Quantity = 4m, Rate = 10m, Uom = "Unit" }
                }
            });

            created.TotalMaterialCost.ShouldBe(40m);
            created.OperatingCost.ShouldBe(50m);
            created.TotalCost.ShouldBe(90m);
            created.FgBasedOperatingCost.ShouldBeTrue();
            created.OperatingCostPerBomQuantity.ShouldBe(12.5m);
            bomId = created.Id;
        });

        await WithUnitOfWorkAsync(async () =>
        {
            var mfgAppService = GetRequiredService<IManufacturingAppService>();
            var fetched = await mfgAppService.GetBomAsync(bomId);
            fetched.FgBasedOperatingCost.ShouldBeTrue();
            fetched.OperatingCostPerBomQuantity.ShouldBe(12.5m);
            fetched.OperatingCost.ShouldBe(50m);
            fetched.TotalCost.ShouldBe(90m);
        });
    }
}
