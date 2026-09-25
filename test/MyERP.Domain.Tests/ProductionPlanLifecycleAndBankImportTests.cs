using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NSubstitute;
using Shouldly;
using Xunit;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using MyERP.Accounting;
using MyERP.Accounting.Entities;
using MyERP.Inventory.DomainServices;
using MyERP.Manufacturing;
using MyERP.Manufacturing.Entities;
using MyERP.Manufacturing.EventHandlers;
using MyERP.Manufacturing.Events;

namespace MyERP.Domain.Tests;

public class ProductionPlanLifecycleAndBankImportTests
{
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid ItemId = Guid.NewGuid();
    private static readonly Guid BomId = Guid.NewGuid();
    private static readonly Guid WarehouseId = Guid.NewGuid();

    [Fact]
    public void ClosedPlan_StaysClosed_OnProductionProgress()
    {
        // Per ERPNext PR #59454: keep a closed production plan closed until it is reopened
        var plan = new ProductionPlan(Guid.NewGuid(), CompanyId, "PP-CLOSED-01", DateTime.UtcNow);
        var item = new ProductionPlanItem(Guid.NewGuid(), plan.Id, ItemId, "FG Item", BomId, 10m);
        plan.AddPlannedItem(item);
        plan.Submit();
        plan.Close();

        plan.Status.ShouldBe(ProductionPlanStatus.Closed);

        // Subsequent production completion must not override Closed status
        plan.UpdateProducedStatus(allCompleted: true, hasProduction: true);
        plan.Status.ShouldBe(ProductionPlanStatus.Closed);

        plan.UpdateProducedStatus(allCompleted: false, hasProduction: true);
        plan.Status.ShouldBe(ProductionPlanStatus.Closed);

        // Reopen explicitly restores Submitted / InProgress
        plan.Reopen();
        plan.Status.ShouldBe(ProductionPlanStatus.Submitted);
    }

    [Fact]
    public void ProductionPlan_UpdateProducedStatus_TransitionsToCompleted_And_InProgress()
    {
        // Per ERPNext PR #59449: status transitions when items produced
        var plan = new ProductionPlan(Guid.NewGuid(), CompanyId, "PP-PROD-01", DateTime.UtcNow);
        var item = new ProductionPlanItem(Guid.NewGuid(), plan.Id, ItemId, "FG Item", BomId, 10m);
        plan.AddPlannedItem(item);
        plan.Submit();
        plan.Status.ShouldBe(ProductionPlanStatus.Submitted);

        // Partial production -> InProgress
        plan.UpdateProducedStatus(allCompleted: false, hasProduction: true);
        plan.Status.ShouldBe(ProductionPlanStatus.InProgress);

        // Full production & WOs completed -> Completed
        plan.UpdateProducedStatus(allCompleted: true, hasProduction: true);
        plan.Status.ShouldBe(ProductionPlanStatus.Completed);

        // Cancellation / un-manufacture -> reverts completion back to InProgress
        plan.UpdateProducedStatus(allCompleted: false, hasProduction: true);
        plan.Status.ShouldBe(ProductionPlanStatus.InProgress);
    }

    [Fact]
    public async Task EventHandler_ReleasesReservationOnCompleteAndClose_RestoresOnReopen()
    {
        // Per ERPNext PR #59449: plan reservations are released when completed and on close
        var planId = Guid.NewGuid();
        var plan = new ProductionPlan(planId, CompanyId, "PP-RES-01", DateTime.UtcNow);
        var mrItem = new ProductionPlanMrItem(Guid.NewGuid(), planId, ItemId, "RM Item", 15m)
        {
            WarehouseId = WarehouseId
        };
        plan.AddMaterialRequirement(mrItem);

        var planRepo = Substitute.For<IRepository<ProductionPlan, Guid>>();
        planRepo.GetAsync(planId, includeDetails: true).Returns(plan);

        var binRepo = Substitute.For<IRepository<global::MyERP.Inventory.Entities.Bin, Guid>>();
        var binQueryable = new List<global::MyERP.Inventory.Entities.Bin>().AsQueryable();
        binRepo.GetQueryableAsync().Returns(Task.FromResult(binQueryable));
        var binService = Substitute.For<BinService>(binRepo);


        var handler = new ProductionPlanEventHandler(planRepo, binService);

        // 1. Submit -> +15 reserved
        await handler.HandleEventAsync(new ProductionPlanSubmittedEvent(planId, null));
        await binService.Received(1).UpdateReservedQtyForProductionPlanAsync(ItemId, WarehouseId, 15m, null);

        // 2. Complete -> -15 reserved (released)
        await handler.HandleEventAsync(new ProductionPlanCompletedEvent(planId, null));
        await binService.Received(1).UpdateReservedQtyForProductionPlanAsync(ItemId, WarehouseId, -15m, null);

        // 3. Completion Reverted -> +15 reserved (restored)
        await handler.HandleEventAsync(new ProductionPlanCompletionRevertedEvent(planId, null));
        await binService.Received(2).UpdateReservedQtyForProductionPlanAsync(ItemId, WarehouseId, 15m, null);

        // 4. Closed -> -15 reserved (released)
        await handler.HandleEventAsync(new ProductionPlanClosedEvent(planId, null));
        await binService.Received(2).UpdateReservedQtyForProductionPlanAsync(ItemId, WarehouseId, -15m, null);

        // 5. Reopened -> +15 reserved (restored)
        await handler.HandleEventAsync(new ProductionPlanReopenedEvent(planId, null));
        await binService.Received(3).UpdateReservedQtyForProductionPlanAsync(ItemId, WarehouseId, 15m, null);
    }

    [Fact]
    public async Task BankStatementImport_RejectsNonCompanyBankAccount()
    {
        // Per ERPNext PR #59447 & commit 48720781ce: show/allow only company bank accounts
        var bankAccountId = Guid.NewGuid();
        var glAccountId = Guid.NewGuid();
        var bankAccount = new BankAccount(bankAccountId, CompanyId, "Personal Account", glAccountId, "CIMB")
        {
            IsCompanyAccount = false
        };

        var bankAccountRepo = Substitute.For<IRepository<BankAccount, Guid>>();
        bankAccountRepo.GetAsync(bankAccountId).Returns(bankAccount);

        var txRepo = Substitute.For<IRepository<BankTransaction, Guid>>();
        var guidGen = Substitute.For<IGuidGenerator>();

        var appService = new BankStatementImportAppService(txRepo, bankAccountRepo, guidGen);

        var input = new BankStatementImportInput
        {
            CompanyId = CompanyId,
            BankAccountId = bankAccountId,
            CsvContent = "Date,Description,Debit,Credit,Balance\n2026-09-01,Payment,100,0,900"
        };

        var ex = await Should.ThrowAsync<BusinessException>(async () =>
            await appService.ImportFromCsvAsync(input));

        ex.Code.ShouldBe(MyERPDomainErrorCodes.BankAccountMustBeCompanyAccount);
    }

    [Fact]
    public async Task BankStatementImport_RejectsCompanyMismatch()
    {
        var otherCompanyId = Guid.NewGuid();
        var bankAccountId = Guid.NewGuid();
        var glAccountId = Guid.NewGuid();
        var bankAccount = new BankAccount(bankAccountId, otherCompanyId, "CIMB Company", glAccountId, "CIMB")
        {
            IsCompanyAccount = true
        };


        var bankAccountRepo = Substitute.For<IRepository<BankAccount, Guid>>();
        bankAccountRepo.GetAsync(bankAccountId).Returns(bankAccount);

        var txRepo = Substitute.For<IRepository<BankTransaction, Guid>>();
        var guidGen = Substitute.For<IGuidGenerator>();

        var appService = new BankStatementImportAppService(txRepo, bankAccountRepo, guidGen);

        var input = new BankStatementImportInput
        {
            CompanyId = CompanyId,
            BankAccountId = bankAccountId,
            CsvContent = "Date,Description,Debit,Credit,Balance\n2026-09-01,Payment,100,0,900"
        };

        var ex = await Should.ThrowAsync<BusinessException>(async () =>
            await appService.ImportFromCsvAsync(input));

        ex.Code.ShouldBe(MyERPDomainErrorCodes.BankAccountCompanyMismatch);
    }
}
