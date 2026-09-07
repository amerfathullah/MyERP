using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Core;
using MyERP.Core.DomainServices;
using MyERP.Inventory;
using MyERP.Inventory.DomainServices;
using MyERP.Inventory.Entities;
using MyERP.Manufacturing.Entities;
using NSubstitute;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace MyERP.Domain.Tests.Inventory;

public class StockEntryFgConversionValidationTests
{
    private readonly IRepository<Warehouse, Guid> _warehouseRepo = Substitute.For<IRepository<Warehouse, Guid>>();
    private readonly IRepository<Item, Guid> _itemRepo = Substitute.For<IRepository<Item, Guid>>();
    private readonly IRepository<WorkOrder, Guid> _woRepo = Substitute.For<IRepository<WorkOrder, Guid>>();
    private readonly IRepository<ItemAlternative, Guid> _altRepo = Substitute.For<IRepository<ItemAlternative, Guid>>();
    private readonly IRepository<StockEntry, Guid> _seRepo = Substitute.For<IRepository<StockEntry, Guid>>();

    private StockEntryManager CreateManager()
    {
        var restrictionEntryRepo = Substitute.For<IRepository<MyERP.Core.Entities.CompanyRestrictionEntry, Guid>>();
        var custRepo = Substitute.For<IRepository<MyERP.Sales.Entities.Customer, Guid>>();
        var suppRepo = Substitute.For<IRepository<MyERP.Purchasing.Entities.Supplier, Guid>>();
        var accRepo = Substitute.For<IRepository<MyERP.Accounting.Entities.Account, Guid>>();
        var companyRestriction = new CompanyRestrictionValidationService(
            restrictionEntryRepo, _itemRepo, custRepo, suppRepo, accRepo, _warehouseRepo);
        return new StockEntryManager(_warehouseRepo, _itemRepo, companyRestriction);
    }

    [Fact]
    public async Task ValidateFgConversionAsync_Throws_WhenEntryTypeNotRepack()
    {
        var manager = CreateManager();
        var entry = new StockEntry(Guid.NewGuid(), Guid.NewGuid(), StockEntryType.MaterialTransfer, DateTime.UtcNow)
        {
            IsFgConversion = true,
            WorkOrderId = Guid.NewGuid()
        };

        var ex = await Should.ThrowAsync<BusinessException>(() =>
            manager.ValidateFgConversionAsync(entry, _woRepo, _altRepo, _seRepo));
        ex.Data["detail"]!.ToString()!.ShouldContain("Repack");
    }

    [Fact]
    public async Task ValidateFgConversionAsync_Throws_WhenWorkOrderMissing()
    {
        var manager = CreateManager();
        var entry = new StockEntry(Guid.NewGuid(), Guid.NewGuid(), StockEntryType.Repack, DateTime.UtcNow)
        {
            IsFgConversion = true,
            WorkOrderId = null
        };

        var ex = await Should.ThrowAsync<BusinessException>(() =>
            manager.ValidateFgConversionAsync(entry, _woRepo, _altRepo, _seRepo));
        ex.Data["detail"]!.ToString()!.ShouldContain("Work Order is mandatory");
    }

    [Fact]
    public async Task ValidateFgConversionAsync_Throws_WhenNoItemAlternativesFound()
    {
        var manager = CreateManager();
        var companyId = Guid.NewGuid();
        var prodItemId = Guid.NewGuid();
        var woId = Guid.NewGuid();

        var wo = new WorkOrder(woId, companyId, "WO-001", prodItemId, Guid.NewGuid(), 10m)
        {
            ProducedQuantity = 10m
        };
        _woRepo.FindAsync(woId).Returns(wo);

        _altRepo.GetQueryableAsync().Returns(Task.FromResult(new List<ItemAlternative>().AsQueryable()));

        var entry = new StockEntry(Guid.NewGuid(), companyId, StockEntryType.Repack, DateTime.UtcNow)
        {
            IsFgConversion = true,
            WorkOrderId = woId
        };
        entry.AddItem(prodItemId, 5m, Guid.NewGuid(), null);

        var ex = await Should.ThrowAsync<BusinessException>(() =>
            manager.ValidateFgConversionAsync(entry, _woRepo, _altRepo, _seRepo));
        ex.Data["detail"]!.ToString()!.ShouldContain("No Item Alternative records found");
    }

    [Fact]
    public async Task ValidateFgConversionAsync_Throws_WhenOutputItemIsNotValidAlternative()
    {
        var manager = CreateManager();
        var companyId = Guid.NewGuid();
        var prodItemId = Guid.NewGuid();
        var validAltId = Guid.NewGuid();
        var invalidAltId = Guid.NewGuid();
        var woId = Guid.NewGuid();

        var wo = new WorkOrder(woId, companyId, "WO-001", prodItemId, Guid.NewGuid(), 10m)
        {
            ProducedQuantity = 10m
        };
        _woRepo.FindAsync(woId).Returns(wo);

        var alternatives = new List<ItemAlternative>
        {
            new(Guid.NewGuid(), companyId, prodItemId, validAltId)
        };
        _altRepo.GetQueryableAsync().Returns(Task.FromResult(alternatives.AsQueryable()));

        var entry = new StockEntry(Guid.NewGuid(), companyId, StockEntryType.Repack, DateTime.UtcNow)
        {
            IsFgConversion = true,
            WorkOrderId = woId
        };
        entry.AddItem(prodItemId, 5m, Guid.NewGuid(), null);
        entry.AddItem(invalidAltId, 5m, null, Guid.NewGuid(), isFinishedItem: true);

        var ex = await Should.ThrowAsync<BusinessException>(() =>
            manager.ValidateFgConversionAsync(entry, _woRepo, _altRepo, _seRepo));
        ex.Data["detail"]!.ToString()!.ShouldContain("not an alternative item");
    }

    [Fact]
    public async Task ValidateFgConversionAsync_Throws_WhenOutputQtyDoesNotMatchConsumedQty()
    {
        var manager = CreateManager();
        var companyId = Guid.NewGuid();
        var prodItemId = Guid.NewGuid();
        var validAltId = Guid.NewGuid();
        var woId = Guid.NewGuid();

        var wo = new WorkOrder(woId, companyId, "WO-001", prodItemId, Guid.NewGuid(), 10m)
        {
            ProducedQuantity = 10m
        };
        _woRepo.FindAsync(woId).Returns(wo);

        var alternatives = new List<ItemAlternative>
        {
            new(Guid.NewGuid(), companyId, prodItemId, validAltId)
        };
        _altRepo.GetQueryableAsync().Returns(Task.FromResult(alternatives.AsQueryable()));

        var entry = new StockEntry(Guid.NewGuid(), companyId, StockEntryType.Repack, DateTime.UtcNow)
        {
            IsFgConversion = true,
            WorkOrderId = woId
        };
        entry.AddItem(prodItemId, 5m, Guid.NewGuid(), null);
        entry.AddItem(validAltId, 4m, null, Guid.NewGuid(), isFinishedItem: true); // 4 != 5

        var ex = await Should.ThrowAsync<BusinessException>(() =>
            manager.ValidateFgConversionAsync(entry, _woRepo, _altRepo, _seRepo));
        ex.Data["detail"]!.ToString()!.ShouldContain("must equal converted quantity");
    }

    [Fact]
    public async Task ValidateFgConversionAsync_Throws_WhenConsumedQtyExceedsAvailableProducedQty()
    {
        var manager = CreateManager();
        var companyId = Guid.NewGuid();
        var prodItemId = Guid.NewGuid();
        var validAltId = Guid.NewGuid();
        var woId = Guid.NewGuid();

        var wo = new WorkOrder(woId, companyId, "WO-001", prodItemId, Guid.NewGuid(), 10m)
        {
            ProducedQuantity = 3m // only 3 produced
        };
        _woRepo.FindAsync(woId).Returns(wo);

        var alternatives = new List<ItemAlternative>
        {
            new(Guid.NewGuid(), companyId, prodItemId, validAltId)
        };
        _altRepo.GetQueryableAsync().Returns(Task.FromResult(alternatives.AsQueryable()));
        _seRepo.GetQueryableAsync().Returns(Task.FromResult(new List<StockEntry>().AsQueryable()));

        var entry = new StockEntry(Guid.NewGuid(), companyId, StockEntryType.Repack, DateTime.UtcNow)
        {
            IsFgConversion = true,
            WorkOrderId = woId
        };
        entry.AddItem(prodItemId, 5m, Guid.NewGuid(), null); // 5 > 3
        entry.AddItem(validAltId, 5m, null, Guid.NewGuid(), isFinishedItem: true);

        var ex = await Should.ThrowAsync<BusinessException>(() =>
            manager.ValidateFgConversionAsync(entry, _woRepo, _altRepo, _seRepo));
        ex.Data["detail"]!.ToString()!.ShouldContain("cannot exceed available produced quantity");
    }

    [Fact]
    public async Task ValidateFgConversionAsync_Succeeds_WhenAllRulesSatisfied()
    {
        var manager = CreateManager();
        var companyId = Guid.NewGuid();
        var prodItemId = Guid.NewGuid();
        var validAltId = Guid.NewGuid();
        var woId = Guid.NewGuid();

        var wo = new WorkOrder(woId, companyId, "WO-001", prodItemId, Guid.NewGuid(), 10m)
        {
            ProducedQuantity = 10m
        };
        _woRepo.FindAsync(woId).Returns(wo);

        var alternatives = new List<ItemAlternative>
        {
            new(Guid.NewGuid(), companyId, prodItemId, validAltId)
        };
        _altRepo.GetQueryableAsync().Returns(Task.FromResult(alternatives.AsQueryable()));
        _seRepo.GetQueryableAsync().Returns(Task.FromResult(new List<StockEntry>().AsQueryable()));

        var entry = new StockEntry(Guid.NewGuid(), companyId, StockEntryType.Repack, DateTime.UtcNow)
        {
            IsFgConversion = true,
            WorkOrderId = woId
        };
        entry.AddItem(prodItemId, 5m, Guid.NewGuid(), null);
        entry.AddItem(validAltId, 5m, null, Guid.NewGuid(), isFinishedItem: true);

        await manager.ValidateFgConversionAsync(entry, _woRepo, _altRepo, _seRepo);
    }

    [Fact]
    public async Task ValidateFgConversionAsync_Throws_WhenAllowAlternativeFinishedGoodsDisabled()
    {
        var manager = CreateManager();
        var companyId = Guid.NewGuid();
        var woId = Guid.NewGuid();
        var mfgSettingsRepo = Substitute.For<IRepository<ManufacturingSettings, Guid>>();
        var settings = new ManufacturingSettings(Guid.NewGuid(), companyId)
        {
            AllowAlternativeFinishedGoods = false
        };
        mfgSettingsRepo.FindAsync(Arg.Any<System.Linq.Expressions.Expression<Func<ManufacturingSettings, bool>>>())
            .Returns(settings);

        var entry = new StockEntry(Guid.NewGuid(), companyId, StockEntryType.Repack, DateTime.UtcNow)
        {
            IsFgConversion = true,
            WorkOrderId = woId
        };

        var ex = await Should.ThrowAsync<BusinessException>(() =>
            manager.ValidateFgConversionAsync(entry, _woRepo, _altRepo, _seRepo, mfgSettingsRepo));
        ex.Data["detail"]!.ToString()!.ShouldContain("Allow Alternative Finished Goods");
    }

    [Fact]
    public async Task ValidateFgConversionAsync_Succeeds_WhenAllowAlternativeFinishedGoodsEnabled()
    {
        var manager = CreateManager();
        var companyId = Guid.NewGuid();
        var prodItemId = Guid.NewGuid();
        var validAltId = Guid.NewGuid();
        var woId = Guid.NewGuid();

        var wo = new WorkOrder(woId, companyId, "WO-001", prodItemId, Guid.NewGuid(), 10m)
        {
            ProducedQuantity = 10m
        };
        _woRepo.FindAsync(woId).Returns(wo);

        var alternatives = new List<ItemAlternative>
        {
            new(Guid.NewGuid(), companyId, prodItemId, validAltId)
        };
        _altRepo.GetQueryableAsync().Returns(Task.FromResult(alternatives.AsQueryable()));
        _seRepo.GetQueryableAsync().Returns(Task.FromResult(new List<StockEntry>().AsQueryable()));

        var mfgSettingsRepo = Substitute.For<IRepository<ManufacturingSettings, Guid>>();
        var settings = new ManufacturingSettings(Guid.NewGuid(), companyId)
        {
            AllowAlternativeFinishedGoods = true
        };
        mfgSettingsRepo.FindAsync(Arg.Any<System.Linq.Expressions.Expression<Func<ManufacturingSettings, bool>>>())
            .Returns(settings);

        var entry = new StockEntry(Guid.NewGuid(), companyId, StockEntryType.Repack, DateTime.UtcNow)
        {
            IsFgConversion = true,
            WorkOrderId = woId
        };
        entry.AddItem(prodItemId, 5m, Guid.NewGuid(), null);
        entry.AddItem(validAltId, 5m, null, Guid.NewGuid(), isFinishedItem: true);

        await manager.ValidateFgConversionAsync(entry, _woRepo, _altRepo, _seRepo, mfgSettingsRepo);
    }
}