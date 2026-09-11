using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Inventory;
using MyERP.Inventory.Entities;
using MyERP.Manufacturing.DomainServices;
using MyERP.Manufacturing.Entities;
using NSubstitute;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace MyERP.Manufacturing;

public class BomDisabledItemValidationTests
{
    private readonly IRepository<BillOfMaterials, Guid> _bomRepository;
    private readonly IRepository<Item, Guid> _itemRepository;
    private readonly BomValidationService _service;

    public BomDisabledItemValidationTests()
    {
        _bomRepository = Substitute.For<IRepository<BillOfMaterials, Guid>>();
        _itemRepository = Substitute.For<IRepository<Item, Guid>>();
        _service = new BomValidationService(_bomRepository, _itemRepository);
    }

    [Fact]
    public async Task ValidateNoDisabledItems_ActiveItems_PassesValidation()
    {
        var fgItemId = Guid.NewGuid();
        var rawMatId = Guid.NewGuid();
        var bom = new BillOfMaterials(Guid.NewGuid(), Guid.NewGuid(), "BOM-001", fgItemId);
        bom.AddItem(new BomItem(Guid.NewGuid(), bom.Id, rawMatId, "Raw Material", 2m, 10m));

        var fgItem = new Item(fgItemId, bom.CompanyId, "FG-001", "Finished Good", ItemType.Goods) { IsActive = true };
        var rmItem = new Item(rawMatId, bom.CompanyId, "RM-001", "Raw Material", ItemType.Goods) { IsActive = true };

        var items = new List<Item> { fgItem, rmItem }.AsQueryable();
        _itemRepository.GetQueryableAsync().Returns(Task.FromResult(items));

        await _service.ValidateNoDisabledItemsAsync(bom);
    }

    [Fact]
    public async Task ValidateNoDisabledItems_DisabledFgItem_ThrowsValidationFailed()
    {
        var fgItemId = Guid.NewGuid();
        var rawMatId = Guid.NewGuid();
        var bom = new BillOfMaterials(Guid.NewGuid(), Guid.NewGuid(), "BOM-001", fgItemId);
        bom.AddItem(new BomItem(Guid.NewGuid(), bom.Id, rawMatId, "Raw Material", 2m, 10m));

        var fgItem = new Item(fgItemId, bom.CompanyId, "FG-001", "Finished Good", ItemType.Goods) { IsActive = false };
        var rmItem = new Item(rawMatId, bom.CompanyId, "RM-001", "Raw Material", ItemType.Goods) { IsActive = true };

        var items = new List<Item> { fgItem, rmItem }.AsQueryable();
        _itemRepository.GetQueryableAsync().Returns(Task.FromResult(items));

        var ex = await Should.ThrowAsync<BusinessException>(async () =>
            await _service.ValidateNoDisabledItemsAsync(bom));

        ex.Code.ShouldBe(MyERPDomainErrorCodes.ValidationFailed);
        (ex.Data["detail"]?.ToString() ?? string.Empty).ShouldContain("Disabled Item");
        (ex.Data["detail"]?.ToString() ?? string.Empty).ShouldContain("Finished Good");
    }

    [Fact]
    public async Task ValidateNoDisabledItems_DisabledRawMaterial_ThrowsValidationFailed()
    {
        var fgItemId = Guid.NewGuid();
        var rawMatId = Guid.NewGuid();
        var bom = new BillOfMaterials(Guid.NewGuid(), Guid.NewGuid(), "BOM-001", fgItemId);
        bom.AddItem(new BomItem(Guid.NewGuid(), bom.Id, rawMatId, "Raw Material", 2m, 10m));

        var fgItem = new Item(fgItemId, bom.CompanyId, "FG-001", "Finished Good", ItemType.Goods) { IsActive = true };
        var rmItem = new Item(rawMatId, bom.CompanyId, "RM-001", "Raw Material", ItemType.Goods) { IsActive = false };

        var items = new List<Item> { fgItem, rmItem }.AsQueryable();
        _itemRepository.GetQueryableAsync().Returns(Task.FromResult(items));

        var ex = await Should.ThrowAsync<BusinessException>(async () =>
            await _service.ValidateNoDisabledItemsAsync(bom));

        ex.Code.ShouldBe(MyERPDomainErrorCodes.ValidationFailed);
        (ex.Data["detail"]?.ToString() ?? string.Empty).ShouldContain("Disabled Item");
        (ex.Data["detail"]?.ToString() ?? string.Empty).ShouldContain("Raw Material");
    }

    [Fact]
    public async Task ValidateNoDisabledItems_DisabledSecondaryItem_ThrowsValidationFailed()
    {
        var fgItemId = Guid.NewGuid();
        var rawMatId = Guid.NewGuid();
        var scrapId = Guid.NewGuid();
        var bom = new BillOfMaterials(Guid.NewGuid(), Guid.NewGuid(), "BOM-001", fgItemId);
        bom.AddItem(new BomItem(Guid.NewGuid(), bom.Id, rawMatId, "Raw Material", 2m, 10m));
        bom.AddSecondaryItem(new BomSecondaryItem(Guid.NewGuid(), bom.Id, scrapId, SecondaryItemType.Scrap, 1m));

        var fgItem = new Item(fgItemId, bom.CompanyId, "FG-001", "Finished Good", ItemType.Goods) { IsActive = true };
        var rmItem = new Item(rawMatId, bom.CompanyId, "RM-001", "Raw Material", ItemType.Goods) { IsActive = true };
        var scrapItem = new Item(scrapId, bom.CompanyId, "SCRAP-001", "Scrap Material", ItemType.Goods) { IsActive = false };

        var items = new List<Item> { fgItem, rmItem, scrapItem }.AsQueryable();
        _itemRepository.GetQueryableAsync().Returns(Task.FromResult(items));

        var ex = await Should.ThrowAsync<BusinessException>(async () =>
            await _service.ValidateNoDisabledItemsAsync(bom));

        ex.Code.ShouldBe(MyERPDomainErrorCodes.ValidationFailed);
        (ex.Data["detail"]?.ToString() ?? string.Empty).ShouldContain("Disabled Item");
        (ex.Data["detail"]?.ToString() ?? string.Empty).ShouldContain("Scrap Material");
    }

    [Fact]
    public async Task ValidateNoDisabledItems_DisabledOperationFgItem_ThrowsValidationFailed()
    {
        var fgItemId = Guid.NewGuid();
        var rawMatId = Guid.NewGuid();
        var semiFgId = Guid.NewGuid();
        var bom = new BillOfMaterials(Guid.NewGuid(), Guid.NewGuid(), "BOM-001", fgItemId);
        bom.AddItem(new BomItem(Guid.NewGuid(), bom.Id, rawMatId, "Raw Material", 2m, 10m));
        var op = new BomOperation(Guid.NewGuid(), bom.Id, Guid.NewGuid(), 1, 10m, Guid.NewGuid());
        op.FinishedGoodItemId = semiFgId;
        bom.AddOperation(op);

        var fgItem = new Item(fgItemId, bom.CompanyId, "FG-001", "Finished Good", ItemType.Goods) { IsActive = true };
        var rmItem = new Item(rawMatId, bom.CompanyId, "RM-001", "Raw Material", ItemType.Goods) { IsActive = true };
        var semiFgItem = new Item(semiFgId, bom.CompanyId, "SEMI-001", "Semi Finished Good", ItemType.Goods) { IsActive = false };

        var items = new List<Item> { fgItem, rmItem, semiFgItem }.AsQueryable();
        _itemRepository.GetQueryableAsync().Returns(Task.FromResult(items));

        var ex = await Should.ThrowAsync<BusinessException>(async () =>
            await _service.ValidateNoDisabledItemsAsync(bom));

        ex.Code.ShouldBe(MyERPDomainErrorCodes.ValidationFailed);
        (ex.Data["detail"]?.ToString() ?? string.Empty).ShouldContain("Disabled Item");
        (ex.Data["detail"]?.ToString() ?? string.Empty).ShouldContain("Semi Finished Good");
    }
}
