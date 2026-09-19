using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Accounting;
using MyERP.Accounting.DomainServices;
using MyERP.Accounting.Entities;
using MyERP.Core.Entities;
using MyERP.Inventory;
using MyERP.Inventory.DomainServices;
using MyERP.Inventory.Entities;
using MyERP.Sales;
using MyERP.Sales.Entities;
using NSubstitute;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace MyERP.Domain.Tests;

public class UpstreamBatch59142To59181Tests
{
    // --- PR #59081: Bank Reconciliation Date Range Validation ---

    [Fact]
    public async Task BankReconciliation_GetTransactions_Throws_WhenDateFromGreaterThanDateTo()
    {
        var repo = Substitute.For<IRepository<BankTransaction, Guid>>();
        var peRepo = Substitute.For<IRepository<PaymentEntry, Guid>>();
        var jeRepo = Substitute.For<IRepository<JournalEntry, Guid>>();
        var baRepo = Substitute.For<IRepository<BankAccount, Guid>>();

        var appService = new BankReconciliationAppService(repo, peRepo, jeRepo, baRepo, null!, null!);

        var input = new GetBankTransactionsDto
        {
            BankAccountId = Guid.NewGuid(),
            DateFrom = new DateTime(2026, 9, 20),
            DateTo = new DateTime(2026, 9, 10),
        };

        var ex = await Should.ThrowAsync<BusinessException>(() => appService.GetTransactionsAsync(input));
        ex.Code.ShouldBe(MyERPDomainErrorCodes.DateRangeInvalid);
    }

    // --- PR #59142: Shipping Rule Account Company Validation ---

    [Fact]
    public void ShippingRule_ValidateAccountCompany_Throws_WhenCompanyMismatch()
    {
        var companyA = Guid.NewGuid();
        var companyB = Guid.NewGuid();

        var rule = new ShippingRule(
            Guid.NewGuid(),
            "Standard Shipping",
            ShippingRuleType.Selling,
            ShippingCalculationMode.Fixed,
            Guid.NewGuid(),
            companyId: companyA);

        var accountOfCompanyB = new Account(
            Guid.NewGuid(),
            companyB,
            "5001",
            "Freight Out",
            AccountType.Expense);

        var ex = Should.Throw<BusinessException>(() => rule.ValidateAccountCompany(accountOfCompanyB));
        ex.Code.ShouldBe(MyERPDomainErrorCodes.ShippingRuleAccountCompanyMismatch);
    }

    [Fact]
    public void ShippingRule_ValidateAccountCompany_Succeeds_WhenCompanyMatches()
    {
        var companyA = Guid.NewGuid();

        var rule = new ShippingRule(
            Guid.NewGuid(),
            "Standard Shipping",
            ShippingRuleType.Selling,
            ShippingCalculationMode.Fixed,
            Guid.NewGuid(),
            companyId: companyA);

        var accountOfCompanyA = new Account(
            Guid.NewGuid(),
            companyA,
            "5001",
            "Freight Out",
            AccountType.Expense);

        Should.NotThrow(() => rule.ValidateAccountCompany(accountOfCompanyA));
    }

    // --- PR #59155: Putaway Rule Continuation Past Undersized Whole-UOM Rule ---

    [Fact]
    public async Task PutawayRule_SkipsUndersizedWholeUomRule_AndAllocatesToNextCandidate()
    {
        var companyId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var whSmall = Guid.NewGuid();
        var whLarge = Guid.NewGuid();

        // Small warehouse capacity 0.5 (priority 1) - undersized for whole unit
        var ruleSmall = new PutawayRule(Guid.NewGuid(), companyId, whSmall)
        {
            ItemId = itemId,
            Priority = 1,
            StockCapacity = 0.5m,
        };

        // Large warehouse capacity 20 (priority 2)
        var ruleLarge = new PutawayRule(Guid.NewGuid(), companyId, whLarge)
        {
            ItemId = itemId,
            Priority = 2,
            StockCapacity = 20m,
        };

        var ruleRepo = Substitute.For<IRepository<PutawayRule, Guid>>();
        var rules = new List<PutawayRule> { ruleSmall, ruleLarge }.AsQueryable();
        ruleRepo.GetQueryableAsync().Returns(Task.FromResult(rules));

        var binRepo = Substitute.For<IRepository<Bin, Guid>>();
        var bins = new List<Bin>().AsQueryable();
        binRepo.GetQueryableAsync().Returns(Task.FromResult(bins));

        var putawayService = new PutawayService(ruleRepo, binRepo);

        // When mustBeWholeNumber = true, ruleSmall allows Math.Floor(0.5) = 0.
        // PR #59155 ensures allocation continues to ruleLarge instead of breaking.
        var allocations = await putawayService.AllocateAsync(
            companyId,
            itemId,
            totalQty: 2m,
            mustBeWholeNumber: true);

        allocations.Count.ShouldBe(1);
        allocations[0].WarehouseId.ShouldBe(whLarge);
        allocations[0].Qty.ShouldBe(2m);
    }

    // --- PR #59170: Pick List Respects Manual Picking ---

    [Fact]
    public void PickList_PickManually_DefaultsFalse()
    {
        var pl = new PickList(Guid.NewGuid(), Guid.NewGuid(), "Delivery");
        pl.PickManually.ShouldBeFalse();
    }

    [Fact]
    public async Task PickListManager_AllocateStockAsync_DoesNotAllocate_WhenPickManuallyIsTrue()
    {
        var pl = new PickList(Guid.NewGuid(), Guid.NewGuid(), "Delivery")
        {
            PickManually = true
        };
        var itemId = Guid.NewGuid();
        var whId = Guid.NewGuid();
        pl.AddItem(itemId, whId, 10m);

        var plRepo = Substitute.For<IRepository<PickList, Guid>>();
        var binRepo = Substitute.For<IRepository<Bin, Guid>>();
        var manager = new PickListManager(plRepo, binRepo);

        var result = await manager.AllocateStockAsync(pl);
        result.Allocations.ShouldBeEmpty();
        result.HasShortage.ShouldBeFalse();
    }

    // --- PR #59181: Revaluation Journal Filter Clarification in AR/AP Reports ---

    [Fact]
    public void AgingReportRequestDto_And_AgingReport_SupportIncludeRevaluationJournals()
    {
        var request = new AgingReportRequestDto
        {
            CompanyId = Guid.NewGuid(),
            IncludeRevaluationJournals = true,
        };
        request.IncludeRevaluationJournals.ShouldBeTrue();

        var report = new AgingReport
        {
            ReportType = "Receivable",
            IncludeRevaluationJournals = true,
        };
        report.IncludeRevaluationJournals.ShouldBeTrue();

        var dto = new AgingReportDto
        {
            ReportType = "Receivable",
            IncludeRevaluationJournals = true,
        };
        dto.IncludeRevaluationJournals.ShouldBeTrue();
    }
}
