using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Core;
using MyERP.Core.DomainServices;
using MyERP.Core.Entities;
using MyERP.Sales.DomainServices;
using NSubstitute;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace MyERP.Domain.Tests;

/// <summary>
/// Unit tests for upstream ERPNext parity batch:
/// - PR #62324 / commits 16e6c794b5, a5f27756a9, eec3bcf900, 7d493a6678: Sales target non-negative bounds and duplicate prevention.
/// - PR #61835 / commits 32569b28fb, 1fe56c013a, 53f265e9c5, 6869de7511, 633d0c22c3: Sales person disable restrictions and sales team validations.
/// - PR #60804 / commit f8741089cd: Party Type Account Type mutation guard when ledger entries exist.
/// - PR #61829 / commits 25a7a1e594, 0e0a66ef0b: Leaf supplier group validation and duplicate name checks.
/// - PR #61828 / commits 167f2f656e, 03771aebd8: Quotation lost reason requirement.
/// - PR #62149 / commits f787f4942e, ffcf97c223, 05bc9ce0fe: Repeated component product bundle decomposition and returns with disabled bundles.
/// </summary>
public class UpstreamParitySellingAndSetupBatchTests
{
    [Fact]
    public void SalesPerson_AddTarget_RejectsNegativeQtyOrAmount_PR62324()
    {
        var sp = new global::MyERP.Sales.Entities.SalesPerson(Guid.NewGuid(), "Ali");
        var fyId = Guid.NewGuid();

        var exQty = Should.Throw<BusinessException>(() =>
            sp.AddTarget(fyId, targetQty: -10m, targetAmount: 5000m));
        exQty.Code.ShouldBe(MyERPDomainErrorCodes.ValidationFailed);

        var exAmt = Should.Throw<BusinessException>(() =>
            sp.AddTarget(fyId, targetQty: 10m, targetAmount: -500m));
        exAmt.Code.ShouldBe(MyERPDomainErrorCodes.ValidationFailed);
    }

    [Fact]
    public void SalesPerson_AddTarget_RejectsDuplicateFiscalYearAndItemGroup_PR62324()
    {
        var sp = new global::MyERP.Sales.Entities.SalesPerson(Guid.NewGuid(), "Siti");
        var fyId = Guid.NewGuid();
        var igId = Guid.NewGuid();

        // First target succeeds
        sp.AddTarget(fyId, targetQty: 100m, targetAmount: 20000m, itemGroupId: igId);
        sp.Targets.Count.ShouldBe(1);

        // Duplicate fiscal year + item group throws
        var ex = Should.Throw<BusinessException>(() =>
            sp.AddTarget(fyId, targetQty: 50m, targetAmount: 10000m, itemGroupId: igId));
        ex.Code.ShouldBe(MyERPDomainErrorCodes.ValidationFailed);

        // Different item group succeeds
        sp.AddTarget(fyId, targetQty: 50m, targetAmount: 10000m, itemGroupId: Guid.NewGuid());
        sp.Targets.Count.ShouldBe(2);

        // Overall target (null item group) succeeds
        sp.AddTarget(fyId, targetQty: 200m, targetAmount: 40000m, itemGroupId: null);
        sp.Targets.Count.ShouldBe(3);
    }

    [Fact]
    public async Task SalesPerson_DisableAsync_BlocksWhenUnfinishedSalesOrderExists_PR61835()
    {
        var spRepo = Substitute.For<IRepository<global::MyERP.Sales.Entities.SalesPerson, Guid>>();
        var teamRepo = Substitute.For<IRepository<global::MyERP.Sales.Entities.SalesTeamEntry, Guid>>();
        var soRepo = Substitute.For<IRepository<global::MyERP.Sales.Entities.SalesOrder, Guid>>();
        var dnRepo = Substitute.For<IRepository<global::MyERP.Sales.Entities.DeliveryNote, Guid>>();
        var logRepo = Substitute.For<IRepository<global::MyERP.Core.Entities.DocumentActivityLog, Guid>>();

        var spId = Guid.NewGuid();
        var soId = Guid.NewGuid();
        var sp = new global::MyERP.Sales.Entities.SalesPerson(spId, "John Doe");
        spRepo.GetAsync(spId).Returns(Task.FromResult(sp));

        var teamEntry = new global::MyERP.Sales.Entities.SalesTeamEntry(
            Guid.NewGuid(), spId, "SalesOrder", soId, 100m, 1000m, 5m);
        teamRepo.GetQueryableAsync().Returns(Task.FromResult(new List<global::MyERP.Sales.Entities.SalesTeamEntry> { teamEntry }.AsQueryable()));

        var unfinishedSo = new global::MyERP.Sales.Entities.SalesOrder(
            soId, Guid.NewGuid(), Guid.NewGuid(), "SO-UNF-001", DateTime.UtcNow);
        unfinishedSo.AddItem(Guid.NewGuid(), "SO Item", 1m, 100m, 0m, "Unit");
        unfinishedSo.Submit(); // Status = ToDeliverAndBill (unfinished)

        soRepo.GetQueryableAsync().Returns(Task.FromResult(new List<global::MyERP.Sales.Entities.SalesOrder> { unfinishedSo }.AsQueryable()));
        dnRepo.GetQueryableAsync().Returns(Task.FromResult(new List<global::MyERP.Sales.Entities.DeliveryNote>().AsQueryable()));

        var appService = new global::MyERP.Sales.SalesPersonAppService(spRepo);
        var lazySp = Substitute.For<Volo.Abp.DependencyInjection.IAbpLazyServiceProvider>();
        lazySp.LazyGetRequiredService<IRepository<global::MyERP.Sales.Entities.SalesTeamEntry, Guid>>().Returns(teamRepo);
        lazySp.LazyGetRequiredService<IRepository<global::MyERP.Sales.Entities.SalesOrder, Guid>>().Returns(soRepo);
        lazySp.LazyGetRequiredService<IRepository<global::MyERP.Sales.Entities.DeliveryNote, Guid>>().Returns(dnRepo);
        lazySp.LazyGetRequiredService<IRepository<global::MyERP.Core.Entities.DocumentActivityLog, Guid>>().Returns(logRepo);
        appService.LazyServiceProvider = lazySp;

        var ex = await Should.ThrowAsync<BusinessException>(() => appService.DisableAsync(spId));
        ex.Code.ShouldBe(MyERPDomainErrorCodes.ValidationFailed);
        (ex.Data["detail"]?.ToString() ?? string.Empty).ShouldContain("SO-UNF-001");
        (ex.Data["detail"]?.ToString() ?? string.Empty).ShouldContain("unfinished");
    }

    [Fact]
    public async Task SalesPerson_DisableAsync_BlocksWhenUnbilledDeliveryNoteExists_PR61835()
    {
        var spRepo = Substitute.For<IRepository<global::MyERP.Sales.Entities.SalesPerson, Guid>>();
        var teamRepo = Substitute.For<IRepository<global::MyERP.Sales.Entities.SalesTeamEntry, Guid>>();
        var soRepo = Substitute.For<IRepository<global::MyERP.Sales.Entities.SalesOrder, Guid>>();
        var dnRepo = Substitute.For<IRepository<global::MyERP.Sales.Entities.DeliveryNote, Guid>>();
        var logRepo = Substitute.For<IRepository<global::MyERP.Core.Entities.DocumentActivityLog, Guid>>();

        var spId = Guid.NewGuid();
        var dnId = Guid.NewGuid();
        var sp = new global::MyERP.Sales.Entities.SalesPerson(spId, "Jane Doe");
        spRepo.GetAsync(spId).Returns(Task.FromResult(sp));

        var teamEntry = new global::MyERP.Sales.Entities.SalesTeamEntry(
            Guid.NewGuid(), spId, "DeliveryNote", dnId, 100m, 2000m, 5m);
        teamRepo.GetQueryableAsync().Returns(Task.FromResult(new List<global::MyERP.Sales.Entities.SalesTeamEntry> { teamEntry }.AsQueryable()));

        var unbilledDn = new global::MyERP.Sales.Entities.DeliveryNote(
            dnId, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "DN-UNB-001", DateTime.UtcNow);
        unbilledDn.AddItem(Guid.NewGuid(), "Delivered Item", 10m, 200m, 0m, "Unit");
        unbilledDn.Submit(); // Status = Submitted, PerBilled = 0% (unbilled)

        soRepo.GetQueryableAsync().Returns(Task.FromResult(new List<global::MyERP.Sales.Entities.SalesOrder>().AsQueryable()));
        dnRepo.GetQueryableAsync().Returns(Task.FromResult(new List<global::MyERP.Sales.Entities.DeliveryNote> { unbilledDn }.AsQueryable()));

        var appService = new global::MyERP.Sales.SalesPersonAppService(spRepo);
        var lazySp = Substitute.For<Volo.Abp.DependencyInjection.IAbpLazyServiceProvider>();
        lazySp.LazyGetRequiredService<IRepository<global::MyERP.Sales.Entities.SalesTeamEntry, Guid>>().Returns(teamRepo);
        lazySp.LazyGetRequiredService<IRepository<global::MyERP.Sales.Entities.SalesOrder, Guid>>().Returns(soRepo);
        lazySp.LazyGetRequiredService<IRepository<global::MyERP.Sales.Entities.DeliveryNote, Guid>>().Returns(dnRepo);
        lazySp.LazyGetRequiredService<IRepository<global::MyERP.Core.Entities.DocumentActivityLog, Guid>>().Returns(logRepo);
        appService.LazyServiceProvider = lazySp;

        var ex = await Should.ThrowAsync<BusinessException>(() => appService.DisableAsync(spId));
        ex.Code.ShouldBe(MyERPDomainErrorCodes.ValidationFailed);
        (ex.Data["detail"]?.ToString() ?? string.Empty).ShouldContain("DN-UNB-001");
        (ex.Data["detail"]?.ToString() ?? string.Empty).ShouldContain("unfinished");
    }

    [Fact]
    public async Task PartyType_UpdateAsync_BlocksAccountTypeChangeWhenLedgerEntriesExist_PR60804()
    {
        var partyTypeRepo = Substitute.For<IRepository<PartyType, Guid>>();
        var jeLineRepo = Substitute.For<IRepository<global::MyERP.Accounting.Entities.JournalEntryLine, Guid>>();
        var pleRepo = Substitute.For<IRepository<global::MyERP.Accounting.Entities.PaymentLedgerEntry, Guid>>();

        var partyTypeId = Guid.NewGuid();
        var partyType = new PartyType(partyTypeId, "Custom Party", PartyAccountType.Receivable);
        partyTypeRepo.GetAsync(partyTypeId).Returns(Task.FromResult(partyType));

        var jeLine = new global::MyERP.Accounting.Entities.JournalEntryLine(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 500m, isDebit: true)
        {
            PartyType = "Custom Party",
            PartyId = Guid.NewGuid()
        };
        jeLineRepo.GetQueryableAsync().Returns(Task.FromResult(new List<global::MyERP.Accounting.Entities.JournalEntryLine> { jeLine }.AsQueryable()));
        pleRepo.GetQueryableAsync().Returns(Task.FromResult(new List<global::MyERP.Accounting.Entities.PaymentLedgerEntry>().AsQueryable()));

        var appService = new PartyTypeAppService(partyTypeRepo);
        var lazySp = Substitute.For<Volo.Abp.DependencyInjection.IAbpLazyServiceProvider>();
        lazySp.LazyGetRequiredService<IRepository<global::MyERP.Accounting.Entities.JournalEntryLine, Guid>>().Returns(jeLineRepo);
        lazySp.LazyGetRequiredService<IRepository<global::MyERP.Accounting.Entities.PaymentLedgerEntry, Guid>>().Returns(pleRepo);
        appService.LazyServiceProvider = lazySp;

        var ex = await Should.ThrowAsync<BusinessException>(() =>
            appService.UpdateAsync(partyTypeId, new CreateUpdatePartyTypeDto
            {
                Name = "Custom Party",
                AccountType = PartyAccountType.Payable // Trying to change Receivable -> Payable
            }));

        ex.Code.ShouldBe(MyERPDomainErrorCodes.ValidationFailed);
        (ex.Data["detail"]?.ToString() ?? string.Empty).ShouldContain("Cannot change Account Type for Custom Party after ledger entries exist");
    }

    [Fact]
    public async Task Supplier_CreateAsync_BlocksGroupTypeSupplierGroup_PR61829()
    {
        var supplierRepo = Substitute.For<IRepository<global::MyERP.Purchasing.Entities.Supplier, Guid>>();
        var groupRepo = Substitute.For<IRepository<SupplierGroup, Guid>>();
        var accountRepo = Substitute.For<IRepository<global::MyERP.Accounting.Entities.Account, Guid>>();

        var groupId = Guid.NewGuid();
        var supplierGroup = new SupplierGroup(groupId, "All Supplier Groups", isGroup: true);

        groupRepo.GetQueryableAsync().Returns(Task.FromResult(new List<SupplierGroup> { supplierGroup }.AsQueryable()));
        supplierRepo.GetQueryableAsync().Returns(Task.FromResult(new List<global::MyERP.Purchasing.Entities.Supplier>().AsQueryable()));
        accountRepo.GetQueryableAsync().Returns(Task.FromResult(new List<global::MyERP.Accounting.Entities.Account>().AsQueryable()));

        var appService = new global::MyERP.Purchasing.SupplierAppService(supplierRepo);
        var lazySp = Substitute.For<Volo.Abp.DependencyInjection.IAbpLazyServiceProvider>();
        lazySp.LazyGetRequiredService<IRepository<SupplierGroup, Guid>>().Returns(groupRepo);
        lazySp.LazyGetRequiredService<IRepository<global::MyERP.Accounting.Entities.Account, Guid>>().Returns(accountRepo);
        appService.LazyServiceProvider = lazySp;

        var ex = await Should.ThrowAsync<BusinessException>(() =>
            appService.CreateAsync(new global::MyERP.Purchasing.CreateUpdateSupplierDto
            {
                Name = "Acme Supplies",
                SupplierGroupId = groupId
            }));

        ex.Code.ShouldBe(MyERPDomainErrorCodes.ValidationFailed);
        (ex.Data["detail"]?.ToString() ?? string.Empty).ShouldContain("Cannot select a Group type Supplier Group");
    }

    [Fact]
    public async Task Supplier_CreateAsync_BlocksSupplierNameMatchingSupplierGroup_PR61829()
    {
        var supplierRepo = Substitute.For<IRepository<global::MyERP.Purchasing.Entities.Supplier, Guid>>();
        var groupRepo = Substitute.For<IRepository<SupplierGroup, Guid>>();
        var accountRepo = Substitute.For<IRepository<global::MyERP.Accounting.Entities.Account, Guid>>();

        var groupId = Guid.NewGuid();
        var supplierGroup = new SupplierGroup(groupId, "Hardware", isGroup: false);

        groupRepo.GetQueryableAsync().Returns(Task.FromResult(new List<SupplierGroup> { supplierGroup }.AsQueryable()));
        supplierRepo.GetQueryableAsync().Returns(Task.FromResult(new List<global::MyERP.Purchasing.Entities.Supplier>().AsQueryable()));
        accountRepo.GetQueryableAsync().Returns(Task.FromResult(new List<global::MyERP.Accounting.Entities.Account>().AsQueryable()));

        var appService = new global::MyERP.Purchasing.SupplierAppService(supplierRepo);
        var lazySp = Substitute.For<Volo.Abp.DependencyInjection.IAbpLazyServiceProvider>();
        lazySp.LazyGetRequiredService<IRepository<SupplierGroup, Guid>>().Returns(groupRepo);
        lazySp.LazyGetRequiredService<IRepository<global::MyERP.Accounting.Entities.Account, Guid>>().Returns(accountRepo);
        appService.LazyServiceProvider = lazySp;

        var ex = await Should.ThrowAsync<BusinessException>(() =>
            appService.CreateAsync(new global::MyERP.Purchasing.CreateUpdateSupplierDto
            {
                Name = "Hardware" // Matches supplier group name
            }));

        ex.Code.ShouldBe(MyERPDomainErrorCodes.SupplierNameCannotMatchSupplierGroup);
    }

    [Fact]
    public async Task Quotation_DeclareLostAsync_RequiresLostReason_PR61828()
    {
        var quotationRepo = Substitute.For<IRepository<global::MyERP.Sales.Entities.Quotation, Guid>>();
        var custRepo = Substitute.For<IRepository<global::MyERP.Sales.Entities.Customer, Guid>>();
        var compRepo = Substitute.For<IRepository<global::MyERP.CRM.Entities.CompetitorDetail, Guid>>();
        var reasonDetailRepo = Substitute.For<IRepository<global::MyERP.Sales.Entities.QuotationLostReasonDetail, Guid>>();
        var reasonRepo = Substitute.For<IRepository<global::MyERP.Sales.Entities.QuotationLostReason, Guid>>();
        var numGen = Substitute.For<IDocumentNumberGenerator>();
        var pricingRuleRepo = Substitute.For<IRepository<global::MyERP.Sales.Entities.PricingRule, Guid>>();
        var itemRepo = Substitute.For<IRepository<global::MyERP.Inventory.Entities.Item, Guid>>();
        var discountCeiling = new DiscountCeilingValidationService(itemRepo);
        var pricingRule = new PricingRuleApplicationService(pricingRuleRepo, discountCeiling);

        var quotationId = Guid.NewGuid();
        var qtn = new global::MyERP.Sales.Entities.Quotation(
            quotationId, Guid.NewGuid(), Guid.NewGuid(), "QTN-001", DateTime.UtcNow);
        quotationRepo.GetAsync(quotationId, includeDetails: true).Returns(Task.FromResult(qtn));

        var appService = new global::MyERP.Sales.QuotationAppService(
            quotationRepo, custRepo, compRepo, reasonDetailRepo, reasonRepo, numGen, pricingRule);

        // Calling without lost reasons should throw ValidationFailed
        var ex = await Should.ThrowAsync<BusinessException>(() =>
            appService.MarkLostAsync(quotationId, new global::MyERP.Sales.MarkQuotationLostDto
            {
                LostReasonIds = new List<Guid>() // Empty reasons
            }));

        ex.Code.ShouldBe(MyERPDomainErrorCodes.ValidationFailed);
        (ex.Data["detail"]?.ToString() ?? string.Empty).ShouldContain("Please select at least one Lost Reason");
    }

    [Fact]
    public async Task ProductBundle_Decompose_RepeatedComponent_ProducesSeparateRows_PR62149()
    {
        var bundleRepo = Substitute.For<IRepository<global::MyERP.Sales.Entities.ProductBundle, Guid>>();
        var parentItemId = Guid.NewGuid();
        var componentItemId = Guid.NewGuid();

        var bundle = new global::MyERP.Sales.Entities.ProductBundle(Guid.NewGuid(), parentItemId);
        bundle.AddItem(componentItemId, 2m, "Component A", "Unit"); // First occurrence: 2
        bundle.AddItem(componentItemId, 3m, "Component A", "Unit"); // Repeated component: 3

        bundleRepo.GetQueryableAsync().Returns(Task.FromResult(new List<global::MyERP.Sales.Entities.ProductBundle> { bundle }.AsQueryable()));

        var service = new ProductBundleDecompositionService(bundleRepo);
        var result = await service.DecomposeAsync(parentItemId, transactionQty: 2m, parentRate: 100m);

        result.Count.ShouldBe(2);
        result[0].ComponentItemId.ShouldBe(componentItemId);
        result[0].Qty.ShouldBe(4m); // 2 * 2
        result[0].Rate.ShouldBe(40m); // 100 * (2 / 5)

        result[1].ComponentItemId.ShouldBe(componentItemId);
        result[1].Qty.ShouldBe(6m); // 3 * 2
        result[1].Rate.ShouldBe(60m); // 100 * (3 / 5)

        result.Sum(r => r.Qty).ShouldBe(10m);
        result.Sum(r => r.Rate).ShouldBe(100m);
    }

    [Fact]
    public async Task ProductBundle_Decompose_DisabledBundleAllowedOnReturn_PR62149()
    {
        var bundleRepo = Substitute.For<IRepository<global::MyERP.Sales.Entities.ProductBundle, Guid>>();
        var parentItemId = Guid.NewGuid();
        var componentItemId = Guid.NewGuid();

        var bundle = new global::MyERP.Sales.Entities.ProductBundle(Guid.NewGuid(), parentItemId);
        bundle.AddItem(componentItemId, 1m, "Component A", "Unit");
        bundle.Disable(); // Disabled!

        bundleRepo.GetQueryableAsync().Returns(Task.FromResult(new List<global::MyERP.Sales.Entities.ProductBundle> { bundle }.AsQueryable()));

        var service = new ProductBundleDecompositionService(bundleRepo);

        // Standard transaction: disabled bundle returns empty
        var standard = await service.DecomposeAsync(parentItemId, transactionQty: 1m, parentRate: 50m, isReturn: false);
        standard.ShouldBeEmpty();

        // Return transaction: disabled bundle is permitted
        var returned = await service.DecomposeAsync(parentItemId, transactionQty: -1m, parentRate: 50m, isReturn: true);
        returned.Count.ShouldBe(1);
        returned[0].ComponentItemId.ShouldBe(componentItemId);
        returned[0].Qty.ShouldBe(-1m);
    }
}
