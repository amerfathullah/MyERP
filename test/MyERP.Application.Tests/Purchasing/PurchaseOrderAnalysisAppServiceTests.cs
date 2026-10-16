using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using MyERP.Core;
using MyERP.Core.Entities;
using MyERP.Inventory;
using MyERP.Inventory.Entities;
using MyERP.Purchasing.Entities;
using NSubstitute;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace MyERP.Purchasing;

/// <summary>
/// Unit tests for PurchaseOrderAnalysisAppService.
/// Implements ERPNext buying/report/purchase_order_analysis.
/// </summary>
public class PurchaseOrderAnalysisAppServiceTests
{
    private readonly IRepository<PurchaseOrder, Guid> _poRepo;
    private readonly IRepository<Supplier, Guid> _supplierRepo;
    private readonly IRepository<Item, Guid> _itemRepo;
    private readonly IRepository<Warehouse, Guid> _warehouseRepo;
    private readonly IRepository<Company, Guid> _companyRepo;
    private readonly PurchaseOrderAnalysisAppService _service;

    public PurchaseOrderAnalysisAppServiceTests()
    {
        _poRepo = Substitute.For<IRepository<PurchaseOrder, Guid>>();
        _supplierRepo = Substitute.For<IRepository<Supplier, Guid>>();
        _itemRepo = Substitute.For<IRepository<Item, Guid>>();
        _warehouseRepo = Substitute.For<IRepository<Warehouse, Guid>>();
        _companyRepo = Substitute.For<IRepository<Company, Guid>>();

        _service = new PurchaseOrderAnalysisAppService(
            _poRepo, _supplierRepo, _itemRepo, _warehouseRepo, _companyRepo);
    }

    [Fact]
    public async Task GetAnalysisAsync_CannotCombineGroupByPoAndGroupByItem()
    {
        var input = new GetPurchaseOrderAnalysisDto
        {
            CompanyId = Guid.NewGuid(),
            GroupByPo = true,
            GroupByItem = true
        };

        var ex = await Should.ThrowAsync<BusinessException>(() => _service.GetAnalysisAsync(input));
        ex.Code.ShouldBe(MyERPDomainErrorCodes.ValidationFailed);
    }

    [Fact]
    public async Task GetAnalysisAsync_FromDateGreaterThanToDate_ThrowsValidationException()
    {
        var input = new GetPurchaseOrderAnalysisDto
        {
            CompanyId = Guid.NewGuid(),
            FromDate = new DateTime(2026, 10, 15),
            ToDate = new DateTime(2026, 10, 1)
        };

        var ex = await Should.ThrowAsync<BusinessException>(() => _service.GetAnalysisAsync(input));
        ex.Code.ShouldBe(MyERPDomainErrorCodes.ValidationFailed);
    }

    [Fact]
    public async Task GetAnalysisAsync_DetailedRows_CalculatesQuantitiesAndAmounts()
    {
        var companyId = Guid.NewGuid();
        var supplierId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var orderDate = new DateTime(2026, 9, 1);
        var expectedDeliveryDate = new DateTime(2026, 9, 10);

        var po = new PurchaseOrder(Guid.NewGuid(), companyId, supplierId, "PO-0001", orderDate)
        {
            ExpectedDeliveryDate = expectedDeliveryDate
        };
        po.AddItem(itemId, "Test Item Description", quantity: 10, unitPrice: 100, taxAmount: 0, uom: "Nos");
        po.Items[0].ReceivedQty = 4;
        po.Items[0].BilledQty = 3;
        po.Submit();

        SetupRepositories(companyId, [po], [(supplierId, "Supplier A")], [(itemId, "ITEM-001", "Test Item")]);

        var result = await _service.GetAnalysisAsync(new GetPurchaseOrderAnalysisDto
        {
            CompanyId = companyId,
            FromDate = orderDate.AddDays(-1),
            ToDate = orderDate.AddDays(1)
        });

        result.Rows.Count.ShouldBe(1);
        var row = result.Rows[0];
        row.PurchaseOrderNumber.ShouldBe("PO-0001");
        row.SupplierName.ShouldBe("Supplier A");
        row.ItemCode.ShouldBe("ITEM-001");
        row.Uom.ShouldBe("Nos");
        row.Qty.ShouldBe(10);
        row.ReceivedQty.ShouldBe(4);
        row.PendingQty.ShouldBe(6); // 10 - 4
        row.BilledQty.ShouldBe(3);
        row.QtyToBill.ShouldBe(7); // 10 - 3
        row.Amount.ShouldBe(1000);
        row.ReceivedQtyAmount.ShouldBe(400);
        row.BilledAmount.ShouldBe(300);
        row.PendingAmount.ShouldBe(700); // 1000 - 300
        row.RequiredDate.ShouldBe(expectedDeliveryDate);

        result.TotalAmount.ShouldBe(1000);
        result.TotalBilledAmount.ShouldBe(300);
        result.TotalAmountToBill.ShouldBe(700);
        result.TotalQty.ShouldBe(10);
        result.TotalReceivedQty.ShouldBe(4);
    }

    [Fact]
    public async Task GetAnalysisAsync_GroupByPo_AggregatesFieldsAndDelay()
    {
        var companyId = Guid.NewGuid();
        var supplierId = Guid.NewGuid();
        var item1Id = Guid.NewGuid();
        var item2Id = Guid.NewGuid();
        var orderDate = new DateTime(2026, 9, 1);

        var po = new PurchaseOrder(Guid.NewGuid(), companyId, supplierId, "PO-0002", orderDate);
        po.AddItem(item1Id, "Item 1", quantity: 10, unitPrice: 50, taxAmount: 0, uom: "Nos");
        po.AddItem(item2Id, "Item 2", quantity: 5, unitPrice: 200, taxAmount: 0, uom: "Nos");
        po.Items[0].ReceivedQty = 2;
        po.Items[0].BilledQty = 2;
        po.Items[1].ReceivedQty = 1;
        po.Items[1].BilledQty = 0;
        po.Submit();

        SetupRepositories(companyId, [po], [(supplierId, "Supplier A")],
            [(item1Id, "ITEM-001", "Item 1"), (item2Id, "ITEM-002", "Item 2")]);

        var result = await _service.GetAnalysisAsync(new GetPurchaseOrderAnalysisDto
        {
            CompanyId = companyId,
            FromDate = orderDate.AddDays(-1),
            ToDate = orderDate.AddDays(1),
            GroupByPo = true
        });

        result.Rows.Count.ShouldBe(1);
        var row = result.Rows[0];
        row.PurchaseOrderNumber.ShouldBe("PO-0002");
        row.Qty.ShouldBe(15); // 10 + 5
        row.ReceivedQty.ShouldBe(3); // 2 + 1
        row.PendingQty.ShouldBe(12); // 8 + 4
        row.BilledQty.ShouldBe(2); // 2 + 0
        row.QtyToBill.ShouldBe(13); // 8 + 5
        row.Amount.ShouldBe(1500); // 500 + 1000
        row.ReceivedQtyAmount.ShouldBe(300); // 100 + 200
        row.BilledAmount.ShouldBe(100); // 100 + 0
        row.PendingAmount.ShouldBe(1400); // 1500 - 100
    }

    [Fact]
    public async Task GetAnalysisAsync_GroupByItem_AggregatesAcrossPurchaseOrdersForSameUom()
    {
        var companyId = Guid.NewGuid();
        var supplierId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var orderDate = new DateTime(2026, 9, 1);

        var po1 = new PurchaseOrder(Guid.NewGuid(), companyId, supplierId, "PO-0003", orderDate);
        po1.AddItem(itemId, "Item 1", quantity: 10, unitPrice: 50, taxAmount: 0, uom: "Nos");
        po1.Items[0].ReceivedQty = 3;
        po1.Submit();

        var po2 = new PurchaseOrder(Guid.NewGuid(), companyId, supplierId, "PO-0004", orderDate);
        po2.AddItem(itemId, "Item 1", quantity: 4, unitPrice: 50, taxAmount: 0, uom: "Nos");
        po2.Items[0].ReceivedQty = 0;
        po2.Submit();

        SetupRepositories(companyId, [po1, po2], [(supplierId, "Supplier A")], [(itemId, "ITEM-001", "Item 1")]);

        var result = await _service.GetAnalysisAsync(new GetPurchaseOrderAnalysisDto
        {
            CompanyId = companyId,
            FromDate = orderDate.AddDays(-1),
            ToDate = orderDate.AddDays(1),
            GroupByItem = true
        });

        // Combined into single item row
        result.Rows.Count.ShouldBe(1);
        var row = result.Rows[0];
        row.ItemCode.ShouldBe("ITEM-001");
        row.Uom.ShouldBe("Nos");
        row.Qty.ShouldBe(14); // 10 + 4
        row.ReceivedQty.ShouldBe(3);
        row.PendingQty.ShouldBe(11);
        row.Amount.ShouldBe(700); // 500 + 200
    }

    [Fact]
    public async Task GetAnalysisAsync_GroupByItem_KeepsDifferentUomsApart()
    {
        var companyId = Guid.NewGuid();
        var supplierId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var orderDate = new DateTime(2026, 9, 1);

        var po1 = new PurchaseOrder(Guid.NewGuid(), companyId, supplierId, "PO-0005", orderDate);
        po1.AddItem(itemId, "Item 1", quantity: 10, unitPrice: 50, taxAmount: 0, uom: "Nos");
        po1.Submit();

        var po2 = new PurchaseOrder(Guid.NewGuid(), companyId, supplierId, "PO-0006", orderDate);
        po2.AddItem(itemId, "Item 1", quantity: 2, unitPrice: 500, taxAmount: 0, uom: "Box");
        po2.Submit();

        SetupRepositories(companyId, [po1, po2], [(supplierId, "Supplier A")], [(itemId, "ITEM-001", "Item 1")]);

        var result = await _service.GetAnalysisAsync(new GetPurchaseOrderAnalysisDto
        {
            CompanyId = companyId,
            FromDate = orderDate.AddDays(-1),
            ToDate = orderDate.AddDays(1),
            GroupByItem = true
        });

        // Keeps "Box" and "Nos" apart
        result.Rows.Count.ShouldBe(2);
        result.Rows.Any(r => r.Uom == "Box" && r.Qty == 2).ShouldBeTrue();
        result.Rows.Any(r => r.Uom == "Nos" && r.Qty == 10).ShouldBeTrue();
    }

    private void SetupRepositories(
        Guid companyId,
        List<PurchaseOrder> orders,
        List<(Guid Id, string Name)> suppliers,
        List<(Guid Id, string ItemCode, string ItemName)> items)
    {
        _poRepo.WithDetailsAsync(Arg.Any<Expression<Func<PurchaseOrder, object>>[]>())
            .Returns(Task.FromResult(orders.AsQueryable()));

        var supplierEntities = suppliers.Select(s =>
        {
            var sup = new Supplier(s.Id, companyId, s.Name);
            return sup;
        }).AsQueryable();
        _supplierRepo.GetQueryableAsync().Returns(Task.FromResult(supplierEntities));

        var itemEntities = items.Select(i =>
        {
            var itm = new Item(i.Id, companyId, i.ItemCode, i.ItemName, ItemType.Goods);
            return itm;
        }).AsQueryable();
        _itemRepo.GetQueryableAsync().Returns(Task.FromResult(itemEntities));

        _warehouseRepo.GetQueryableAsync().Returns(Task.FromResult(new List<Warehouse>().AsQueryable()));

        var company = new Company(companyId, "Test Company");
        _companyRepo.FindAsync(companyId).Returns(Task.FromResult<Company?>(company));
    }
}
