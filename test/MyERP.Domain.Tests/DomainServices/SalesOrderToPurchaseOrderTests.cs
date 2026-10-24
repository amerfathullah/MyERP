using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Purchasing.DomainServices;
using MyERP.Purchasing.Entities;
using MyERP.Sales.DomainServices;
using MyERP.Sales.Entities;
using NSubstitute;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace MyERP.DomainServices;

/// <summary>
/// Sales Order → Purchase Order mapping (ERPNext sales_order/mapper.py make_purchase_order,
/// commit 92a4ccffab) and SO ordered-qty tracking from Purchase Order submit/cancel.
/// </summary>
public class SalesOrderToPurchaseOrderTests
{
    private static SalesOrder CreateSalesOrder() =>
        new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "SO-001", DateTime.Today);

    [Fact]
    public void ResolvePurchaseLines_RepeatedItemCode_StaysSeparatePerRow()
    {
        var so = CreateSalesOrder();
        var sameItem = Guid.NewGuid();
        so.AddItem(sameItem, "Row A", 5m, 10m, 0m);
        so.AddItem(sameItem, "Row B", 7m, 10m, 0m);
        var supplierA = Guid.NewGuid();
        var supplierB = Guid.NewGuid();

        var lines = DropShipService.ResolvePurchaseLines(so, new[]
        {
            (so.Items[0].Id, supplierA),
            (so.Items[1].Id, supplierB),
        });

        lines.Count.ShouldBe(2);
        lines.Single(l => l.Item.Id == so.Items[0].Id).SupplierId.ShouldBe(supplierA);
        lines.Single(l => l.Item.Id == so.Items[1].Id).SupplierId.ShouldBe(supplierB);
    }

    [Fact]
    public void ResolvePurchaseLines_UnknownRow_Throws()
    {
        var so = CreateSalesOrder();
        so.AddItem(Guid.NewGuid(), "Row A", 5m, 10m, 0m);

        Should.Throw<BusinessException>(() =>
            DropShipService.ResolvePurchaseLines(so, new[] { (Guid.NewGuid(), Guid.NewGuid()) }));
    }

    [Fact]
    public void ResolvePurchaseLines_QtyIsUnorderedRemainder_InTransactionUom()
    {
        var so = CreateSalesOrder();
        so.AddItem(Guid.NewGuid(), "Dozen row", 10m, 10m, 0m);
        var row = so.Items[0];
        row.ConversionFactor = 12m;
        row.OrderedQty = 24m; // stock qty already ordered = 2 dozen

        var lines = DropShipService.ResolvePurchaseLines(so, new[] { (row.Id, Guid.NewGuid()) });

        lines.Single().Quantity.ShouldBe(8m);
    }

    [Fact]
    public void ResolvePurchaseLines_SkipsClosedAndFullyOrderedRows()
    {
        var so = CreateSalesOrder();
        so.AddItem(Guid.NewGuid(), "Closed", 5m, 10m, 0m);
        so.AddItem(Guid.NewGuid(), "Fully ordered", 5m, 10m, 0m);
        so.AddItem(Guid.NewGuid(), "Open", 5m, 10m, 0m);
        so.Items[0].IsClosed = true;
        so.Items[1].OrderedQty = 5m;
        var supplier = Guid.NewGuid();

        var lines = DropShipService.ResolvePurchaseLines(so, so.Items.Select(i => (i.Id, supplier)));

        lines.Single().Item.Id.ShouldBe(so.Items[2].Id);
    }

    [Fact]
    public void ResolvePurchaseLines_RowSelectedTwice_LastSupplierWins()
    {
        var so = CreateSalesOrder();
        so.AddItem(Guid.NewGuid(), "Row", 5m, 10m, 0m);
        var first = Guid.NewGuid();
        var last = Guid.NewGuid();

        var lines = DropShipService.ResolvePurchaseLines(so, new[]
        {
            (so.Items[0].Id, first),
            (so.Items[0].Id, last),
        });

        lines.Single().SupplierId.ShouldBe(last);
    }

    [Fact]
    public async Task UpdateSalesOrderOrderedQty_SubmitThenCancel_RoundTrips()
    {
        var so = CreateSalesOrder();
        so.AddItem(Guid.NewGuid(), "Row", 10m, 10m, 0m);
        var soItem = so.Items[0];
        soItem.ConversionFactor = 2m;

        var po = new PurchaseOrder(Guid.NewGuid(), so.CompanyId, Guid.NewGuid(), "PO-001", DateTime.Today);
        var poItem = po.AddItem(soItem.ItemId, "Row", 4m, 0m, 0m);
        poItem.ConversionFactor = 2m;
        poItem.SalesOrderId = so.Id;
        poItem.SalesOrderItemId = soItem.Id;

        var repo = Substitute.For<IRepository<SalesOrder, Guid>>();
        repo.GetQueryableAsync().Returns(Task.FromResult(new List<SalesOrder> { so }.AsQueryable()));
        var manager = new PurchaseOrderManager(null!, null!, null!);

        await manager.UpdateSalesOrderOrderedQtyAsync(po, repo);
        soItem.OrderedQty.ShouldBe(8m); // 4 × 2 stock qty

        await manager.UpdateSalesOrderOrderedQtyAsync(po, repo, reverse: true);
        soItem.OrderedQty.ShouldBe(0m);

        // Reverse never goes below zero.
        await manager.UpdateSalesOrderOrderedQtyAsync(po, repo, reverse: true);
        soItem.OrderedQty.ShouldBe(0m);
    }

    [Fact]
    public async Task UpdateSalesOrderOrderedQty_PoWithoutSoLink_NoQuery()
    {
        var po = new PurchaseOrder(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "PO-002", DateTime.Today);
        po.AddItem(Guid.NewGuid(), "Plain", 1m, 1m, 0m);
        var repo = Substitute.For<IRepository<SalesOrder, Guid>>();

        await new PurchaseOrderManager(null!, null!, null!).UpdateSalesOrderOrderedQtyAsync(po, repo);

        await repo.DidNotReceive().GetQueryableAsync();
    }
}
