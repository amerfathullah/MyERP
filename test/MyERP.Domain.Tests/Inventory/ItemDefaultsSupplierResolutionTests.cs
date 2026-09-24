using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Inventory;
using MyERP.Inventory.DomainServices;
using MyERP.Inventory.Entities;
using NSubstitute;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace MyERP.InventoryTests;

public class ItemDefaultsSupplierResolutionTests
{
    private readonly IRepository<Item, Guid> _itemRepo = Substitute.For<IRepository<Item, Guid>>();
    private readonly IRepository<ItemGroup, Guid> _itemGroupRepo = Substitute.For<IRepository<ItemGroup, Guid>>();
    private readonly IRepository<ItemDefault, Guid> _itemDefaultRepo = Substitute.For<IRepository<ItemDefault, Guid>>();

    [Fact]
    public async Task ResolveDefaultSupplier_PrefersCompanyItemDefault()
    {
        var itemId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        var expectedSupplier = Guid.NewGuid();

        var itemDefault = new ItemDefault(Guid.NewGuid(), itemId, companyId)
        {
            DefaultSupplierId = expectedSupplier
        };

        var queryable = new List<ItemDefault> { itemDefault }.AsQueryable();
        _itemDefaultRepo.GetQueryableAsync().Returns(Task.FromResult(queryable));

        var service = new ItemDefaultsResolutionService(_itemRepo, _itemGroupRepo, _itemDefaultRepo);
        var result = await service.ResolveDefaultSupplierAsync(itemId, companyId);

        result.ShouldBe(expectedSupplier);
    }

    [Fact]
    public async Task ResolveDefaultSupplier_FallsBackToItemSupplier_WhenNoItemDefault()
    {
        var itemId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        var expectedSupplier = Guid.NewGuid();

        var queryable = new List<ItemDefault>().AsQueryable();
        _itemDefaultRepo.GetQueryableAsync().Returns(Task.FromResult(queryable));

        var item = new Item(itemId, Guid.NewGuid(), "ITEM-01", "Widget", ItemType.Goods);
        item.Suppliers.Add(new ItemSupplier(Guid.NewGuid(), itemId, expectedSupplier));
        _itemRepo.FindAsync(itemId).Returns(Task.FromResult<Item?>(item));

        var service = new ItemDefaultsResolutionService(_itemRepo, _itemGroupRepo, _itemDefaultRepo);
        var result = await service.ResolveDefaultSupplierAsync(itemId, companyId);

        result.ShouldBe(expectedSupplier);
    }

    [Fact]
    public async Task ResolveDefaultSupplier_FallsBackToItemGroupHierarchy_PerPR59349()
    {
        var itemId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        var parentGroupId = Guid.NewGuid();
        var childGroupId = Guid.NewGuid();
        var expectedSupplier = Guid.NewGuid();

        var queryable = new List<ItemDefault>().AsQueryable();
        _itemDefaultRepo.GetQueryableAsync().Returns(Task.FromResult(queryable));

        var item = new Item(itemId, Guid.NewGuid(), "ITEM-02", "Gear", ItemType.Goods)
        {
            ItemGroupId = childGroupId
        };
        _itemRepo.FindAsync(itemId).Returns(Task.FromResult<Item?>(item));

        var childGroup = new ItemGroup(childGroupId, "Sub-gears") { ParentId = parentGroupId };
        var parentGroup = new ItemGroup(parentGroupId, "Gears") { DefaultSupplierId = expectedSupplier };

        _itemGroupRepo.FindAsync(childGroupId).Returns(Task.FromResult<ItemGroup?>(childGroup));
        _itemGroupRepo.FindAsync(parentGroupId).Returns(Task.FromResult<ItemGroup?>(parentGroup));

        var service = new ItemDefaultsResolutionService(_itemRepo, _itemGroupRepo, _itemDefaultRepo);
        var result = await service.ResolveDefaultSupplierAsync(itemId, companyId);

        result.ShouldBe(expectedSupplier);
    }
}
