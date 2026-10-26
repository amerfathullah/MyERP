using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Accounting;
using MyERP.Accounting.DomainServices;
using MyERP.Accounting.Entities;
using MyERP.Core;
using MyERP.Core.Entities;
using MyERP.Inventory.DomainServices;
using MyERP.Inventory.Entities;
using MyERP.Sales;
using MyERP.Sales.DomainServices;
using MyERP.Sales.Entities;
using NSubstitute;
using Shouldly;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Volo.Abp.Settings;
using Xunit;

namespace MyERP.Domain.Tests;

public class UpstreamParityBatch20261026Part4Tests
{
    // --- ERPNext PR #62149 / commit 05bc9ce0fe: Product Bundle in Pick List ---

    [Fact]
    public void PickListManager_ComputePickedQtyForBundle_UsesOrderRowBundleVersion()
    {
        // Scenario:
        // Bundle v1 requires 2 components per bundle.
        // Later bundle v2 requires 4 components per bundle.
        // Sales order item was placed when v1 was active (or specifically recorded v1 with 2 components).
        // 6 components are picked in Pick List.
        // Picked bundles must be 6 / 2 = 3 bundles, NOT 6 / 4 = 1 bundle.

        var soItemId = Guid.NewGuid();
        var componentItemId = Guid.NewGuid();
        var pickListId = Guid.NewGuid();
        var warehouseId = Guid.NewGuid();
        var packedRowId = Guid.NewGuid();

        var pickItems = new List<PickListItem>
        {
            new PickListItem(
                id: Guid.NewGuid(),
                pickListId: pickListId,
                itemId: componentItemId,
                warehouseId: warehouseId,
                qty: 6m,
                stockQty: 6m,
                itemName: "Component",
                batchId: null,
                productBundleItemId: packedRowId)
            {
                SourceDocumentItemId = soItemId
            }
        };

        // Bundle version v1 requires 2 of componentItemId per bundle
        var bundleV1Map = new Dictionary<Guid, decimal>
        {
            [componentItemId] = 2m
        };

        var pickedBundles = PickListManager.ComputePickedQtyForBundle(pickItems, soItemId, bundleV1Map);

        pickedBundles.ShouldBe(3);
    }

    [Fact]
    public void PickListManager_ComputePickedQtyForBundle_AccountsForComponentUomConversion()
    {
        // Scenario:
        // Component UOM in bundle is "Box", with conversion factor 12 (1 Box = 12 Nos).
        // Pick List items are in stock UOM ("Nos").
        // Pick List picks 12 Nos.
        // Component required qty in stock UOM is 1 * 12 = 12 Nos.
        // 12 Nos picked / 12 = 1 bundle (not 12 bundles).

        var soItemId = Guid.NewGuid();
        var componentItemId = Guid.NewGuid();
        var pickListId = Guid.NewGuid();
        var warehouseId = Guid.NewGuid();
        var packedRowId = Guid.NewGuid();

        var pickItems = new List<PickListItem>
        {
            new PickListItem(
                id: Guid.NewGuid(),
                pickListId: pickListId,
                itemId: componentItemId,
                warehouseId: warehouseId,
                qty: 12m,
                stockQty: 12m,
                itemName: "Component",
                batchId: null,
                productBundleItemId: packedRowId)
            {
                SourceDocumentItemId = soItemId
            }
        };

        // In stock UOM, 1 Box = 12 Nos
        var bundleMapInStockUom = new Dictionary<Guid, decimal>
        {
            [componentItemId] = 12m
        };

        var pickedBundles = PickListManager.ComputePickedQtyForBundle(pickItems, soItemId, bundleMapInStockUom);

        pickedBundles.ShouldBe(1);
    }

    [Fact]
    public async Task ProductBundleDecompositionService_DecomposeAsync_HonorsProductBundleIdOverride()
    {
        var parentItemId = Guid.NewGuid();
        var comp1 = Guid.NewGuid();
        var comp2 = Guid.NewGuid();

        // Bundle v1: 2 comp1
        var bundleV1 = new ProductBundle(Guid.NewGuid(), parentItemId) { IsActive = false }; // old version
        bundleV1.AddItem(comp1, 2m, "Comp 1");

        // Bundle v2: 4 comp2 (currently active)
        var bundleV2 = new ProductBundle(Guid.NewGuid(), parentItemId) { IsActive = true };
        bundleV2.AddItem(comp2, 4m, "Comp 2");

        var bundleRepo = Substitute.For<IRepository<ProductBundle, Guid>>();
        bundleRepo.GetQueryableAsync().Returns(Task.FromResult(new[] { bundleV1, bundleV2 }.AsQueryable()));

        var service = new ProductBundleDecompositionService(bundleRepo);

        // Call DecomposeAsync specifying bundleV1 explicitly (order row version)
        var decomposed = await service.DecomposeAsync(
            itemId: parentItemId,
            transactionQty: 3m,
            parentRate: 100m,
            productBundleId: bundleV1.Id);

        decomposed.Count.ShouldBe(1);
        decomposed[0].ComponentItemId.ShouldBe(comp1);
        decomposed[0].Qty.ShouldBe(6m); // 2 * 3 = 6
        decomposed[0].ParentBundleId.ShouldBe(bundleV1.Id);
    }

    // --- ERPNext PR #62153 & #60274: Chart of Accounts Importer validations ---

    [Fact]
    public void ChartOfAccountsImportService_ValidateRootTypes_RejectsMissingRootType()
    {
        // Only Asset, Liability, Equity, Revenue (Expense missing)
        var rows = new List<CoaTemplateRow>
        {
            new CoaTemplateRow("1000", "Assets", AccountType.Asset, isGroup: true),
            new CoaTemplateRow("2000", "Liabilities", AccountType.Liability, isGroup: true),
            new CoaTemplateRow("3000", "Equity", AccountType.Equity, isGroup: true),
            new CoaTemplateRow("4000", "Revenue", AccountType.Revenue, isGroup: true),
        };

        var ex = Should.Throw<BusinessException>(() =>
            ChartOfAccountsImportService.ValidateRootTypes(rows));

        ex.Code.ShouldBe(MyERPDomainErrorCodes.ValidationFailed);
        ex.Data["detail"]!.ToString()!.ShouldContain("Expense");
    }

    [Fact]
    public void ChartOfAccountsImportService_ValidateMandatoryAccountTypes_RejectsMissingBankOrCash()
    {
        // Has all roots, but missing Bank and Cash
        var rows = new List<CoaTemplateRow>
        {
            new CoaTemplateRow("1000", "Assets", AccountType.Asset, isGroup: true),
            new CoaTemplateRow("2000", "Liabilities", AccountType.Liability, isGroup: true),
            new CoaTemplateRow("3000", "Equity", AccountType.Equity, isGroup: true),
            new CoaTemplateRow("4000", "Revenue", AccountType.Revenue, isGroup: true),
            new CoaTemplateRow("5000", "Expenses", AccountType.Expense, isGroup: true),

            new CoaTemplateRow("1130", "Accounts Receivable", AccountType.Asset, parentCode: "1000", subType: AccountSubType.AccountsReceivable),
            new CoaTemplateRow("1140", "Inventory", AccountType.Asset, parentCode: "1000", subType: AccountSubType.Stock),
            new CoaTemplateRow("1210", "Equipment", AccountType.Asset, parentCode: "1000", subType: AccountSubType.FixedAsset),
            new CoaTemplateRow("2110", "Accounts Payable", AccountType.Liability, parentCode: "2000", subType: AccountSubType.AccountsPayable),
            new CoaTemplateRow("5100", "Cost of Goods Sold", AccountType.Expense, parentCode: "5000", subType: AccountSubType.CostOfGoodsSold),
            new CoaTemplateRow("5200", "Depreciation Expense", AccountType.Expense, parentCode: "5000", subType: AccountSubType.DepreciationExpense),
            new CoaTemplateRow("5300", "Stock Adjustment", AccountType.Expense, parentCode: "5000"),
        };

        var ex = Should.Throw<BusinessException>(() =>
            ChartOfAccountsImportService.ValidateMandatoryAccountTypes(rows));

        ex.Code.ShouldBe(MyERPDomainErrorCodes.ValidationFailed);
        ex.Data["detail"]!.ToString()!.ShouldContain("Bank");
        ex.Data["detail"]!.ToString()!.ShouldContain("Cash");
    }

    [Fact]
    public void ChartOfAccountsImportService_StandardTemplates_PassAllValidations()
    {
        var myTemplate = ChartOfAccountsImportService.GetMalaysianTemplate();
        Should.NotThrow(() => ChartOfAccountsImportService.ValidateRootTypes(myTemplate));
        Should.NotThrow(() => ChartOfAccountsImportService.ValidateMandatoryAccountTypes(myTemplate));
    }

    // --- ERPNext PR #61415 (commit c2679f01a6): Bank Account Balance Validations ---

    [Fact]
    public async Task BankAccountBalanceAppService_CreateAsync_RejectsNonCompanyBankAccount()
    {
        var balanceRepo = Substitute.For<IRepository<BankAccountBalance, Guid>>();
        var bankAccountRepo = Substitute.For<IRepository<BankAccount, Guid>>();
        var companyRepo = Substitute.For<IRepository<Company, Guid>>();

        var nonCompanyAccount = new BankAccount(Guid.NewGuid(), Guid.NewGuid(), "Customer Bank Account", Guid.NewGuid(), "Customer Bank")
        {
            IsCompanyAccount = false
        };
        bankAccountRepo.GetAsync(nonCompanyAccount.Id).Returns(Task.FromResult(nonCompanyAccount));

        var appService = new BankAccountBalanceAppService(balanceRepo, bankAccountRepo, companyRepo);

        var ex = await Should.ThrowAsync<BusinessException>(() => appService.CreateAsync(new CreateUpdateBankAccountBalanceDto
        {
            BankAccountId = nonCompanyAccount.Id,
            Date = DateTime.UtcNow.Date,
            Balance = 5000m
        }));

        ex.Code.ShouldBe(MyERPDomainErrorCodes.ValidationFailed);
        ex.Data["detail"]!.ToString()!.ShouldContain("Closing balances can only be recorded for company bank accounts");
    }

    [Fact]
    public async Task BankAccountBalanceAppService_CreateAsync_RejectsDuplicateDateForSameAccount()
    {
        var balanceRepo = Substitute.For<IRepository<BankAccountBalance, Guid>>();
        var bankAccountRepo = Substitute.For<IRepository<BankAccount, Guid>>();
        var companyRepo = Substitute.For<IRepository<Company, Guid>>();

        var companyAccount = new BankAccount(Guid.NewGuid(), Guid.NewGuid(), "Company Operating Account", Guid.NewGuid(), "Maybank")
        {
            IsCompanyAccount = true
        };
        bankAccountRepo.GetAsync(companyAccount.Id).Returns(Task.FromResult(companyAccount));

        var targetDate = new DateTime(2026, 10, 26);
        var existing = new BankAccountBalance(Guid.NewGuid(), companyAccount.Id, targetDate, 1000m);
        balanceRepo.GetQueryableAsync().Returns(Task.FromResult(new[] { existing }.AsQueryable()));

        var appService = new BankAccountBalanceAppService(balanceRepo, bankAccountRepo, companyRepo);

        var ex = await Should.ThrowAsync<BusinessException>(() => appService.CreateAsync(new CreateUpdateBankAccountBalanceDto
        {
            BankAccountId = companyAccount.Id,
            Date = targetDate,
            Balance = 2000m
        }));

        ex.Code.ShouldBe(MyERPDomainErrorCodes.DuplicateRecord);
        ex.Data["detail"]!.ToString()!.ShouldContain("A closing balance already exists");
    }

    // --- ERPNext PR #62092 (commit 60ba2d388b): Account for returned deliveries in credit limits ---

    [Fact]
    public async Task CreditLimitService_GetCustomerOutstandingAsync_DeductsReturnedDeliveryNoteQty()
    {
        var customerId = Guid.NewGuid();
        var companyId = Guid.NewGuid();

        var customer = new Customer(customerId, companyId, "Test Customer") { CreditLimit = 1000m };
        var customerRepo = Substitute.For<IRepository<Customer, Guid>>();
        customerRepo.GetAsync(customerId).Returns(Task.FromResult(customer));

        var invoiceRepo = Substitute.For<IRepository<SalesInvoice, Guid>>();
        invoiceRepo.GetQueryableAsync().Returns(Task.FromResult(Array.Empty<SalesInvoice>().AsQueryable()));

        var orderRepo = Substitute.For<IRepository<SalesOrder, Guid>>();
        var creditLimitRepo = Substitute.For<IRepository<CustomerCreditLimit, Guid>>();
        creditLimitRepo.GetQueryableAsync().Returns(Task.FromResult(Array.Empty<CustomerCreditLimit>().AsQueryable()));

        var settingProvider = Substitute.For<ISettingProvider>();

        // Standalone delivery note: 4 units at 100 each = 400 total
        var dn = new DeliveryNote(Guid.NewGuid(), companyId, customerId, Guid.NewGuid(), "DN-0001", DateTime.UtcNow.Date);
        dn.AddItem(Guid.NewGuid(), "Item 1", 4m, 100m, 0m);
        var item = dn.Items[0];
        item.ReturnedQty = 3m; // 3 returned!
        item.BilledQty = 0m;

        dn.Submit(); // Status = Submitted

        var dnRepo = Substitute.For<IRepository<DeliveryNote, Guid>>();
        dnRepo.GetQueryableAsync().Returns(Task.FromResult(new[] { dn }.AsQueryable()));

        var serviceProvider = Substitute.For<IServiceProvider>();
        serviceProvider.GetService(typeof(IRepository<DeliveryNote, Guid>)).Returns(dnRepo);

        var creditLimitService = new CreditLimitService(customerRepo, invoiceRepo, orderRepo, creditLimitRepo, settingProvider);
        creditLimitService.LazyServiceProvider = new Volo.Abp.DependencyInjection.AbpLazyServiceProvider(serviceProvider);

        var outstanding = await creditLimitService.GetCustomerOutstandingAsync(customerId, companyId);

        // Net unbilled = 4 - 0 - 3 = 1 unit => (1 / 4) * 400 = 100 exposure (NOT 400!)
        outstanding.ShouldBe(100m);
    }

    // --- ERPNext PR #61620 (commit 442ddcf6f5 & 1662d7ba59): Dunning cancellation requires payment cancellation ---

    [Fact]
    public void Dunning_Cancel_RequiresPaymentCancellationFirst()
    {
        var d = new Dunning(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow.Date, 1);
        d.AddOverduePayment(Guid.NewGuid(), 500m, DateTime.UtcNow.AddDays(-10).Date, 10);
        d.DunningFee = 50m;
        d.Submit();
        d.RecordDunningPayment(50m);

        var ex = Should.Throw<BusinessException>(() => d.Cancel());
        ex.Code.ShouldBe(MyERPDomainErrorCodes.ValidationFailed);
        ex.Data["detail"]!.ToString()!.ShouldContain("Cannot cancel Dunning with paid fees or interest");

        // Cancel payment first -> reverses paid amount
        d.RecordDunningPayment(-50m);
        d.Cancel();
        d.Status.ShouldBe(DocumentStatus.Cancelled);
    }

    // --- ERPNext PR #61620 (commit 308143fc67): Income account is required to collect dunning fees or interest ---

    [Fact]
    public async Task DunningAppService_SubmitAsync_ThrowsWhenIncomeAccountMissingForFeeOrInterest()
    {
        var companyId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var d = new Dunning(Guid.NewGuid(), companyId, customerId, new DateTime(2026, 3, 1), 1);
        d.AddOverduePayment(Guid.NewGuid(), 500m, new DateTime(2026, 2, 1), 10);
        d.DunningFee = 25m; // Has fee

        var dunningRepo = Substitute.For<IRepository<Dunning, Guid>>();
        dunningRepo.WithDetailsAsync().Returns(Task.FromResult(new[] { d }.AsQueryable()));

        var dunningManager = new DunningManager(dunningRepo);

        var companyRepo = Substitute.For<IRepository<Company, Guid>>();
        var company = new Company(companyId, "Test Company")
        {
            DefaultReceivableAccountId = Guid.NewGuid(),
            DefaultIncomeAccountId = null // Missing income account!
        };
        companyRepo.GetAsync(companyId).Returns(Task.FromResult(company));

        var fyRepo = Substitute.For<IRepository<FiscalYear, Guid>>();
        var fy = new FiscalYear(Guid.NewGuid(), companyId, "2026", new DateTime(2026, 1, 1), new DateTime(2026, 12, 31));
        fyRepo.GetQueryableAsync().Returns(Task.FromResult(new[] { fy }.AsQueryable()));

        var lazyProvider = Substitute.For<IAbpLazyServiceProvider>();
        lazyProvider.LazyGetRequiredService<IRepository<Company, Guid>>().Returns(companyRepo);
        lazyProvider.LazyGetRequiredService<IRepository<FiscalYear, Guid>>().Returns(fyRepo);

        var appService = new DunningAppService(dunningRepo, dunningManager)
        {
            LazyServiceProvider = lazyProvider
        };

        var ex = await Should.ThrowAsync<BusinessException>(() => appService.SubmitAsync(d.Id));
        ex.Code.ShouldBe(MyERPDomainErrorCodes.ValidationFailed);
        ex.Data["detail"]!.ToString()!.ShouldContain("Income Account is required to collect dunning fees or interest.");
    }

    // --- ERPNext PR #62251 (commit ef89cb497d): Unreconcile payment reverses FX gain/loss for both voucher and against-vouchers ---

    [Fact]
    public async Task UnreconcilePaymentAppService_SubmitAsync_ReversesExchangeGainLossForBothVoucherAndAgainstVoucher()
    {
        var companyId = Guid.NewGuid();
        var peId = Guid.NewGuid();
        var siId = Guid.NewGuid();
        var pleId = Guid.NewGuid();
        var ple = new PaymentLedgerEntry(
            pleId, companyId, DateTime.UtcNow.Date,
            Guid.NewGuid(), "Customer", Guid.NewGuid(),
            "PaymentEntry", peId,
            "SalesInvoice", siId,
            150m, 150m, "MYR");

        var unrecon = new UnreconcilePayment(Guid.NewGuid(), companyId, UnreconcileVoucherType.PaymentEntry, peId);
        unrecon.AddAllocation(pleId, "SalesInvoice", siId, 150m);

        var unreconRepo = Substitute.For<IRepository<UnreconcilePayment, Guid>>();
        unreconRepo.WithDetailsAsync().Returns(Task.FromResult(new[] { unrecon }.AsQueryable()));

        var pleRepo = Substitute.For<IRepository<PaymentLedgerEntry, Guid>>();
        pleRepo.GetAsync(pleId).Returns(Task.FromResult(ple));

        var postingOrchestrator = Substitute.For<DocumentPostingOrchestrator>();

        var activityLogRepo = Substitute.For<IRepository<MyERP.Core.Entities.DocumentActivityLog, Guid>>();
        var lazyProvider = Substitute.For<IAbpLazyServiceProvider>();
        lazyProvider.LazyGetRequiredService<IRepository<MyERP.Core.Entities.DocumentActivityLog, Guid>>().Returns(activityLogRepo);

        var appService = new UnreconcilePaymentAppService(unreconRepo, pleRepo, postingOrchestrator)
        {
            LazyServiceProvider = lazyProvider
        };

        await appService.SubmitAsync(unrecon.Id);

        // Verifies FX gain/loss reversed for the primary voucher
        await postingOrchestrator.Received(1).ReverseExchangeGainLossJournalEntriesAsync("PaymentEntry", peId);

        // Verifies FX gain/loss also reversed for linked against voucher (return / invoice)
        await postingOrchestrator.Received(1).ReverseExchangeGainLossJournalEntriesAsync("SalesInvoice", siId);
    }

    // --- ERPNext PR #61620 / commits 58e9c6f8a8, 1662d7ba59, a5f08e36ec: Dunning Payment Linking & Cancellation ---

    [Fact]
    public async Task DunningAppService_CancelAsync_ThrowsWhenActivePaymentEntryLinked()
    {
        var dunningId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        var customerId = Guid.NewGuid();

        var dunning = new Dunning(dunningId, companyId, customerId, DateTime.UtcNow, 1);
        dunning.AddOverduePayment(Guid.NewGuid(), 100m, DateTime.UtcNow.AddDays(-10), 10);
        dunning.Submit();

        var dunningRepo = Substitute.For<IRepository<Dunning, Guid>>();
        dunningRepo.GetAsync(dunningId).Returns(Task.FromResult(dunning));

        var peId = Guid.NewGuid();
        var pe = new PaymentEntry(peId, companyId, PaymentType.Receive, DateTime.UtcNow, 100m, Guid.NewGuid(), Guid.NewGuid())
        {
            PaymentNumber = "PE-2026-0001"
        };
        pe.Submit();

        var peTax = new PaymentEntryTax(Guid.NewGuid(), peId, Guid.NewGuid())
        {
            DunningId = dunningId,
            TaxAmount = 25m
        };

        var peTaxRepo = Substitute.For<IRepository<PaymentEntryTax, Guid>>();
        peTaxRepo.GetQueryableAsync().Returns(Task.FromResult(new[] { peTax }.AsQueryable()));

        var peRepo = Substitute.For<IRepository<PaymentEntry, Guid>>();
        peRepo.GetQueryableAsync().Returns(Task.FromResult(new[] { pe }.AsQueryable()));

        var lazyProvider = Substitute.For<IAbpLazyServiceProvider>();
        lazyProvider.LazyGetRequiredService<IRepository<PaymentEntryTax, Guid>>().Returns(peTaxRepo);
        lazyProvider.LazyGetRequiredService<IRepository<PaymentEntry, Guid>>().Returns(peRepo);

        var dunningManager = new DunningManager(dunningRepo);

        var appService = new DunningAppService(dunningRepo, dunningManager)
        {
            LazyServiceProvider = lazyProvider
        };

        var ex = await Should.ThrowAsync<BusinessException>(async () => await appService.CancelAsync(dunningId));
        ex.Code.ShouldBe(MyERPDomainErrorCodes.ValidationFailed);
        ex.Data["detail"]!.ToString().ShouldContain("Cannot cancel Dunning");
        ex.Data["detail"]!.ToString().ShouldContain("PE-2026-0001");
    }

    [Fact]
    public void Dunning_Cancel_ThrowsWhenPaidDunningAmountGreaterThanZero()
    {
        var dunning = new Dunning(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow, 1);
        dunning.AddOverduePayment(Guid.NewGuid(), 100m, DateTime.UtcNow.AddDays(-10), 10);
        dunning.Submit();
        dunning.RecordDunningPayment(50m);

        var ex = Should.Throw<BusinessException>(() => dunning.Cancel());
        ex.Code.ShouldBe(MyERPDomainErrorCodes.ValidationFailed);
        ex.Data["detail"]!.ToString().ShouldContain("Cannot cancel Dunning with paid fees or interest");
    }

    [Fact]
    public async Task PaymentEntryAppService_SubmitAsync_ThrowsWhenDunningNotSubmitted()
    {
        var companyId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var dunningId = Guid.NewGuid();

        var dunning = new Dunning(dunningId, companyId, customerId, DateTime.UtcNow, 1);
        // Note: dunning left in Draft status!

        var dunningRepo = Substitute.For<IRepository<Dunning, Guid>>();
        dunningRepo.GetQueryableAsync().Returns(Task.FromResult(new[] { dunning }.AsQueryable()));

        var peId = Guid.NewGuid();
        var pe = new PaymentEntry(peId, companyId, PaymentType.Receive, DateTime.UtcNow, 100m, Guid.NewGuid(), Guid.NewGuid())
        {
            PartyType = "Customer",
            PartyId = customerId,
            PaymentNumber = "PE-2026-TEST"
        };
        pe.AddTax(new PaymentEntryTax(Guid.NewGuid(), peId, Guid.NewGuid())
        {
            DunningId = dunningId,
            AddDeductTax = TaxAddDeduct.Add,
            TaxAmount = 20m
        });

        var peRepo = Substitute.For<IRepository<PaymentEntry, Guid>>();
        peRepo.GetAsync(peId).Returns(Task.FromResult(pe));

        var lazyProvider = Substitute.For<IAbpLazyServiceProvider>();
        lazyProvider.LazyGetRequiredService<IRepository<Dunning, Guid>>().Returns(dunningRepo);
        lazyProvider.LazyGetRequiredService<IRepository<DunningType, Guid>>().Returns(Substitute.For<IRepository<DunningType, Guid>>());
        lazyProvider.LazyGetRequiredService<IRepository<global::MyERP.Core.Entities.Company, Guid>>().Returns(Substitute.For<IRepository<global::MyERP.Core.Entities.Company, Guid>>());
        lazyProvider.LazyGetRequiredService<IRepository<ModeOfPayment, Guid>>().Returns(Substitute.For<IRepository<ModeOfPayment, Guid>>());

        var appService = new PaymentEntryAppService(
            peRepo,
            Substitute.For<IRepository<SalesInvoice, Guid>>(),
            Substitute.For<IRepository<MyERP.Purchasing.Entities.PurchaseInvoice, Guid>>(),
            Substitute.For<IRepository<PaymentScheduleEntry, Guid>>(),
            Substitute.For<IRepository<PaymentLedgerEntry, Guid>>(),
            Substitute.For<MyERP.Core.DomainServices.IDocumentNumberGenerator>(),
            Substitute.For<DocumentPostingOrchestrator>(),
            Substitute.For<IRepository<MyERP.Core.Entities.DocumentActivityLog, Guid>>())
        {
            LazyServiceProvider = lazyProvider
        };

        var ex = await Should.ThrowAsync<BusinessException>(async () => await appService.SubmitAsync(peId));
        ex.Code.ShouldBe(MyERPDomainErrorCodes.ValidationFailed);
        ex.Data["detail"]!.ToString().ShouldContain("must be submitted");
    }

    [Fact]
    public async Task PaymentEntryAppService_SubmitAsync_ThrowsWhenCustomerMismatch()
    {
        var companyId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var otherCustomerId = Guid.NewGuid();
        var dunningId = Guid.NewGuid();

        var dunning = new Dunning(dunningId, companyId, customerId, DateTime.UtcNow, 1);
        dunning.AddOverduePayment(Guid.NewGuid(), 100m, DateTime.UtcNow.AddDays(-10), 10);
        dunning.Submit();

        var dunningRepo = Substitute.For<IRepository<Dunning, Guid>>();
        dunningRepo.GetQueryableAsync().Returns(Task.FromResult(new[] { dunning }.AsQueryable()));

        var peId = Guid.NewGuid();
        var pe = new PaymentEntry(peId, companyId, PaymentType.Receive, DateTime.UtcNow, 100m, Guid.NewGuid(), Guid.NewGuid())
        {
            PartyType = "Customer",
            PartyId = otherCustomerId, // Mismatched customer!
            PaymentNumber = "PE-2026-TEST"
        };
        pe.AddTax(new PaymentEntryTax(Guid.NewGuid(), peId, Guid.NewGuid())
        {
            DunningId = dunningId,
            AddDeductTax = TaxAddDeduct.Add,
            TaxAmount = 20m
        });

        var peRepo = Substitute.For<IRepository<PaymentEntry, Guid>>();
        peRepo.GetAsync(peId).Returns(Task.FromResult(pe));

        var lazyProvider = Substitute.For<IAbpLazyServiceProvider>();
        lazyProvider.LazyGetRequiredService<IRepository<Dunning, Guid>>().Returns(dunningRepo);
        lazyProvider.LazyGetRequiredService<IRepository<DunningType, Guid>>().Returns(Substitute.For<IRepository<DunningType, Guid>>());
        lazyProvider.LazyGetRequiredService<IRepository<global::MyERP.Core.Entities.Company, Guid>>().Returns(Substitute.For<IRepository<global::MyERP.Core.Entities.Company, Guid>>());
        lazyProvider.LazyGetRequiredService<IRepository<ModeOfPayment, Guid>>().Returns(Substitute.For<IRepository<ModeOfPayment, Guid>>());

        var appService = new PaymentEntryAppService(
            peRepo,
            Substitute.For<IRepository<SalesInvoice, Guid>>(),
            Substitute.For<IRepository<MyERP.Purchasing.Entities.PurchaseInvoice, Guid>>(),
            Substitute.For<IRepository<PaymentScheduleEntry, Guid>>(),
            Substitute.For<IRepository<PaymentLedgerEntry, Guid>>(),
            Substitute.For<MyERP.Core.DomainServices.IDocumentNumberGenerator>(),
            Substitute.For<DocumentPostingOrchestrator>(),
            Substitute.For<IRepository<MyERP.Core.Entities.DocumentActivityLog, Guid>>())
        {
            LazyServiceProvider = lazyProvider
        };

        var ex = await Should.ThrowAsync<BusinessException>(async () => await appService.SubmitAsync(peId));
        ex.Code.ShouldBe(MyERPDomainErrorCodes.ValidationFailed);
        ex.Data["detail"]!.ToString().ShouldContain("belongs to a different customer");
    }

    // --- ERPNext PR #62149 / commit ffcf97c223: Product Bundle disabled version in returns vs standard ---

    [Fact]
    public async Task ProductBundleDecompositionService_DecomposeAsync_ThrowsWhenChosenBundleIsDisabledAndNotReturn()
    {
        var parentItemId = Guid.NewGuid();
        var bundleId = Guid.NewGuid();
        var bundle = new ProductBundle(bundleId, parentItemId)
        {
            IsDisabled = true
        };
        bundle.AddItem(Guid.NewGuid(), 2m, "Component A", "Unit");

        var bundleRepo = Substitute.For<IRepository<ProductBundle, Guid>>();
        bundleRepo.GetQueryableAsync().Returns(Task.FromResult(new[] { bundle }.AsQueryable()));

        var service = new ProductBundleDecompositionService(bundleRepo);

        var ex = await Should.ThrowAsync<BusinessException>(async () =>
            await service.DecomposeAsync(parentItemId, 1m, 100m, isReturn: false, productBundleId: bundleId));

        ex.Code.ShouldBe(MyERPDomainErrorCodes.ValidationFailed);
        ex.Data["detail"]!.ToString().ShouldContain("disabled and cannot be used");
    }

    [Fact]
    public async Task ProductBundleDecompositionService_DecomposeAsync_AllowsDisabledBundleWhenIsReturn()
    {
        var parentItemId = Guid.NewGuid();
        var bundleId = Guid.NewGuid();
        var bundle = new ProductBundle(bundleId, parentItemId)
        {
            IsDisabled = true
        };
        bundle.AddItem(Guid.NewGuid(), 2m, "Component A", "Unit");

        var bundleRepo = Substitute.For<IRepository<ProductBundle, Guid>>();
        bundleRepo.GetQueryableAsync().Returns(Task.FromResult(new[] { bundle }.AsQueryable()));

        var service = new ProductBundleDecompositionService(bundleRepo);

        var result = await service.DecomposeAsync(parentItemId, 1m, 100m, isReturn: true, productBundleId: bundleId);

        result.Count.ShouldBe(1);
        result[0].Qty.ShouldBe(2m);
    }
}
