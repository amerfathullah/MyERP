using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using MyERP.Inventory;
using MyERP.Inventory.Entities;
using MyERP.Purchasing.Entities;
using NSubstitute;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace MyERP.Purchasing;

public class SubcontractedRawMaterialsToBeTransferredAppServiceTests
{
    private readonly IRepository<SubcontractingOrder, Guid> _scoRepo;
    private readonly IRepository<Supplier, Guid> _supplierRepo;
    private readonly IRepository<Item, Guid> _itemRepo;
    private readonly IRepository<Warehouse, Guid> _warehouseRepo;
    private readonly SubcontractedRawMaterialsToBeTransferredAppService _service;

    public SubcontractedRawMaterialsToBeTransferredAppServiceTests()
    {
        _scoRepo = Substitute.For<IRepository<SubcontractingOrder, Guid>>();
        _supplierRepo = Substitute.For<IRepository<Supplier, Guid>>();
        _itemRepo = Substitute.For<IRepository<Item, Guid>>();
        _warehouseRepo = Substitute.For<IRepository<Warehouse, Guid>>();

        _service = new SubcontractedRawMaterialsToBeTransferredAppService(
            _scoRepo,
            _supplierRepo,
            _itemRepo,
            _warehouseRepo);
    }

    [Fact]
    public async Task GetReportAsync_Throws_WhenFromDateGreaterThanToDate()
    {
        var input = new SubcontractedRawMaterialsToBeTransferredFilterDto
        {
            CompanyId = Guid.NewGuid(),
            FromDate = new DateTime(2026, 5, 10),
            ToDate = new DateTime(2026, 5, 1)
        };

        var ex = await Should.ThrowAsync<BusinessException>(() => _service.GetReportAsync(input));
        ex.Code.ShouldBe(MyERPDomainErrorCodes.ValidationFailed);
    }

    [Fact]
    public async Task GetReportAsync_ReturnsEmpty_WhenNoMatchingOrders()
    {
        var companyId = Guid.NewGuid();
        _scoRepo.WithDetailsAsync(Arg.Any<Expression<Func<SubcontractingOrder, object>>[]>())
            .Returns(Task.FromResult(new List<SubcontractingOrder>().AsQueryable()));

        var input = new SubcontractedRawMaterialsToBeTransferredFilterDto
        {
            CompanyId = companyId
        };

        var result = await _service.GetReportAsync(input);

        result.Rows.ShouldBeEmpty();
        result.Summary.TotalOrders.ShouldBe(0);
        result.Summary.TotalPendingQty.ShouldBe(0);
    }

    [Fact]
    public async Task GetReportAsync_FiltersCompletedAndTransferred_AndCalculatesPendingQtyCorrectly()
    {
        var companyId = Guid.NewGuid();
        var supplier1Id = Guid.NewGuid();
        var supplier2Id = Guid.NewGuid();
        var rmItem1Id = Guid.NewGuid();
        var rmItem2Id = Guid.NewGuid();
        var fgItemId = Guid.NewGuid();
        var today = new DateTime(2026, 5, 15);

        // Order 1: Submitted / Open with 2 supplied items (one partial transferred, one fully transferred)
        var sco1 = new SubcontractingOrder(Guid.NewGuid(), companyId, "SCO-2026-001", today, supplier1Id);
        sco1.AddItem(new SubcontractingOrderItem(Guid.NewGuid(), sco1.Id, fgItemId, "FG Product", qty: 10m, rate: 50m));
        sco1.AddSuppliedItem(new SubcontractingOrderSuppliedItem(Guid.NewGuid(), sco1.Id, rmItem1Id, "Steel Rod", requiredQty: 20m)
        {
            TransferredQty = 8m // Pending: 12
        });
        sco1.AddSuppliedItem(new SubcontractingOrderSuppliedItem(Guid.NewGuid(), sco1.Id, rmItem2Id, "Plastic Bolt", requiredQty: 10m)
        {
            TransferredQty = 10m // Pending: 0 -> should NOT appear
        });
        sco1.Submit();

        // Order 2: Draft order -> should be ignored
        var sco2 = new SubcontractingOrder(Guid.NewGuid(), companyId, "SCO-DRAFT", today, supplier1Id);
        sco2.AddItem(new SubcontractingOrderItem(Guid.NewGuid(), sco2.Id, fgItemId, "FG Product", qty: 5m, rate: 50m));
        sco2.AddSuppliedItem(new SubcontractingOrderSuppliedItem(Guid.NewGuid(), sco2.Id, rmItem1Id, "Steel Rod", requiredQty: 10m));

        // Order 3: Closed order -> should be ignored
        var sco3 = new SubcontractingOrder(Guid.NewGuid(), companyId, "SCO-CLOSED", today, supplier2Id);
        sco3.AddItem(new SubcontractingOrderItem(Guid.NewGuid(), sco3.Id, fgItemId, "FG Product", qty: 10m, rate: 50m));
        sco3.AddSuppliedItem(new SubcontractingOrderSuppliedItem(Guid.NewGuid(), sco3.Id, rmItem1Id, "Steel Rod", requiredQty: 20m)
        {
            TransferredQty = 5m
        });
        sco3.Submit();
        sco3.Close();

        var orders = new List<SubcontractingOrder> { sco1, sco2, sco3 }.AsQueryable();
        _scoRepo.WithDetailsAsync(Arg.Any<Expression<Func<SubcontractingOrder, object>>[]>())
            .Returns(Task.FromResult(orders));

        var suppliers = new List<Supplier>
        {
            new Supplier(supplier1Id, companyId, "Supplier Metalworks"),
            new Supplier(supplier2Id, companyId, "Supplier Plastics")
        }.AsQueryable();
        _supplierRepo.GetQueryableAsync().Returns(Task.FromResult(suppliers));

        var items = new List<Item>
        {
            new Item(rmItem1Id, companyId, "RM-STEEL", "Steel Rod", ItemType.Goods),
            new Item(rmItem2Id, companyId, "RM-BOLT", "Plastic Bolt", ItemType.Goods)
        }.AsQueryable();
        _itemRepo.GetQueryableAsync().Returns(Task.FromResult(items));

        _warehouseRepo.GetQueryableAsync().Returns(Task.FromResult(new List<Warehouse>().AsQueryable()));

        var input = new SubcontractedRawMaterialsToBeTransferredFilterDto
        {
            CompanyId = companyId,
            FromDate = today.AddDays(-1),
            ToDate = today.AddDays(1)
        };

        var result = await _service.GetReportAsync(input);

        result.Rows.Count.ShouldBe(1);
        var row = result.Rows[0];
        row.SubcontractOrderNumber.ShouldBe("SCO-2026-001");
        row.SupplierName.ShouldBe("Supplier Metalworks");
        row.RmItemCode.ShouldBe("RM-STEEL");
        row.RmItemName.ShouldBe("Steel Rod");
        row.RequiredQty.ShouldBe(20m);
        row.TransferredQty.ShouldBe(8m);
        row.PendingQty.ShouldBe(12m);

        result.Summary.TotalOrders.ShouldBe(1);
        result.Summary.TotalRequiredQty.ShouldBe(20m);
        result.Summary.TotalTransferredQty.ShouldBe(8m);
        result.Summary.TotalPendingQty.ShouldBe(12m);
    }

    [Fact]
    public async Task GetReportAsync_FiltersBySupplier()
    {
        var companyId = Guid.NewGuid();
        var supplier1Id = Guid.NewGuid();
        var supplier2Id = Guid.NewGuid();
        var rmItemId = Guid.NewGuid();
        var fgItemId = Guid.NewGuid();
        var today = new DateTime(2026, 5, 15);

        var sco1 = new SubcontractingOrder(Guid.NewGuid(), companyId, "SCO-SUP1", today, supplier1Id);
        sco1.AddItem(new SubcontractingOrderItem(Guid.NewGuid(), sco1.Id, fgItemId, "FG 1", 10m, 50m));
        sco1.AddSuppliedItem(new SubcontractingOrderSuppliedItem(Guid.NewGuid(), sco1.Id, rmItemId, "Steel Rod", 15m));
        sco1.Submit();

        var sco2 = new SubcontractingOrder(Guid.NewGuid(), companyId, "SCO-SUP2", today, supplier2Id);
        sco2.AddItem(new SubcontractingOrderItem(Guid.NewGuid(), sco2.Id, fgItemId, "FG 2", 10m, 50m));
        sco2.AddSuppliedItem(new SubcontractingOrderSuppliedItem(Guid.NewGuid(), sco2.Id, rmItemId, "Steel Rod", 25m));
        sco2.Submit();

        var orders = new List<SubcontractingOrder> { sco1, sco2 }.AsQueryable();
        _scoRepo.WithDetailsAsync(Arg.Any<Expression<Func<SubcontractingOrder, object>>[]>())
            .Returns(Task.FromResult(orders));

        var suppliers = new List<Supplier>
        {
            new Supplier(supplier1Id, companyId, "Alpha Supplier"),
            new Supplier(supplier2Id, companyId, "Beta Supplier")
        }.AsQueryable();
        _supplierRepo.GetQueryableAsync().Returns(Task.FromResult(suppliers));

        var items = new List<Item>
        {
            new Item(rmItemId, companyId, "RM-STEEL", "Steel Rod", ItemType.Goods)
        }.AsQueryable();
        _itemRepo.GetQueryableAsync().Returns(Task.FromResult(items));

        _warehouseRepo.GetQueryableAsync().Returns(Task.FromResult(new List<Warehouse>().AsQueryable()));

        var input = new SubcontractedRawMaterialsToBeTransferredFilterDto
        {
            CompanyId = companyId,
            SupplierId = supplier1Id
        };

        var result = await _service.GetReportAsync(input);

        result.Rows.Count.ShouldBe(1);
        result.Rows[0].SubcontractOrderNumber.ShouldBe("SCO-SUP1");
        result.Rows[0].SupplierName.ShouldBe("Alpha Supplier");
    }
}
