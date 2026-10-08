using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Core;
using MyERP.Core.Entities;
using MyERP.Inventory.Entities;
using MyERP.Sales.DomainServices;
using MyERP.Sales.Entities;
using NSubstitute;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace MyERP.Sales;

/// <summary>
/// Regression tests for ERPNext PR #60140:
/// - Keep over delivery qty whole for whole number UOMs (Math.Floor on max_deliverable_qty)
/// - Allow delivery when fully delivered rows still have over-delivery allowance remaining
/// - Validate against Sales Order enforces floored allowance
/// </summary>
public class SalesOrderOverDeliveryAllowanceTests
{
    private readonly Guid _companyId = Guid.NewGuid();
    private readonly Guid _customerId = Guid.NewGuid();

    private SalesOrder CreateOrder()
    {
        return new SalesOrder(Guid.NewGuid(), _companyId, _customerId, "SO-TEST-001", DateTime.UtcNow);
    }

    [Fact]
    public void GetMaxDeliverableQty_StandardUom_CalculatesDecimalAllowance()
    {
        var order = CreateOrder();
        var itemId = Guid.NewGuid();
        order.AddItem(itemId, "Liquid Item", quantity: 10m, unitPrice: 20m, taxAmount: 0m, uom: "Liter");
        var item = order.Items.Single();

        // 50% allowance: 10 * 1.5 = 15.0
        var maxQty = item.GetMaxDeliverableQty(allowancePct: 50m, mustBeWholeNumber: false);
        maxQty.ShouldBe(15m);
    }

    [Fact]
    public void GetMaxDeliverableQty_WholeNumberUom_FloorsAllowedQuantity()
    {
        var order = CreateOrder();
        var itemId = Guid.NewGuid();
        order.AddItem(itemId, "Piece Item", quantity: 3m, unitPrice: 100m, taxAmount: 0m, uom: "Unit");
        var item = order.Items.Single();

        // 3 * (1 + 50/100) = 4.5 -> floored to 4.0 for whole number UOM
        var maxQty = item.GetMaxDeliverableQty(allowancePct: 50m, mustBeWholeNumber: true);
        maxQty.ShouldBe(4m);
    }

    [Fact]
    public void HasOverDeliverableRows_ReturnsTrue_WhenFullyDeliveredRowHasRemainingAllowance()
    {
        var order = CreateOrder();
        var itemId = Guid.NewGuid();
        order.AddItem(itemId, "Over Deliverable Item", quantity: 10m, unitPrice: 50m, taxAmount: 0m, uom: "Unit");
        var item = order.Items.Single();
        item.DeliveredQty = 10m; // fully delivered ordered qty
        order.Submit();

        // 50% allowance -> max is 15. Delivered is 10 < 15.
        var hasOverDeliverable = order.HasOverDeliverableRows(allowancePct: 50m);
        hasOverDeliverable.ShouldBeTrue();
    }

    [Fact]
    public void HasOverDeliverableRows_ReturnsFalse_WhenZeroAllowanceOrAlreadyAtMax()
    {
        var order = CreateOrder();
        var itemId = Guid.NewGuid();
        order.AddItem(itemId, "Fully Capped Item", quantity: 10m, unitPrice: 50m, taxAmount: 0m, uom: "Unit");
        var item = order.Items.Single();
        item.DeliveredQty = 15m; // delivered up to max allowance
        order.Submit();

        order.HasOverDeliverableRows(allowancePct: 50m).ShouldBeFalse();
        order.HasOverDeliverableRows(allowancePct: 0m).ShouldBeFalse();
    }

    [Fact]
    public void HasOverDeliverableRows_WholeNumberUom_ConsidersFlooredMax()
    {
        var order = CreateOrder();
        var itemId = Guid.NewGuid();
        order.AddItem(itemId, "Whole Unit Item", quantity: 3m, unitPrice: 10m, taxAmount: 0m, uom: "Unit");
        var item = order.Items.Single();
        item.DeliveredQty = 3m; // delivered ordered qty
        order.Submit();

        var uomMap = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase) { ["Unit"] = true };

        // max allowed is floor(4.5) = 4. Delivered is 3 < 4 -> true!
        order.HasOverDeliverableRows(50m, uomMap).ShouldBeTrue();

        // once delivered reaches 4 -> false!
        item.DeliveredQty = 4m;
        order.HasOverDeliverableRows(50m, uomMap).ShouldBeFalse();
    }

    [Fact]
    public void SalesOrderManager_ValidateDeliveryQty_HonorsWholeNumberUomFloor()
    {
        var order = CreateOrder();
        var itemId = Guid.NewGuid();
        order.AddItem(itemId, "Whole Item", quantity: 3m, unitPrice: 50m, taxAmount: 0m, uom: "Unit");
        order.Items.Single().DeliveredQty = 3m;

        var manager = new SalesOrderManager(null!);

        // Max is floor(3 * 1.5) = 4. Remaining = 4 - 3 = 1.
        // Delivering 1 succeeds:
        manager.ValidateDeliveryQty(order, itemId, deliveryQty: 1m, overDeliveryAllowancePct: 50m, mustBeWholeNumber: true);

        // Delivering 2 throws OverDelivery:
        var ex = Should.Throw<BusinessException>(() =>
            manager.ValidateDeliveryQty(order, itemId, deliveryQty: 2m, overDeliveryAllowancePct: 50m, mustBeWholeNumber: true));
        ex.Code.ShouldBe(MyERPDomainErrorCodes.OverDelivery);
    }

    [Fact]
    public async Task DeliveryNoteManager_ValidateAgainstSalesOrderAsync_EnforcesWholeNumberUomAllowance()
    {
        var dnRepo = Substitute.For<IRepository<DeliveryNote, Guid>>();
        var soRepo = Substitute.For<IRepository<SalesOrder, Guid>>();
        var companyRepo = Substitute.For<IRepository<Company, Guid>>();
        var itemRepo = Substitute.For<IRepository<Item, Guid>>();
        var uomRepo = Substitute.For<IRepository<Uom, Guid>>();

        var manager = new DeliveryNoteManager(dnRepo, soRepo, companyRepo, itemRepo, uomRepo);

        var company = new Company(_companyId, "Test Co") { OverDeliveryReceiptAllowance = 50m };
        companyRepo.GetAsync(_companyId).Returns(Task.FromResult(company));

        var order = CreateOrder();
        var itemId = Guid.NewGuid();
        order.AddItem(itemId, "Whole Widget", quantity: 3m, unitPrice: 100m, taxAmount: 0m, uom: "Unit");
        var soItem = order.Items.Single();
        soItem.DeliveredQty = 3m; // already delivered 3
        order.Submit();

        soRepo.GetAsync(order.Id).Returns(Task.FromResult(order));

        var uom = new Uom(Guid.NewGuid(), "Unit") { MustBeWholeNumber = true };
        uomRepo.GetQueryableAsync().Returns(Task.FromResult(new List<Uom> { uom }.AsQueryable()));

        // Delivery Note attempting 1 unit: 3 + 1 = 4 <= floor(4.5) -> passes
        var dnPass = new DeliveryNote(Guid.NewGuid(), _companyId, _customerId, Guid.NewGuid(), "DN-PASS", DateTime.UtcNow)
        {
            SalesOrderId = order.Id
        };
        dnPass.AddItem(itemId, "Whole Widget", quantity: 1m, unitPrice: 100m, taxAmount: 0m, uom: "Unit", salesOrderItemId: soItem.Id);
        await manager.ValidateAgainstSalesOrderAsync(dnPass);

        // Delivery Note attempting 2 units: 3 + 2 = 5 > floor(4.5) = 4 -> throws OverDelivery
        var dnFail = new DeliveryNote(Guid.NewGuid(), _companyId, _customerId, Guid.NewGuid(), "DN-FAIL", DateTime.UtcNow)
        {
            SalesOrderId = order.Id
        };
        dnFail.AddItem(itemId, "Whole Widget", quantity: 2m, unitPrice: 100m, taxAmount: 0m, uom: "Unit", salesOrderItemId: soItem.Id);

        var ex = await Should.ThrowAsync<BusinessException>(() => manager.ValidateAgainstSalesOrderAsync(dnFail));
        ex.Code.ShouldBe(MyERPDomainErrorCodes.OverDelivery);
    }
}
