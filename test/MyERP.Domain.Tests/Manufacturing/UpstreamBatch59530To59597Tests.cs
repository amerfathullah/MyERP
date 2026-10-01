using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using MyERP.Accounting;
using MyERP.Accounting.Entities;
using MyERP.Core;
using MyERP.Core.DomainServices;
using MyERP.Core.Entities;
using MyERP.Inventory;
using MyERP.Inventory.DomainServices;
using MyERP.Inventory.Entities;
using MyERP.Manufacturing;
using MyERP.Manufacturing.Entities;
using MyERP.Manufacturing.Services;
using MyERP.Purchasing;
using MyERP.Purchasing.Entities;
using MyERP.Settings;
using NSubstitute;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Settings;
using Xunit;

namespace MyERP.Domain.Tests.Manufacturing;

public class UpstreamBatch59530To59597Tests
{
    private readonly Guid _companyId = Guid.NewGuid();
    private readonly Guid _itemId = Guid.NewGuid();
    private readonly Guid _warehouseId = Guid.NewGuid();

    // =========================================================================
    // PR #59597: Value batch and serial returns at the receipt rate
    // =========================================================================
    [Fact]
    public async Task SubcontractingReceipt_Return_ValuedAtOriginalReceiptRate()
    {
        var scrRepo = Substitute.For<IRepository<SubcontractingReceipt, Guid>>();
        var numberGen = Substitute.For<IDocumentNumberGenerator>();
        numberGen.GenerateAsync("SCR-RET", _companyId).Returns("SCR-RET-0001");

        var origScr = new SubcontractingReceipt(
            Guid.NewGuid(), _companyId, "SCR-0001", DateTime.UtcNow.AddDays(-2),
            Guid.NewGuid(), Guid.NewGuid());
        var origItem = new SubcontractingReceiptItem(
            Guid.NewGuid(), origScr.Id, _itemId, "Subcontract FG", 10m, 55.50m);
        origScr.AddItem(origItem);
        origScr.Submit();

        scrRepo.GetAsync(origScr.Id, includeDetails: true).Returns(origScr);
        scrRepo.GetQueryableAsync().Returns(Task.FromResult(new List<SubcontractingReceipt>().AsQueryable()));

        var valService = Substitute.For<StockValuationService>(
            Substitute.For<IRepository<StockLedgerEntry, Guid>>(),
            Substitute.For<IRepository<Item, Guid>>(),
            Substitute.For<ISettingProvider>());
        var binService = Substitute.For<BinService>(
            Substitute.For<IRepository<Bin, Guid>>());

        var appService = new SubcontractingAppService(
            Substitute.For<IRepository<SubcontractingOrder, Guid>>(),
            scrRepo,
            numberGen,
            valService,
            binService);

        SubcontractingReceipt? insertedReceipt = null;
        await scrRepo.InsertAsync(Arg.Do<SubcontractingReceipt>(r => insertedReceipt = r), autoSave: true);

        var result = await appService.CreateReceiptReturnAsync(new CreateSubcontractingReceiptReturnDto
        {
            ReturnAgainstReceiptId = origScr.Id,
            PostingDate = DateTime.UtcNow,
            Items = new List<CreateScrReturnItemDto>
            {
                new()
                {
                    ItemId = _itemId,
                    ItemName = "Subcontract FG",
                    Qty = 4m,
                    Rate = 0m // Zero passed by user, must be overridden with orig receipt rate
                }
            }
        });

        insertedReceipt.ShouldNotBeNull();
        insertedReceipt.IsReturn.ShouldBeTrue();
        insertedReceipt.Items.Count.ShouldBe(1);
        insertedReceipt.Items[0].Qty.ShouldBe(-4m);
        insertedReceipt.Items[0].Rate.ShouldBe(55.50m);
        insertedReceipt.Items[0].Amount.ShouldBe(-4m * 55.50m);
    }

    // =========================================================================
    // PR #59530: Cancel reconciliation entries when recreating stock ledgers
    // =========================================================================
    [Fact]
    public async Task StockReconciliation_Cancel_MarksExistingSlesAsCancelled()
    {
        var srRepo = Substitute.For<IRepository<StockReconciliation, Guid>>();
        var sleRepo = Substitute.For<IRepository<StockLedgerEntry, Guid>>();
        var binRepo = Substitute.For<IRepository<Bin, Guid>>();
        var jeRepo = Substitute.For<IRepository<JournalEntry, Guid>>();
        var itemRepo = Substitute.For<IRepository<Item, Guid>>();
        var sreRepo = Substitute.For<IRepository<StockReservationEntry, Guid>>();
        var settingProvider = Substitute.For<ISettingProvider>();

        var sr = new StockReconciliation(Guid.NewGuid(), _companyId, DateTime.UtcNow)
        {
            ExpenseAccountId = Guid.NewGuid()
        };
        sr.AddItem(_itemId, _warehouseId, 10m, 100m, 0m, 0m, "Nos");
        sr.Submit();

        var queryableSr = new List<StockReconciliation> { sr }.AsQueryable();
        srRepo.WithDetailsAsync().Returns(Task.FromResult(queryableSr));
        sreRepo.GetQueryableAsync().Returns(Task.FromResult(new List<StockReservationEntry>().AsQueryable()));

        var fyRepo = Substitute.For<IRepository<FiscalYear, Guid>>();
        var fy = new FiscalYear(Guid.NewGuid(), _companyId, "2026", DateTime.UtcNow.AddYears(-1), DateTime.UtcNow.AddYears(1));
        fyRepo.GetQueryableAsync().Returns(Task.FromResult(new List<FiscalYear> { fy }.AsQueryable()));

        var numberGen = Substitute.For<IDocumentNumberGenerator>();
        numberGen.GenerateAsync("JE", _companyId).Returns("JE-0001");

        var whAccountService = Substitute.For<WarehouseAccountService>(
            Substitute.For<IRepository<WarehouseAccount, Guid>>(),
            Substitute.For<IRepository<Warehouse, Guid>>(),
            Substitute.For<IRepository<Company, Guid>>(),
            Substitute.For<IRepository<Account, Guid>>());
        whAccountService.ResolveStockAccountAsync(_warehouseId, _companyId, Arg.Any<Guid?>()).Returns(Guid.NewGuid());

        var existingSle = new StockLedgerEntry(
            Guid.NewGuid(), _companyId, _itemId, _warehouseId, DateTime.UtcNow,
            10m, 100m, 10m, 1000m)
        {
            VoucherType = "StockReconciliation",
            VoucherId = sr.Id
        };
        var existingSles = new List<StockLedgerEntry> { existingSle };

        sleRepo.GetListAsync(Arg.Any<System.Linq.Expressions.Expression<Func<StockLedgerEntry, bool>>>())
            .Returns(callInfo =>
            {
                var predicate = callInfo.Arg<System.Linq.Expressions.Expression<Func<StockLedgerEntry, bool>>>().Compile();
                return Task.FromResult(existingSles.Where(predicate).ToList());
            });

        var item = new Item(_itemId, _companyId, "ITEM-01", "Item 1", ItemType.Goods)
        {
            AllowNegativeStock = true
        };
        itemRepo.GetAsync(_itemId).Returns(item);

        var valService = new StockValuationService(sleRepo, itemRepo, settingProvider);
        var valSp = Substitute.For<Volo.Abp.DependencyInjection.IAbpLazyServiceProvider>();
        valSp.LazyGetService<Volo.Abp.Guids.IGuidGenerator>().Returns(Volo.Abp.Guids.SimpleGuidGenerator.Instance);
        typeof(Volo.Abp.Domain.Services.DomainService)
            .GetProperty("LazyServiceProvider", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)?
            .SetValue(valService, valSp);

        var binService = new BinService(binRepo);

        var appService = new StockReconciliationAppService(
            srRepo, jeRepo, fyRepo, valService, binService, whAccountService, numberGen);

        var lazySp = Substitute.For<Volo.Abp.DependencyInjection.IAbpLazyServiceProvider>();
        lazySp.LazyGetRequiredService<IRepository<StockReservationEntry, Guid>>().Returns(sreRepo);
        lazySp.LazyGetRequiredService<IRepository<StockLedgerEntry, Guid>>().Returns(sleRepo);
        lazySp.LazyGetRequiredService<IRepository<SerialAndBatchBundle, Guid>>().Returns(Substitute.For<IRepository<SerialAndBatchBundle, Guid>>());
        lazySp.LazyGetRequiredService<IRepository<MyERP.Core.Entities.DocumentActivityLog, Guid>>().Returns(Substitute.For<IRepository<MyERP.Core.Entities.DocumentActivityLog, Guid>>());

        typeof(Volo.Abp.Application.Services.ApplicationService)
            .GetProperty("LazyServiceProvider", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)?
            .SetValue(appService, lazySp);

        await appService.CancelAsync(sr.Id);

        sr.Status.ShouldBe(DocumentStatus.Cancelled);
        existingSle.IsCancelled.ShouldBeTrue();
        await sleRepo.Received().UpdateManyAsync(Arg.Is<IEnumerable<StockLedgerEntry>>(list => list.All(s => s.IsCancelled)));
    }

    // =========================================================================
    // PR #59567: Block manufacture dialog when process loss leaves no finished good
    // =========================================================================
    [Fact]
    public void WorkOrderProductionService_ValidateAndGetProductionParams_Throws_WhenProcessLossLeavesNoFg()
    {
        var woRepo = Substitute.For<IRepository<WorkOrder, Guid>>();
        var service = new WorkOrderProductionService(woRepo);

        var wo = new WorkOrder(Guid.NewGuid(), _companyId, "WO-001", _itemId, Guid.NewGuid(), 10m);
        wo.Submit();
        wo.Start();

        // produceQty <= 0 with processLossQty > 0
        var ex = Should.Throw<BusinessException>(() =>
        {
            service.ValidateAndGetProductionParams(wo, produceQty: 0m, processLossQty: 2m);
        });

        (ex.Data["detail"]?.ToString() ?? "").ShouldContain("Qty for Manufacture must be greater than the process loss");
    }

    [Fact]
    public async Task ManufacturingAppService_CreateManufactureStockEntryAsync_Throws_WhenProcessLossExceedsFgQty()
    {
        var woRepo = Substitute.For<IRepository<WorkOrder, Guid>>();
        var wo = new WorkOrder(Guid.NewGuid(), _companyId, "WO-001", _itemId, Guid.NewGuid(), 10m);
        wo.Submit();
        wo.Start();
        woRepo.GetAsync(wo.Id, includeDetails: true).Returns(wo);

        var valService = Substitute.For<StockValuationService>(
            Substitute.For<IRepository<StockLedgerEntry, Guid>>(),
            Substitute.For<IRepository<Item, Guid>>(),
            Substitute.For<ISettingProvider>());
        var binService = Substitute.For<BinService>(
            Substitute.For<IRepository<Bin, Guid>>());

        var appService = new ManufacturingAppService(
            Substitute.For<IRepository<BillOfMaterials, Guid>>(),
            woRepo,
            Substitute.For<IRepository<MaterialRequest, Guid>>(),
            Substitute.For<IDocumentNumberGenerator>(),
            valService,
            binService);

        var ex = await Should.ThrowAsync<BusinessException>(async () =>
        {
            await appService.CreateManufactureStockEntryAsync(new CreateManufactureStockEntryDto
            {
                WorkOrderId = wo.Id,
                FgQuantity = 2m,
                ProcessLossQty = 2m
            });
        });

        (ex.Data["detail"]?.ToString() ?? "").ShouldContain("Qty for Manufacture must be greater than the process loss of 2 to produce a finished good.");
    }

    // =========================================================================
    // PR #59573: Respect serial / batch activation in stock settings and item
    // =========================================================================
    [Fact]
    public async Task ErpSettingsAppService_SetAsync_Throws_WhenDisablingSerialBatchWithTrackedItems()
    {
        var itemRepo = Substitute.For<IRepository<Item, Guid>>();
        var trackedItem = new Item(Guid.NewGuid(), _companyId, "ITEM-TRACKED", "Tracked Widget", ItemType.Goods)
        {
            HasSerialNo = true
        };
        itemRepo.GetQueryableAsync().Returns(Task.FromResult(new List<Item> { trackedItem }.AsQueryable()));

        var settingProvider = Substitute.For<ISettingProvider>();
        var settingManager = Substitute.For<Volo.Abp.SettingManagement.ISettingManager>();

        var appService = new ErpSettingsAppService(settingProvider, settingManager);
        var lazySp = Substitute.For<Volo.Abp.DependencyInjection.IAbpLazyServiceProvider>();
        lazySp.LazyGetRequiredService<IRepository<Item, Guid>>().Returns(itemRepo);

        typeof(Volo.Abp.Application.Services.ApplicationService)
            .GetProperty("LazyServiceProvider", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)?
            .SetValue(appService, lazySp);

        var ex = await Should.ThrowAsync<BusinessException>(async () =>
        {
            await appService.SetAsync(MyERPSettings.Stock.EnableSerialAndBatchNoForItem, "false");
        });

        (ex.Data["detail"]?.ToString() ?? "").ShouldContain("Cannot disable Serial and Batch No for Item, as there are items with serial / batch enabled.");
    }

    // =========================================================================
    // PR #59549: Read company when account filters are applied in Invoice Discounting
    // =========================================================================
    [Fact]
    public async Task InvoiceDiscountingAppService_CreateAsync_Throws_WhenAccountBelongsToDifferentCompany()
    {
        var invDiscRepo = Substitute.For<IRepository<InvoiceDiscounting, Guid>>();
        var accountRepo = Substitute.For<IRepository<Account, Guid>>();

        var otherCompanyId = Guid.NewGuid();
        var loanAccountId = Guid.NewGuid();
        var loanAccount = new Account(loanAccountId, otherCompanyId, "2100", "Bank Loan", AccountType.Liability)
        {
            IsGroup = false
        };
        var validAccounts = new List<Account> { loanAccount };
        accountRepo.GetQueryableAsync().Returns(Task.FromResult(validAccounts.AsQueryable()));

        var appService = new InvoiceDiscountingAppService(
            null!, invDiscRepo, null!, null!, null!, null!, null!);

        var lazySp = Substitute.For<Volo.Abp.DependencyInjection.IAbpLazyServiceProvider>();
        lazySp.LazyGetRequiredService<IRepository<Account, Guid>>().Returns(accountRepo);

        typeof(Volo.Abp.Application.Services.ApplicationService)
            .GetProperty("LazyServiceProvider", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)?
            .SetValue(appService, lazySp);

        var ex = await Should.ThrowAsync<BusinessException>(async () =>
        {
            await appService.CreateAsync(new CreateInvoiceDiscountingDto
            {
                CompanyId = _companyId,
                PostingDate = DateTime.UtcNow,
                ShortTermLoanAccountId = loanAccountId,
                BankAccountId = Guid.NewGuid(),
                BankChargesAccountId = Guid.NewGuid(),
                AccountsReceivableCreditAccountId = Guid.NewGuid(),
                AccountsReceivableDiscountedAccountId = Guid.NewGuid(),
                AccountsReceivableUnpaidAccountId = Guid.NewGuid(),
                Invoices = new List<CreateInvoiceDiscountingInvoiceDto>
                {
                    new() { SalesInvoiceId = Guid.NewGuid(), OutstandingAmount = 500m }
                }
            });
        });

        (ex.Data["detail"]?.ToString() ?? "").ShouldContain($"Account {loanAccountId} does not belong to company {_companyId}");
    }

    // =========================================================================
    // PR #59370: Tie break backdated SLE ordering by creation time
    // =========================================================================
    [Fact]
    public async Task StockValuationService_GetPreviousSleAsync_BreaksTiesByCreationTime()
    {
        var sleRepo = Substitute.For<IRepository<StockLedgerEntry, Guid>>();
        var itemRepo = Substitute.For<IRepository<Item, Guid>>();
        var settingProvider = Substitute.For<ISettingProvider>();

        var postingTime = new DateTime(2026, 1, 6, 10, 0, 0, DateTimeKind.Utc);

        var sle1 = new StockLedgerEntry(
            Guid.NewGuid(), _companyId, _itemId, _warehouseId, postingTime,
            100m, 10m, 100m, 1000m)
        {
            PostingDateTime = postingTime
        };
        SetCreationTime(sle1, new DateTime(2026, 1, 6, 10, 0, 1, DateTimeKind.Utc));

        var sle2 = new StockLedgerEntry(
            Guid.NewGuid(), _companyId, _itemId, _warehouseId, postingTime,
            -5m, 10m, 95m, 950m)
        {
            PostingDateTime = postingTime
        };
        SetCreationTime(sle2, new DateTime(2026, 1, 6, 10, 0, 2, DateTimeKind.Utc));

        var sle3 = new StockLedgerEntry(
            Guid.NewGuid(), _companyId, _itemId, _warehouseId, postingTime,
            10m, 10m, 105m, 1050m)
        {
            PostingDateTime = postingTime
        };
        SetCreationTime(sle3, new DateTime(2026, 1, 6, 10, 0, 3, DateTimeKind.Utc));

        var allSles = new List<StockLedgerEntry> { sle1, sle2, sle3 };
        sleRepo.GetQueryableAsync().Returns(Task.FromResult(allSles.AsQueryable()));

        var service = new StockValuationService(sleRepo, itemRepo, settingProvider);

        // Previous SLE before sle3 (creationTime = 10:00:03) should be sle2 (10:00:02)
        var prev = await service.GetPreviousSleAsync(
            _itemId, _warehouseId, postingTime, creationTime: sle3.CreationTime);

        prev.ShouldNotBeNull();
        prev.Id.ShouldBe(sle2.Id);
    }

    private static void SetCreationTime(object entity, DateTime creationTime)
    {
        var prop = entity.GetType().GetProperty("CreationTime", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        prop?.SetValue(entity, creationTime);
    }
}
