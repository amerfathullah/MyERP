using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Core;
using MyERP.Inventory;
using MyERP.Inventory.Entities;
using MyERP.Purchasing;
using MyERP.Purchasing.Entities;
using NSubstitute;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace MyERP.Application.Tests.Inventory;

public class RequestedItemsToTransferAppServiceTests
{
    private readonly IRepository<MaterialRequest, Guid> _mrRepo = Substitute.For<IRepository<MaterialRequest, Guid>>();
    private readonly IRepository<Item, Guid> _itemRepo = Substitute.For<IRepository<Item, Guid>>();
    private readonly IRepository<Warehouse, Guid> _warehouseRepo = Substitute.For<IRepository<Warehouse, Guid>>();

    private readonly RequestedItemsToTransferAppService _appService;
    private readonly Guid _companyId = Guid.NewGuid();

    public RequestedItemsToTransferAppServiceTests()
    {
        _appService = new RequestedItemsToTransferAppService(
            _mrRepo,
            _itemRepo,
            _warehouseRepo);
    }

    [Fact]
    public async Task GetReportAsync_ReturnsPendingItemsForSubmittedMaterialTransfersAndIssues()
    {
        // Arrange
        var today = DateTime.UtcNow.Date;
        var sourceWhId = Guid.NewGuid();
        var targetWhId = Guid.NewGuid();

        var item1Id = Guid.NewGuid();
        var item2Id = Guid.NewGuid();
        var item3Id = Guid.NewGuid();

        var item1 = new Item(item1Id, _companyId, "ITEM-001", "Motor Assembly", ItemType.Goods) { Description = "Main motor" };
        var item2 = new Item(item2Id, _companyId, "ITEM-002", "Bolt M8", ItemType.Goods) { Description = "Fastener" };
        var item3 = new Item(item3Id, _companyId, "ITEM-003", "Lubricant Oil", ItemType.Goods) { Description = "Machine oil" };

        _itemRepo.GetQueryableAsync().Returns(new List<Item> { item1, item2, item3 }.AsQueryable());

        // 1. Submitted MaterialTransfer with 2 items (one partial, one fully transferred)
        var mr1 = new MaterialRequest(Guid.NewGuid(), _companyId, "MAT-REQ-0001", MaterialRequestType.MaterialTransfer, today.AddDays(-2))
        {
            SourceWarehouseId = sourceWhId,
            TargetWarehouseId = targetWhId
        };
        mr1.AddItem(item1Id, "Motor Assembly", 10m, "Nos", sourceWhId);
        mr1.Items[0].OrderedQuantity = 4m; // 6 to transfer
        mr1.AddItem(item2Id, "Bolt M8", 5m, "Nos", sourceWhId);
        mr1.Items[1].OrderedQuantity = 5m; // 0 to transfer (fully completed, should be excluded)
        mr1.Submit();

        // 2. Submitted MaterialIssue with 1 pending item
        var mr2 = new MaterialRequest(Guid.NewGuid(), _companyId, "MAT-REQ-0002", MaterialRequestType.MaterialIssue, today.AddDays(-1))
        {
            SourceWarehouseId = sourceWhId
        };
        mr2.AddItem(item3Id, "Lubricant Oil", 8m, "Litre", sourceWhId);
        mr2.Items[0].OrderedQuantity = 0m; // 8 to transfer
        mr2.Submit();

        // 3. Purchase MaterialRequest (should be excluded)
        var mr3 = new MaterialRequest(Guid.NewGuid(), _companyId, "MAT-REQ-0003", MaterialRequestType.Purchase, today.AddDays(-3));
        mr3.AddItem(item1Id, "Motor Assembly", 20m, "Nos");
        mr3.Submit();

        // 4. Draft MaterialTransfer (should be excluded)
        var mr4 = new MaterialRequest(Guid.NewGuid(), _companyId, "MAT-REQ-0004", MaterialRequestType.MaterialTransfer, today);
        mr4.AddItem(item1Id, "Motor Assembly", 15m, "Nos");

        var allRequests = new List<MaterialRequest> { mr1, mr2, mr3, mr4 };
        _mrRepo.WithDetailsAsync(Arg.Any<System.Linq.Expressions.Expression<Func<MaterialRequest, object>>[]>())
            .Returns(allRequests.AsQueryable());

        var filter = new RequestedItemsToTransferFilterDto
        {
            CompanyId = _companyId
        };

        // Act
        var result = await _appService.GetReportAsync(filter);

        // Assert
        result.ShouldNotBeNull();
        result.TotalRequests.ShouldBe(2);
        result.TotalItems.ShouldBe(2);
        result.TotalQtyToTransfer.ShouldBe(14m); // 6 + 8

        var row1 = result.Rows.FirstOrDefault(r => r.RequestNumber == "MAT-REQ-0001");
        row1.ShouldNotBeNull();
        row1.ItemCode.ShouldBe("ITEM-001");
        row1.RequestedQty.ShouldBe(10m);
        row1.TransferredQty.ShouldBe(4m);
        row1.QtyToTransfer.ShouldBe(6m);
        row1.CompletionPercent.ShouldBe(40m);

        var row2 = result.Rows.FirstOrDefault(r => r.RequestNumber == "MAT-REQ-0002");
        row2.ShouldNotBeNull();
        row2.ItemCode.ShouldBe("ITEM-003");
        row2.RequestedQty.ShouldBe(8m);
        row2.TransferredQty.ShouldBe(0m);
        row2.QtyToTransfer.ShouldBe(8m);
        row2.CompletionPercent.ShouldBe(0m);
    }

    [Fact]
    public async Task GetReportAsync_FiltersByItemIdAndSearchText()
    {
        // Arrange
        var today = DateTime.UtcNow.Date;
        var item1Id = Guid.NewGuid();
        var item2Id = Guid.NewGuid();

        var item1 = new Item(item1Id, _companyId, "ITEM-001", "Motor Assembly", ItemType.Goods);
        var item2 = new Item(item2Id, _companyId, "ITEM-002", "Piston Ring", ItemType.Goods);
        _itemRepo.GetQueryableAsync().Returns(new List<Item> { item1, item2 }.AsQueryable());

        var mr1 = new MaterialRequest(Guid.NewGuid(), _companyId, "MAT-REQ-0010", MaterialRequestType.MaterialTransfer, today);
        mr1.AddItem(item1Id, "Motor Assembly", 10m, "Nos");
        mr1.AddItem(item2Id, "Piston Ring", 20m, "Nos");
        mr1.Submit();

        _mrRepo.WithDetailsAsync(Arg.Any<System.Linq.Expressions.Expression<Func<MaterialRequest, object>>[]>())
            .Returns(new List<MaterialRequest> { mr1 }.AsQueryable());

        // Act - filter by item2
        var filter = new RequestedItemsToTransferFilterDto
        {
            CompanyId = _companyId,
            ItemId = item2Id
        };
        var result = await _appService.GetReportAsync(filter);

        // Assert
        result.Rows.Count.ShouldBe(1);
        result.Rows[0].ItemCode.ShouldBe("ITEM-002");

        // Act - filter by search text "motor"
        var searchFilter = new RequestedItemsToTransferFilterDto
        {
            CompanyId = _companyId,
            SearchText = "motor"
        };
        var searchResult = await _appService.GetReportAsync(searchFilter);

        // Assert
        searchResult.Rows.Count.ShouldBe(1);
        searchResult.Rows[0].ItemCode.ShouldBe("ITEM-001");
    }
}
