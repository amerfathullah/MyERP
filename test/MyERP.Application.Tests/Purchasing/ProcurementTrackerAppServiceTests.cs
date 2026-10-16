using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using MyERP.Accounting.Entities;
using MyERP.Core;
using MyERP.Inventory;
using MyERP.Inventory.Entities;
using MyERP.Projects.Entities;
using MyERP.Purchasing.Entities;
using NSubstitute;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace MyERP.Purchasing;

public class ProcurementTrackerAppServiceTests
{
    private readonly IRepository<PurchaseOrder, Guid> _poRepo;
    private readonly IRepository<MaterialRequest, Guid> _mrRepo;
    private readonly IRepository<PurchaseReceipt, Guid> _prRepo;
    private readonly IRepository<PurchaseInvoice, Guid> _piRepo;
    private readonly IRepository<Supplier, Guid> _supplierRepo;
    private readonly IRepository<Item, Guid> _itemRepo;
    private readonly IRepository<Warehouse, Guid> _warehouseRepo;
    private readonly IRepository<Project, Guid> _projectRepo;
    private readonly IRepository<CostCenter, Guid> _costCenterRepo;
    private readonly ProcurementTrackerAppService _service;

    public ProcurementTrackerAppServiceTests()
    {
        _poRepo = Substitute.For<IRepository<PurchaseOrder, Guid>>();
        _mrRepo = Substitute.For<IRepository<MaterialRequest, Guid>>();
        _prRepo = Substitute.For<IRepository<PurchaseReceipt, Guid>>();
        _piRepo = Substitute.For<IRepository<PurchaseInvoice, Guid>>();
        _supplierRepo = Substitute.For<IRepository<Supplier, Guid>>();
        _itemRepo = Substitute.For<IRepository<Item, Guid>>();
        _warehouseRepo = Substitute.For<IRepository<Warehouse, Guid>>();
        _projectRepo = Substitute.For<IRepository<Project, Guid>>();
        _costCenterRepo = Substitute.For<IRepository<CostCenter, Guid>>();

        _service = new ProcurementTrackerAppService(
            _poRepo,
            _mrRepo,
            _prRepo,
            _piRepo,
            _supplierRepo,
            _itemRepo,
            _warehouseRepo,
            _projectRepo,
            _costCenterRepo);
    }

    [Fact]
    public async Task GetReportAsync_Throws_WhenFromDateGreaterThanToDate()
    {
        var input = new ProcurementTrackerFilterDto
        {
            CompanyId = Guid.NewGuid(),
            FromDate = new DateTime(2026, 5, 10),
            ToDate = new DateTime(2026, 5, 1)
        };

        var ex = await Should.ThrowAsync<BusinessException>(() => _service.GetReportAsync(input));
        ex.Code.ShouldBe(MyERPDomainErrorCodes.ValidationFailed);
    }

    [Fact]
    public async Task GetReportAsync_ReturnsEmpty_WhenNoMatchingRecords()
    {
        var companyId = Guid.NewGuid();
        _poRepo.WithDetailsAsync(Arg.Any<Expression<Func<PurchaseOrder, object>>[]>())
            .Returns(Task.FromResult(new List<PurchaseOrder>().AsQueryable()));
        _mrRepo.WithDetailsAsync(Arg.Any<Expression<Func<MaterialRequest, object>>[]>())
            .Returns(Task.FromResult(new List<MaterialRequest>().AsQueryable()));
        _supplierRepo.GetQueryableAsync()
            .Returns(Task.FromResult(new List<Supplier>().AsQueryable()));
        _itemRepo.GetQueryableAsync()
            .Returns(Task.FromResult(new List<Item>().AsQueryable()));
        _warehouseRepo.GetQueryableAsync()
            .Returns(Task.FromResult(new List<Warehouse>().AsQueryable()));
        _costCenterRepo.GetQueryableAsync()
            .Returns(Task.FromResult(new List<CostCenter>().AsQueryable()));
        _projectRepo.GetQueryableAsync()
            .Returns(Task.FromResult(new List<Project>().AsQueryable()));

        var input = new ProcurementTrackerFilterDto
        {
            CompanyId = companyId
        };

        var result = await _service.GetReportAsync(input);

        result.Rows.ShouldBeEmpty();
        result.Summary.TotalOrders.ShouldBe(0);
        result.Summary.TotalRequisitions.ShouldBe(0);
    }

    [Fact]
    public async Task GetReportAsync_ComputesProcurementLifecycle_WithUnorderedMR_AndPO_PR_PI()
    {
        var companyId = Guid.NewGuid();
        var supplierId = Guid.NewGuid();
        var warehouseId = Guid.NewGuid();
        var itemId1 = Guid.NewGuid();
        var itemId2 = Guid.NewGuid();
        var today = new DateTime(2026, 6, 1);

        // 1. Material Requests:
        // MR 1: Unordered MR item (OrderedQuantity = 0)
        var mr1 = new MaterialRequest(Guid.NewGuid(), companyId, "MAT-REQ-001", MaterialRequestType.Purchase, today);
        mr1.AddItem(itemId1, "Widget Alpha", quantity: 50m, uom: "Nos", warehouseId: warehouseId);
        mr1.Submit();

        // MR 2: Ordered MR item (linked to PO)
        var mr2 = new MaterialRequest(Guid.NewGuid(), companyId, "MAT-REQ-002", MaterialRequestType.Purchase, today);
        mr2.AddItem(itemId2, "Widget Beta", quantity: 100m, uom: "Nos", warehouseId: warehouseId);
        mr2.Submit();
        var mr2Item = mr2.Items.First();
        mr2Item.OrderedQuantity = 100m;

        _mrRepo.WithDetailsAsync(Arg.Any<Expression<Func<MaterialRequest, object>>[]>())
            .Returns(Task.FromResult(new List<MaterialRequest> { mr1, mr2 }.AsQueryable()));

        // 2. Purchase Order:
        var po = new PurchaseOrder(Guid.NewGuid(), companyId, supplierId, "PO-2026-001", today.AddDays(2));
        po.AddItem(itemId2, "Widget Beta", quantity: 100m, unitPrice: 20m, taxAmount: 0m, uom: "Nos");
        var poItem = po.Items.First();
        poItem.MaterialRequestItemId = mr2Item.Id;
        poItem.WarehouseId = warehouseId;
        poItem.ExpectedDeliveryDate = today.AddDays(10);
        po.Submit();

        _poRepo.WithDetailsAsync(Arg.Any<Expression<Func<PurchaseOrder, object>>[]>())
            .Returns(Task.FromResult(new List<PurchaseOrder> { po }.AsQueryable()));

        // 3. Purchase Receipt (Actual Delivery Date):
        var pr = new PurchaseReceipt(Guid.NewGuid(), companyId, supplierId, warehouseId, "PR-2026-001", today.AddDays(8));
        pr.AddItem(itemId2, "Widget Beta", quantity: 80m, unitPrice: 20m, taxAmount: 0m, uom: "Nos", purchaseOrderItemId: poItem.Id);
        pr.Submit();

        _prRepo.WithDetailsAsync(Arg.Any<Expression<Func<PurchaseReceipt, object>>[]>())
            .Returns(Task.FromResult(new List<PurchaseReceipt> { pr }.AsQueryable()));

        var pi = new PurchaseInvoice(Guid.NewGuid(), companyId, supplierId, "PINV-2026-001", today.AddDays(9));
        pi.AddItem(itemId2, "Widget Beta", quantity: 80m, unitPrice: 20m, taxAmount: 0m, uom: "Nos");
        pi.Items.First().PurchaseOrderItemId = poItem.Id;
        pi.Submit();

        _piRepo.WithDetailsAsync(Arg.Any<Expression<Func<PurchaseInvoice, object>>[]>())
            .Returns(Task.FromResult(new List<PurchaseInvoice> { pi }.AsQueryable()));

        // 5. Lookups
        var supplier = new Supplier(supplierId, companyId, "Acme Supplier");
        _supplierRepo.GetQueryableAsync()
            .Returns(Task.FromResult(new List<Supplier> { supplier }.AsQueryable()));

        var item1 = new Item(itemId1, companyId, "ITEM-ALPHA", "Widget Alpha", ItemType.Goods) { StandardBuyingPrice = 10m };
        var item2 = new Item(itemId2, companyId, "ITEM-BETA", "Widget Beta", ItemType.Goods) { StandardBuyingPrice = 20m };
        _itemRepo.GetQueryableAsync()
            .Returns(Task.FromResult(new List<Item> { item1, item2 }.AsQueryable()));

        var warehouse = new Warehouse(warehouseId, companyId, "Stores - Central");
        _warehouseRepo.GetQueryableAsync()
            .Returns(Task.FromResult(new List<Warehouse> { warehouse }.AsQueryable()));

        _costCenterRepo.GetQueryableAsync()
            .Returns(Task.FromResult(new List<CostCenter>().AsQueryable()));
        _projectRepo.GetQueryableAsync()
            .Returns(Task.FromResult(new List<Project>().AsQueryable()));

        var input = new ProcurementTrackerFilterDto
        {
            CompanyId = companyId,
            ShowCompletedOrders = true
        };

        var result = await _service.GetReportAsync(input);

        // Expect 2 rows: 1 for unordered MR1, 1 for PO (linked to MR2)
        result.Rows.Count.ShouldBe(2);

        var mrRow = result.Rows.First(r => r.MaterialRequestNo == "MAT-REQ-001");
        mrRow.ItemCode.ShouldBe("ITEM-ALPHA");
        mrRow.Quantity.ShouldBe(50m);
        mrRow.EstimatedCost.ShouldBe(500m); // 50 * 10
        mrRow.ActualCost.ShouldBe(0m);
        mrRow.PurchaseOrderNo.ShouldBeNull();
        mrRow.RequestingSite.ShouldBe("Stores - Central");

        var poRow = result.Rows.First(r => r.PurchaseOrderNo == "PO-2026-001");
        poRow.MaterialRequestNo.ShouldBe("MAT-REQ-002");
        poRow.SupplierName.ShouldBe("Acme Supplier");
        poRow.ItemCode.ShouldBe("ITEM-BETA");
        poRow.Quantity.ShouldBe(100m);
        poRow.PurchaseOrderAmount.ShouldBe(2000m); // 100 * 20
        poRow.ActualCost.ShouldBe(1600m); // Billed 80 * 20
        poRow.ExpectedDeliveryDate.ShouldBe(today.AddDays(10));
        poRow.ActualDeliveryDate.ShouldBe(today.AddDays(8));

        result.Summary.TotalRequisitions.ShouldBe(2);
        result.Summary.TotalOrders.ShouldBe(1);
        result.Summary.TotalEstimatedCost.ShouldBe(2500m);
        result.Summary.TotalActualCost.ShouldBe(1600m);
        result.Summary.TotalPurchaseOrderAmount.ShouldBe(2000m);
    }
}
