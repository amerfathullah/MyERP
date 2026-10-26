using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using MyERP.Inventory;
using MyERP.Inventory.Entities;
using MyERP.Sales.DomainServices;
using MyERP.Sales.Entities;
using NSubstitute;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace MyERP.Domain.Tests.Sales;

public class SalesTeamServiceTests
{
    private readonly IRepository<SalesPerson, Guid> _salesPersonRepo = Substitute.For<IRepository<SalesPerson, Guid>>();
    private readonly IRepository<SalesTeamEntry, Guid> _salesTeamRepo = Substitute.For<IRepository<SalesTeamEntry, Guid>>();
    private readonly IRepository<Item, Guid> _itemRepo = Substitute.For<IRepository<Item, Guid>>();

    private SalesTeamService CreateService()
    {
        return new SalesTeamService(_salesPersonRepo, _salesTeamRepo, _itemRepo);
    }

    [Fact]
    public async Task SetSalesTeamAsync_DuplicateSalesPerson_ThrowsValidationFailed()
    {
        var service = CreateService();
        var spId = Guid.NewGuid();
        var rows = new List<SalesTeamAllocationInput>
        {
            new() { SalesPersonId = spId, AllocatedPercentage = 50m },
            new() { SalesPersonId = spId, AllocatedPercentage = 50m }
        };

        var ex = await Should.ThrowAsync<BusinessException>(() =>
            service.SetSalesTeamAsync("SalesOrder", Guid.NewGuid(), 1000m, rows));

        ex.Code.ShouldBe(MyERPDomainErrorCodes.ValidationFailed);
    }

    [Theory]
    [InlineData(-10)]
    [InlineData(110)]
    public async Task SetSalesTeamAsync_AllocatedPercentageOutOfRange_ThrowsValidationFailed(decimal percent)
    {
        var service = CreateService();
        var spId = Guid.NewGuid();
        var rows = new List<SalesTeamAllocationInput>
        {
            new() { SalesPersonId = spId, AllocatedPercentage = percent }
        };

        var ex = await Should.ThrowAsync<BusinessException>(() =>
            service.SetSalesTeamAsync("SalesOrder", Guid.NewGuid(), 1000m, rows));

        ex.Code.ShouldBe(MyERPDomainErrorCodes.ValidationFailed);
    }

    [Fact]
    public async Task SetSalesTeamAsync_TotalPercentageNot100_Throws()
    {
        var service = CreateService();
        var sp1 = Guid.NewGuid();
        var sp2 = Guid.NewGuid();
        var rows = new List<SalesTeamAllocationInput>
        {
            new() { SalesPersonId = sp1, AllocatedPercentage = 40m },
            new() { SalesPersonId = sp2, AllocatedPercentage = 50m } // Sum = 90%
        };

        await Should.ThrowAsync<BusinessException>(() =>
            service.SetSalesTeamAsync("SalesOrder", Guid.NewGuid(), 1000m, rows));
    }

    [Fact]
    public async Task SetSalesTeamAsync_GroupSalesPerson_ThrowsValidationFailed()
    {
        var service = CreateService();
        var spId = Guid.NewGuid();
        var groupSp = new SalesPerson(spId, "Sales Team Group") { IsGroup = true };

        _salesPersonRepo.GetListAsync(Arg.Any<Expression<Func<SalesPerson, bool>>>())
            .Returns(Task.FromResult(new List<SalesPerson> { groupSp }));

        var rows = new List<SalesTeamAllocationInput>
        {
            new() { SalesPersonId = spId, AllocatedPercentage = 100m }
        };

        var ex = await Should.ThrowAsync<BusinessException>(() =>
            service.SetSalesTeamAsync("SalesOrder", Guid.NewGuid(), 1000m, rows));

        ex.Code.ShouldBe(MyERPDomainErrorCodes.ValidationFailed);
    }

    [Fact]
    public async Task SetSalesTeamAsync_DisabledSalesPerson_ThrowsValidationFailed()
    {
        var service = CreateService();
        var spId = Guid.NewGuid();
        var disabledSp = new SalesPerson(spId, "Former Sales Person");
        disabledSp.Disable();

        _salesPersonRepo.GetListAsync(Arg.Any<Expression<Func<SalesPerson, bool>>>())
            .Returns(Task.FromResult(new List<SalesPerson> { disabledSp }));

        var rows = new List<SalesTeamAllocationInput>
        {
            new() { SalesPersonId = spId, AllocatedPercentage = 100m }
        };

        var ex = await Should.ThrowAsync<BusinessException>(() =>
            service.SetSalesTeamAsync("SalesOrder", Guid.NewGuid(), 1000m, rows));

        ex.Code.ShouldBe(MyERPDomainErrorCodes.ValidationFailed);
    }

    [Fact]
    public async Task SetSalesTeamAsync_PreservesExistingMemberSavedRate_WhenNoExplicitRateProvided()
    {
        var service = CreateService();
        var docId = Guid.NewGuid();
        var spId = Guid.NewGuid();

        // Master rate is 10%, but saved snapshot rate on document is 5%
        var sp = new SalesPerson(spId, "Active Rep");
        sp.SetCommissionRate(10m);
        _salesPersonRepo.GetListAsync(Arg.Any<Expression<Func<SalesPerson, bool>>>())
            .Returns(Task.FromResult(new List<SalesPerson> { sp }));

        var existingEntry = new SalesTeamEntry(Guid.NewGuid(), spId, "SalesInvoice", docId, 100m, 500m, commissionRate: 5m);
        _salesTeamRepo.GetListAsync(Arg.Any<Expression<Func<SalesTeamEntry, bool>>>())
            .Returns(Task.FromResult(new List<SalesTeamEntry> { existingEntry }));

        var rows = new List<SalesTeamAllocationInput>
        {
            new() { SalesPersonId = spId, AllocatedPercentage = 100m, CommissionRate = null }
        };

        var result = await service.SetSalesTeamAsync("SalesInvoice", docId, 1000m, rows);

        result.ShouldHaveSingleItem();
        result[0].CommissionRate.ShouldBe(5m); // Preserved snapshot rate, NOT master rate 10%
        result[0].AllocatedAmount.ShouldBe(1000m);
        result[0].Incentives.ShouldBe(50m);
    }

    [Fact]
    public async Task SetSalesTeamAsync_InheritsReturnSourceRate_ForReturnDocument()
    {
        var service = CreateService();
        var returnDocId = Guid.NewGuid();
        var originalDocId = Guid.NewGuid();
        var spId = Guid.NewGuid();

        var sp = new SalesPerson(spId, "Active Rep");
        sp.SetCommissionRate(15m); // Master changed to 15%
        _salesPersonRepo.GetListAsync(Arg.Any<Expression<Func<SalesPerson, bool>>>())
            .Returns(Task.FromResult(new List<SalesPerson> { sp }));

        // Original document had rate 8%
        var origEntry = new SalesTeamEntry(Guid.NewGuid(), spId, "SalesInvoice", originalDocId, 100m, 1000m, commissionRate: 8m);
        _salesTeamRepo.GetListAsync(Arg.Any<Expression<Func<SalesTeamEntry, bool>>>())
            .Returns(callInfo =>
            {
                var predicate = callInfo.Arg<Expression<Func<SalesTeamEntry, bool>>>().Compile();
                var list = new List<SalesTeamEntry> { origEntry };
                return Task.FromResult(list.Where(predicate).ToList());
            });

        var rows = new List<SalesTeamAllocationInput>
        {
            new() { SalesPersonId = spId, AllocatedPercentage = 100m, CommissionRate = null }
        };

        var result = await service.SetSalesTeamAsync("SalesInvoice", returnDocId, -500m, rows, isReturn: true, returnAgainstId: originalDocId);

        result.ShouldHaveSingleItem();
        result[0].CommissionRate.ShouldBe(8m); // Inherited from return against document
        result[0].AllocatedAmount.ShouldBe(-500m);
        result[0].Incentives.ShouldBe(-40m);
    }

    [Fact]
    public async Task CalculateEligibleAmountAsync_FiltersOutItemsWithoutGrantCommission_AndAppliesExchangeRate()
    {
        var service = CreateService();
        var item1 = new Item(Guid.NewGuid(), Guid.NewGuid(), "ITM-1", "Standard Item", ItemType.Goods) { GrantCommission = true };
        var item2 = new Item(Guid.NewGuid(), Guid.NewGuid(), "ITM-2", "Exempt Item", ItemType.Goods) { GrantCommission = false };

        _itemRepo.GetListAsync(Arg.Any<Expression<Func<Item, bool>>>())
            .Returns(Task.FromResult(new List<Item> { item1, item2 }));

        var items = new List<(Guid ItemId, decimal LineTotal)>
        {
            (item1.Id, 100m),
            (item2.Id, 250m) // Should be excluded from commission eligibility
        };

        // Exchange rate = 4.5
        var eligible = await service.CalculateEligibleAmountAsync(items, exchangeRate: 4.5m);

        // 100 * 4.5 = 450
        eligible.ShouldBe(450m);
    }

    [Fact]
    public async Task CopySalesTeamFromSourceAsync_CopiesAllEntries_WithRecalculatedEligibleAmount()
    {
        var service = CreateService();
        var sourceId = Guid.NewGuid();
        var targetId = Guid.NewGuid();
        var sp1 = Guid.NewGuid();
        var sp2 = Guid.NewGuid();

        var spEntity1 = new SalesPerson(sp1, "Rep 1");
        var spEntity2 = new SalesPerson(sp2, "Rep 2");
        _salesPersonRepo.GetListAsync(Arg.Any<Expression<Func<SalesPerson, bool>>>())
            .Returns(Task.FromResult(new List<SalesPerson> { spEntity1, spEntity2 }));

        var sourceEntries = new List<SalesTeamEntry>
        {
            new(Guid.NewGuid(), sp1, "SalesOrder", sourceId, 60m, 500m, 5m),
            new(Guid.NewGuid(), sp2, "SalesOrder", sourceId, 40m, 500m, 10m)
        };

        _salesTeamRepo.GetListAsync(Arg.Any<Expression<Func<SalesTeamEntry, bool>>>())
            .Returns(callInfo =>
            {
                var predicate = callInfo.Arg<Expression<Func<SalesTeamEntry, bool>>>().Compile();
                return Task.FromResult(sourceEntries.Where(predicate).ToList());
            });

        var copied = await service.CopySalesTeamFromSourceAsync("SalesOrder", sourceId, "DeliveryNote", targetId, 2000m);

        copied.Count.ShouldBe(2);
        copied[0].SalesPersonId.ShouldBe(sp1);
        copied[0].AllocatedPercentage.ShouldBe(60m);
        copied[0].AllocatedAmount.ShouldBe(1200m);
        copied[0].CommissionRate.ShouldBe(5m);
        copied[0].Incentives.ShouldBe(60m);

        copied[1].SalesPersonId.ShouldBe(sp2);
        copied[1].AllocatedPercentage.ShouldBe(40m);
        copied[1].AllocatedAmount.ShouldBe(800m);
        copied[1].CommissionRate.ShouldBe(10m);
        copied[1].Incentives.ShouldBe(80m);
    }
}
