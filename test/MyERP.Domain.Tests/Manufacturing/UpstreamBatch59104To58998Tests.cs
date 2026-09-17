using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MyERP.Accounting;
using MyERP.Accounting.DomainServices;
using MyERP.Accounting.Entities;
using MyERP.Core;
using MyERP.Inventory;
using MyERP.Inventory.Entities;
using MyERP.Manufacturing;
using MyERP.Manufacturing.Entities;
using MyERP.Sales.DomainServices;
using MyERP.Sales.Entities;
using NSubstitute;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace MyERP.Domain.Tests.Manufacturing;

public class UpstreamBatch59104To58998Tests
{
    // --- PR #59104 / commit 470a1a5477: Job Card zero completed quantity with process loss ---

    [Fact]
    public void JobCard_Complete_AllowsZeroCompletedQty_WhenProcessLossIsPositive()
    {
        var jc = new JobCard(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), forQuantity: 5, sequenceId: 1);
        jc.Start();
        jc.SetProcessLossQty(5);

        // Completing with 0 completed quantity and 5 process loss should succeed
        jc.Complete();

        jc.Status.ShouldBe(JobCardStatus.Completed);
        jc.CompletedQty.ShouldBe(0);
        jc.ProcessLossQty.ShouldBe(5);
    }

    [Fact]
    public void JobCard_Complete_AllowsZeroCompletedQty_WhenPendingQtyIsPositive()
    {
        var jc = new JobCard(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), forQuantity: 10, sequenceId: 1);
        jc.Start();
        jc.SetPendingQty(4);

        // Completing with 0 completed and 0 process loss, but pending qty > 0 should succeed
        jc.Complete();

        jc.Status.ShouldBe(JobCardStatus.Completed);
        jc.CompletedQty.ShouldBe(0);
        jc.PendingQty.ShouldBe(4);
    }

    [Fact]
    public void JobCard_Complete_Throws_WhenCompletedProcessLossAndPending_AllZeroOrNegative()
    {
        var jc = new JobCard(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), forQuantity: 10, sequenceId: 1);
        jc.Start();

        var ex = Should.Throw<BusinessException>(() => jc.Complete());
        ex.Code.ShouldBe(MyERPDomainErrorCodes.ValidationFailed);
    }

    [Fact]
    public void JobCard_SetPendingQty_Throws_WhenNegative()
    {
        var jc = new JobCard(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), forQuantity: 10, sequenceId: 1);

        var ex = Should.Throw<BusinessException>(() => jc.SetPendingQty(-1));
        ex.Code.ShouldBe(MyERPDomainErrorCodes.AmountMustBePositive);
    }

    [Fact]
    public void JobCard_SetProcessLoss_ComputesFromForQuantityAndCompleted()
    {
        var jc = new JobCard(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), forQuantity: 10, sequenceId: 1);
        jc.Start();
        // Add time log with 6 completed
        jc.AddTimeLog(DateTime.UtcNow.AddMinutes(-30), DateTime.UtcNow, completedQty: 6);
        jc.SetProcessLoss();

        jc.CompletedQty.ShouldBe(6);
        jc.ProcessLossQty.ShouldBe(4); // 10 - 6 = 4

        jc.Complete();
        jc.Status.ShouldBe(JobCardStatus.Completed);
    }

    [Fact]
    public void JobCard_SetProcessLoss_DeductsPendingQty()
    {
        var jc = new JobCard(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), forQuantity: 10, sequenceId: 1);
        jc.Start();
        // Completed 6, pending 4 -> process loss should be 0 (10 - 6 - 4 = 0)
        jc.AddTimeLog(DateTime.UtcNow.AddMinutes(-30), DateTime.UtcNow, completedQty: 6);
        jc.SetPendingQty(4);
        jc.SetProcessLoss();

        jc.CompletedQty.ShouldBe(6);
        jc.PendingQty.ShouldBe(4);
        jc.ProcessLossQty.ShouldBe(0);

        jc.Complete();
        jc.Status.ShouldBe(JobCardStatus.Completed);
    }

    [Fact]
    public void JobCard_Complete_AutoCalculatesProcessLoss_WhenNotExplicitlyCalled()
    {
        var jc = new JobCard(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), forQuantity: 10, sequenceId: 1);
        jc.Start();
        jc.AddTimeLog(DateTime.UtcNow.AddMinutes(-30), DateTime.UtcNow, completedQty: 7);
        jc.SetPendingQty(2);

        // Complete() should automatically invoke SetProcessLoss() -> 10 - 7 - 2 = 1
        jc.Complete();

        jc.CompletedQty.ShouldBe(7);
        jc.PendingQty.ShouldBe(2);
        jc.ProcessLossQty.ShouldBe(1);
        jc.Status.ShouldBe(JobCardStatus.Completed);
    }

    // --- PR #58923 / commit 396ba45b4c: Item Group group warehouse/account validation ---

    [Fact]
    public async Task ItemGroupAppService_CreateAsync_Throws_WhenDefaultWarehouseIsGroup()
    {
        var itemGroupRepo = Substitute.For<IRepository<ItemGroup, Guid>>();
        var whRepo = Substitute.For<IRepository<Warehouse, Guid>>();
        var serviceProvider = Substitute.For<IServiceProvider>();

        serviceProvider.GetService(typeof(IRepository<Warehouse, Guid>)).Returns(whRepo);

        var whId = Guid.NewGuid();
        var groupWh = new Warehouse(whId, Guid.NewGuid(), "Group Warehouse")
        {
            IsGroup = true
        };
        whRepo.FindAsync(whId).Returns(groupWh);

        var appService = new ItemGroupAppService(itemGroupRepo);
        var lazySp = Substitute.For<Volo.Abp.DependencyInjection.IAbpLazyServiceProvider>();
        lazySp.LazyGetRequiredService<IRepository<Warehouse, Guid>>().Returns(whRepo);

        typeof(Volo.Abp.Application.Services.ApplicationService)
            .GetProperty("LazyServiceProvider", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public)?
            .SetValue(appService, lazySp);

        var ex = await Should.ThrowAsync<BusinessException>(async () =>
        {
            await appService.CreateAsync(new CreateItemGroupDto
            {
                Name = "Raw Materials",
                DefaultWarehouseId = whId
            });
        });

        ex.Code.ShouldBe(MyERPDomainErrorCodes.GroupWarehouseCannotReceiveStock);
    }

    [Fact]
    public async Task ItemGroupAppService_CreateAsync_Throws_WhenDefaultInventoryAccountIsGroup()
    {
        var itemGroupRepo = Substitute.For<IRepository<ItemGroup, Guid>>();
        var accRepo = Substitute.For<IRepository<Account, Guid>>();

        var accId = Guid.NewGuid();
        var groupAcc = new Account(accId, Guid.NewGuid(), "1500", "Stock Assets", AccountType.Asset)
        {
            IsGroup = true
        };
        accRepo.FindAsync(accId).Returns(groupAcc);

        var appService = new ItemGroupAppService(itemGroupRepo);
        var lazySp = Substitute.For<Volo.Abp.DependencyInjection.IAbpLazyServiceProvider>();
        lazySp.LazyGetRequiredService<IRepository<Account, Guid>>().Returns(accRepo);

        typeof(Volo.Abp.Application.Services.ApplicationService)
            .GetProperty("LazyServiceProvider", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public)?
            .SetValue(appService, lazySp);

        var ex = await Should.ThrowAsync<BusinessException>(async () =>
        {
            await appService.CreateAsync(new CreateItemGroupDto
            {
                Name = "Raw Materials",
                DefaultInventoryAccountId = accId
            });
        });

        ex.Code.ShouldBe(MyERPDomainErrorCodes.AccountIsGroup);
    }

    // --- PR #58751 / commit 683f033c34: Sales Order billing allowance headroom ---

    [Fact]
    public void SalesOrderManager_HasPotentiallyBillableItems_DetectsAllowanceHeadroom()
    {
        var soId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var itemId = Guid.NewGuid();

        var so = new SalesOrder(soId, companyId, customerId, "SO-2026-0001", DateTime.UtcNow);
        so.AddItem(itemId, "Test Item", quantity: 10, unitPrice: 100m, taxAmount: 0m, uom: "Unit");
        so.Submit();

        var soItem = so.Items[0];
        soItem.BilledQty = 10; // fully billed: billedAmt = 1000, amount = 1000

        // With 0% allowance: no headroom
        SalesOrderManager.HasPotentiallyBillableItems(so, null, globalOverBillingAllowance: 0m)
            .ShouldBeFalse();

        // With global allowance 20%: headroom exists (1000 < 1000 * 1.2 = 1200)
        SalesOrderManager.HasPotentiallyBillableItems(so, null, globalOverBillingAllowance: 20m)
            .ShouldBeTrue();

        // With global allowance 0%, but item-level allowance 50%: headroom exists
        var itemAllowances = new Dictionary<Guid, decimal> { [itemId] = 50m };
        SalesOrderManager.HasPotentiallyBillableItems(so, itemAllowances, globalOverBillingAllowance: 0m)
            .ShouldBeTrue();

        // With item-level allowance 0% overriding global 20%? No, item allowance > 0 takes precedence; if 0, global is used.
        var zeroItemAllowance = new Dictionary<Guid, decimal> { [itemId] = 0m };
        SalesOrderManager.HasPotentiallyBillableItems(so, zeroItemAllowance, globalOverBillingAllowance: 0m)
            .ShouldBeFalse();

        // Closed line is skipped
        soItem.IsClosed = true;
        SalesOrderManager.HasPotentiallyBillableItems(so, itemAllowances, globalOverBillingAllowance: 20m)
            .ShouldBeFalse();
    }

    [Fact]
    public void SalesOrderManager_ZeroAmountRow_HeadroomBasedOnPendingBillingQty()
    {
        var soId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var freeItemId = Guid.NewGuid();

        var so = new SalesOrder(soId, companyId, customerId, "SO-2026-FREE", DateTime.UtcNow);
        so.AddItem(freeItemId, "Free Sample Item", quantity: 5, unitPrice: 0m, taxAmount: 0m, uom: "Unit");
        so.Submit();

        var freeItem = so.Items[0];
        // Unbilled: has headroom (5 pending)
        SalesOrderManager.HasPotentiallyBillableItems(so).ShouldBeTrue();

        // Partially billed: still has headroom (2 pending)
        freeItem.BilledQty = 3;
        SalesOrderManager.HasPotentiallyBillableItems(so).ShouldBeTrue();

        // Fully billed: no headroom left (0 pending) per PR #58816
        freeItem.BilledQty = 5;
        SalesOrderManager.HasPotentiallyBillableItems(so).ShouldBeFalse();
    }

    // --- PR #59084 / commit 4e3e301c90: Financial Report Template formula evaluation and reference validation ---

    [Fact]
    public void FinancialReportTemplate_ValidReferenceCodes_PassValidation()
    {
        var template = new FinancialReportTemplate(Guid.NewGuid(), "Test Template", FinancialReportType.BalanceSheet);
        template.AddRow("Revenue", FinancialReportDataSource.AccountData, 1, "REV");
        template.AddRow("Current Assets", FinancialReportDataSource.AccountData, 2, "CA100");
        template.AddRow("Cash Flow 2", FinancialReportDataSource.AccountData, 3, "cash_flow_2");

        var errors = template.ValidateFormulas();
        errors.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("REV-COGS", "Invalid line reference format")]
    [InlineData("123REV", "Invalid line reference format")]
    [InlineData("REV COGS", "Invalid line reference format")]
    [InlineData("abs", "is a reserved name")]
    [InlineData("sum", "is a reserved name")]
    [InlineData("round", "is a reserved name")]
    [InlineData("class", "is a reserved name")]
    [InlineData("if", "is a reserved name")]
    public void FinancialReportTemplate_HyphenAndReservedWords_Rejected(string referenceCode, string expectedErrorSubstring)
    {
        var template = new FinancialReportTemplate(Guid.NewGuid(), "Invalid Template", FinancialReportType.ProfitAndLoss);
        template.AddRow("Row", FinancialReportDataSource.AccountData, 1, referenceCode);

        var errors = template.ValidateFormulas();
        errors.ShouldContain(e => e.Contains(expectedErrorSubstring));
    }

    [Fact]
    public void FinancialReportFormulaEngine_EvaluatesFunctions_AndTrimsFormula()
    {
        var refs = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
        {
            ["A"] = 16m,
            ["B"] = 4m,
            ["C"] = 2m
        };

        // Trimming + sqrt
        FinancialReportFormulaEngine.EvaluateFormula("  sqrt(A)  ", refs).ShouldBe(4m);

        // min, max, sum
        FinancialReportFormulaEngine.EvaluateFormula("min(A, B, C)", refs).ShouldBe(2m);
        FinancialReportFormulaEngine.EvaluateFormula("max(A, B, C)", refs).ShouldBe(16m);
        FinancialReportFormulaEngine.EvaluateFormula("sum(A, B, C)", refs).ShouldBe(22m);

        // pow
        FinancialReportFormulaEngine.EvaluateFormula("pow(B, C)", refs).ShouldBe(16m);

        // Division by zero returns 0 without crashing
        FinancialReportFormulaEngine.EvaluateFormula("A / 0", refs).ShouldBe(0m);
    }

    // --- PR #59120 / commit ded6df3614: reset ordered_qty when Sales Order is cancelled ---

    [Fact]
    public void SalesOrder_Cancel_ResetsOrderedQtyOnItems()
    {
        var soId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var itemId = Guid.NewGuid();

        var so = new SalesOrder(soId, companyId, customerId, "SO-2026-RESET", DateTime.UtcNow);
        so.AddItem(itemId, "Item 1", quantity: 10, unitPrice: 100m, taxAmount: 0m, uom: "Unit");
        so.Submit();

        var line = so.Items[0];
        line.OrderedQty = 10m; // Simulating PO created for this SO line

        so.Cancel();

        so.Status.ShouldBe(DocumentStatus.Cancelled);
        line.OrderedQty.ShouldBe(0m);
    }
}

