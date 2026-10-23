using System;
using MyERP.Core.DomainServices;
using MyERP.Purchasing.Entities;
using MyERP.Sales.Entities;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace MyERP.Core;

public class ChildItemUpdateServiceTests
{
    private readonly ChildItemUpdateService _service = new();

    [Fact]
    public void ValidateSalesOrderItemUpdate_OpenItem_Succeeds()
    {
        var item = new SalesOrderItem(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Item", 10, 50, 0, "Unit");
        Should.NotThrow(() => _service.ValidateSalesOrderItemUpdate(item, 12, 55));
    }

    [Fact]
    public void ValidateSalesOrderItemUpdate_ClosedItem_ThrowsValidationFailed()
    {
        var item = new SalesOrderItem(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Item", 10, 50, 0, "Unit")
        {
            IsClosed = true
        };
        var ex = Should.Throw<BusinessException>(() => _service.ValidateSalesOrderItemUpdate(item, 12, 50));
        ex.Code.ShouldBe(MyERPDomainErrorCodes.ValidationFailed);
    }

    [Fact]
    public void ValidatePurchaseOrderItemUpdate_ClosedItem_ThrowsValidationFailed()
    {
        var item = new PurchaseOrderItem(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Item", 10, 50, 0, "Unit")
        {
            IsClosed = true
        };
        var ex = Should.Throw<BusinessException>(() => _service.ValidatePurchaseOrderItemUpdate(item, 10, 55));
        ex.Code.ShouldBe(MyERPDomainErrorCodes.ValidationFailed);
    }

    [Fact]
    public void ValidateSalesOrderItemDeletion_ClosedItem_ThrowsValidationFailed()
    {
        var item = new SalesOrderItem(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Item", 10, 50, 0, "Unit")
        {
            IsClosed = true
        };
        var ex = Should.Throw<BusinessException>(() => _service.ValidateSalesOrderItemDeletion(item));
        ex.Code.ShouldBe(MyERPDomainErrorCodes.ValidationFailed);
    }

    [Fact]
    public void ValidatePurchaseOrderItemDeletion_ClosedItem_ThrowsValidationFailed()
    {
        var item = new PurchaseOrderItem(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Item", 10, 50, 0, "Unit")
        {
            IsClosed = true
        };
        var ex = Should.Throw<BusinessException>(() => _service.ValidatePurchaseOrderItemDeletion(item));
        ex.Code.ShouldBe(MyERPDomainErrorCodes.ValidationFailed);
    }

    [Fact]
    public void ValidateSalesOrderItemStockQty_WithConversionFactor_ComparesInStockUom()
    {
        // 6 boxes with conversion factor 5 = 30 in stock UOM
        var item = new SalesOrderItem(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Item", 6, 10, 0, "Box")
        {
            ConversionFactor = 5,
            DeliveredQty = 2 // 2 boxes = 10 units delivered in stock UOM
        };

        // Reducing to 5 boxes with conversion factor 2 = 10 units (equal to delivered 10) -> succeeds
        Should.NotThrow(() => _service.ValidateSalesOrderItemStockQty(item, 5, 2));

        // Reducing to 4 boxes with conversion factor 2 = 8 units (< delivered 10) -> throws
        var ex = Should.Throw<BusinessException>(() => _service.ValidateSalesOrderItemStockQty(item, 4, 2));
        ex.Code.ShouldBe(MyERPDomainErrorCodes.ValidationFailed);
    }

    [Fact]
    public void ValidatePurchaseOrderItemStockQty_WithConversionFactor_ComparesInStockUom()
    {
        // 6 boxes with conversion factor 5 = 30 in stock UOM
        var item = new PurchaseOrderItem(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Item", 6, 10, 0, "Box")
        {
            ConversionFactor = 5,
            ReceivedQty = 2 // 2 boxes = 10 units received in stock UOM
        };

        // Reducing to 5 boxes with conversion factor 2 = 10 units (equal to received 10) -> succeeds
        Should.NotThrow(() => _service.ValidatePurchaseOrderItemStockQty(item, 5, 2));

        // Reducing to 4 boxes with conversion factor 2 = 8 units (< received 10) -> throws
        var ex = Should.Throw<BusinessException>(() => _service.ValidatePurchaseOrderItemStockQty(item, 4, 2));
        ex.Code.ShouldBe(MyERPDomainErrorCodes.ValidationFailed);
    }

    [Fact]
    public void ValidateQuotationItemUpdate_RateChange_BlockedWhenOrdered()
    {
        var item = new QuotationItem(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Item", 10, 100, 0, "Unit")
        {
            OrderedQty = 5
        };

        // Same rate succeeds
        Should.NotThrow(() => _service.ValidateQuotationItemUpdate(item, 8, 100));

        // Rate change throws
        var ex = Should.Throw<BusinessException>(() => _service.ValidateQuotationItemUpdate(item, 10, 120));
        ex.Code.ShouldBe(MyERPDomainErrorCodes.ValidationFailed);
    }

    [Fact]
    public void ValidateQuotationItemStockQty_WithConversionFactor_ComparesInStockUom()
    {
        // 6 boxes with conversion factor 5 = 30 in stock UOM
        var item = new QuotationItem(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Item", 6, 10, 0, "Box")
        {
            ConversionFactor = 5,
            OrderedQty = 10 // 10 units ordered in stock UOM
        };

        // Reducing to 5 boxes with conversion factor 2 = 10 units (equal to ordered 10) -> succeeds
        Should.NotThrow(() => _service.ValidateQuotationItemStockQty(item, 5, 2));

        // Reducing to 4 boxes with conversion factor 2 = 8 units (< ordered 10) -> throws
        var ex = Should.Throw<BusinessException>(() => _service.ValidateQuotationItemStockQty(item, 4, 2));
        ex.Code.ShouldBe(MyERPDomainErrorCodes.ValidationFailed);
    }

    [Fact]
    public void ValidateQuotationItemDeletion_ThrowsWhenOrdered()
    {
        var item = new QuotationItem(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Item", 10, 100, 0, "Unit")
        {
            OrderedQty = 2
        };

        var ex = Should.Throw<BusinessException>(() => _service.ValidateQuotationItemDeletion(item));
        ex.Code.ShouldBe(MyERPDomainErrorCodes.ValidationFailed);
    }

    [Fact]
    public void ValidateSupplierQuotationItemDeletion_ThrowsWhenOrdered()
    {
        var item = new SupplierQuotationItem(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 10, 100, "Item", "Unit")
        {
            OrderedQty = 3
        };

        var ex = Should.Throw<BusinessException>(() => _service.ValidateSupplierQuotationItemDeletion(item));
        ex.Code.ShouldBe(MyERPDomainErrorCodes.ValidationFailed);
    }

    [Fact]
    public void ValidateSupplierQuotationItemUpdate_RateChange_BlockedWhenOrdered()
    {
        var item = new SupplierQuotationItem(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 10, 100, "Item", "Unit")
        {
            OrderedQty = 4
        };

        // Same rate succeeds
        Should.NotThrow(() => _service.ValidateSupplierQuotationItemUpdate(item, 8, 100));

        // Rate change throws
        var ex = Should.Throw<BusinessException>(() => _service.ValidateSupplierQuotationItemUpdate(item, 10, 110));
        ex.Code.ShouldBe(MyERPDomainErrorCodes.ValidationFailed);
    }

    [Fact]
    public void ValidateSupplierQuotationItemStockQty_WithConversionFactor_ComparesInStockUom()
    {
        // 6 boxes with conversion factor 5 = 30 in stock UOM
        var item = new SupplierQuotationItem(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 6, 10, "Item", "Box")
        {
            ConversionFactor = 5,
            OrderedQty = 10 // 10 units ordered in stock UOM
        };

        // Reducing to 5 boxes with conversion factor 2 = 10 units (equal to ordered 10) -> succeeds
        Should.NotThrow(() => _service.ValidateSupplierQuotationItemStockQty(item, 5, 2));

        // Reducing to 4 boxes with conversion factor 2 = 8 units (< ordered 10) -> throws
        var ex = Should.Throw<BusinessException>(() => _service.ValidateSupplierQuotationItemStockQty(item, 4, 2));
        ex.Code.ShouldBe(MyERPDomainErrorCodes.ValidationFailed);
    }

    // =========================================================================
    // ERPNext PR #59832 (commit 008fb0c12e): Order status and UOM/conversion locks
    // =========================================================================

    [Theory]
    [InlineData(DocumentStatus.Draft)]
    [InlineData(DocumentStatus.Cancelled)]
    public void ValidateOrderStatusForItemUpdate_DraftOrCancelled_ThrowsInvalidStatusTransition(DocumentStatus status)
    {
        var ex = Should.Throw<BusinessException>(() => _service.ValidateOrderStatusForItemUpdate(status, "Purchase Order", "PO-001"));
        ex.Code.ShouldBe(MyERPDomainErrorCodes.InvalidStatusTransition);
        ex.Data["detail"]!.ToString()!.ShouldContain("Only submitted orders can have items updated");
    }

    [Theory]
    [InlineData(DocumentStatus.Closed)]
    public void ValidateOrderStatusForItemUpdate_Closed_ThrowsInvalidStatusTransition(DocumentStatus status)
    {
        var ex = Should.Throw<BusinessException>(() => _service.ValidateOrderStatusForItemUpdate(status, "Sales Order", "SO-001"));
        ex.Code.ShouldBe(MyERPDomainErrorCodes.InvalidStatusTransition);
        ex.Data["detail"]!.ToString()!.ShouldContain("Cannot update items on Sales Order SO-001");
    }

    [Theory]
    [InlineData(DocumentStatus.ToDeliverAndBill)]
    [InlineData(DocumentStatus.ToBill)]
    [InlineData(DocumentStatus.ToDeliver)]
    [InlineData(DocumentStatus.Completed)]
    public void ValidateOrderStatusForItemUpdate_ActiveSubmittedStatus_Succeeds(DocumentStatus status)
    {
        Should.NotThrow(() => _service.ValidateOrderStatusForItemUpdate(status, "Purchase Order", "PO-001"));
    }

    [Fact]
    public void ValidateUomUnchangedAfterFulfilment_FulfilledItem_ChangesToUomOrConversionFactor_Throws()
    {
        // Changing UOM after part of it received
        var ex1 = Should.Throw<BusinessException>(() =>
            _service.ValidateUomUnchangedAfterFulfilment("Steel Rod", 5m, "Box", "Kg", 10m, null));
        ex1.Code.ShouldBe(MyERPDomainErrorCodes.ValidationFailed);
        ex1.Data["detail"]!.ToString()!.ShouldContain("Cannot change the UOM or conversion factor of Item 'Steel Rod'");

        // Changing conversion factor after part of it received
        var ex2 = Should.Throw<BusinessException>(() =>
            _service.ValidateUomUnchangedAfterFulfilment("Steel Rod", 5m, "Box", "Box", 10m, 12m));
        ex2.Code.ShouldBe(MyERPDomainErrorCodes.ValidationFailed);
        ex2.Data["detail"]!.ToString()!.ShouldContain("Cannot change the UOM or conversion factor of Item 'Steel Rod'");
    }

    [Fact]
    public void ValidateUomUnchangedAfterFulfilment_UnfulfilledItem_AllowsChanges()
    {
        // Not fulfilled yet (fulfilledQty = 0) -> change allowed
        Should.NotThrow(() =>
            _service.ValidateUomUnchangedAfterFulfilment("Steel Rod", 0m, "Box", "Kg", 10m, 5m));
    }

    [Fact]
    public void ValidateUomUnchangedAfterFulfilment_FulfilledItem_SameUomAndFactor_Succeeds()
    {
        // Fulfilled, but UOM and conversion factor remain unchanged
        Should.NotThrow(() =>
            _service.ValidateUomUnchangedAfterFulfilment("Steel Rod", 5m, "Box", "Box", 10m, 10m));
        Should.NotThrow(() =>
            _service.ValidateUomUnchangedAfterFulfilment("Steel Rod", 5m, "Box", null, 10m, null));
    }

    // =========================================================================
    // ERPNext PR #61634 / #61640 / #61581 / #61472 / #61495
    // =========================================================================

    [Fact]
    public void ValidateSalesOrderItem_ReservedStock_BlocksDeleteUomChangeAndUnderReduction()
    {
        var item = new SalesOrderItem(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Widget", 10, 50, 0, "Box")
        {
            ConversionFactor = 2,
            DeliveredQty = 2 // 4 stock units delivered
        };

        var delEx = Should.Throw<BusinessException>(() => _service.ValidateSalesOrderItemDeletion(item, hasKeptStockReservation: true));
        delEx.Data["detail"]!.ToString()!.ShouldContain("Unreserve stock before deleting");

        var uomEx = Should.Throw<BusinessException>(() =>
            _service.ValidateUomUnchangedAfterFulfilment("Widget", 0m, "Box", "Pack", 2m, 2m, hasKeptStockReservation: true));
        uomEx.Data["detail"]!.ToString()!.ShouldContain("while stock is reserved");

        // 4 stock units delivered + 6 stock units reserved = 10 stock units minimum (5 Boxes)
        Should.NotThrow(() => _service.ValidateSalesOrderItemStockQty(item, 5m, 2m, stockReservedQty: 6m));
        var qtyEx = Should.Throw<BusinessException>(() => _service.ValidateSalesOrderItemStockQty(item, 4m, 2m, stockReservedQty: 6m));
        qtyEx.Data["detail"]!.ToString()!.ShouldContain("delivered and reserved quantity");
    }

    [Fact]
    public void PurchaseOrderItem_GetMaxReceivableQty_FloorsWholeNumberUom()
    {
        var poItem = new PurchaseOrderItem(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Bolt", 3m, 10m, 0m, "Nos");
        poItem.GetMaxReceivableQty(50m, mustBeWholeNumber: false).ShouldBe(4.5m);
        poItem.GetMaxReceivableQty(50m, mustBeWholeNumber: true).ShouldBe(4m);
    }

    [Fact]
    public void Opportunity_DeclareLostAndReopen_TracksLostOnDate()
    {
        var opp = new MyERP.CRM.Entities.Opportunity(Guid.NewGuid(), Guid.NewGuid(), "OPP-001", "Deal");
        opp.DeclareLost("Price too high");
        opp.LostOn.ShouldBe(DateTime.UtcNow.Date);
        opp.Reopen();
        opp.LostOn.ShouldBeNull();
    }

    [Fact]
    public void ServiceLevelAgreement_GetWorkingHoursBetween_CountsOnlyServiceWindowHours()
    {
        var slaId = Guid.NewGuid();
        var sla = new MyERP.Support.Entities.ServiceLevelAgreement(slaId, Guid.NewGuid(), "SLA-Std", 8, 4);
        // Monday 09:00 - 17:00
        sla.AddServiceDay(new MyERP.Support.Entities.ServiceDay(Guid.NewGuid(), slaId, DayOfWeek.Monday, TimeSpan.FromHours(9), TimeSpan.FromHours(17)));

        var monday8am = new DateTime(2026, 10, 19, 8, 0, 0, DateTimeKind.Utc); // Monday
        var monday12pm = new DateTime(2026, 10, 19, 12, 0, 0, DateTimeKind.Utc);
        sla.GetWorkingHoursBetween(monday8am, monday12pm).ShouldBe(3m);
    }

    // =========================================================================
    // ERPNext PR #60338 / #60784 / #60187 / #59723 / #61773 / #61970 / #61806
    // =========================================================================

    [Fact]
    public void HolidayList_AddHoliday_RejectsDuplicateDate()
    {
        var list = new MyERP.HumanResources.Entities.HolidayList(Guid.NewGuid(), Guid.NewGuid(), "2026 Holidays", 2026);
        var date = new DateTime(2026, 5, 1);
        list.AddHoliday(new MyERP.HumanResources.Entities.Holiday(Guid.NewGuid(), list.Id, date, "Labour Day"));

        var ex = Should.Throw<BusinessException>(() =>
            list.AddHoliday(new MyERP.HumanResources.Entities.Holiday(Guid.NewGuid(), list.Id, date, "Duplicate Day")));
        ex.Data["detail"]!.ToString()!.ShouldContain("added multiple times");
    }

    [Fact]
    public void AuthorizationRule_And_ItemDiscountInfo_EnforceDiscountGuards()
    {
        var itemRule = new MyERP.Core.Entities.AuthorizationRule(
            Guid.NewGuid(), "SalesOrder", MyERP.Core.AuthorizationBasedOn.ItemwiseDiscount, 120m)
        {
            ApprovingRole = "Sales Manager"
        };
        Should.Throw<BusinessException>(() => itemRule.Validate()).Code.ShouldBe("MyERP:01015");

        var poDiscountRule = new MyERP.Core.Entities.AuthorizationRule(
            Guid.NewGuid(), "PurchaseOrder", MyERP.Core.AuthorizationBasedOn.AverageDiscount, 10m)
        {
            ApprovingRole = "Purchase Manager"
        };
        var poEx = Should.Throw<BusinessException>(() => poDiscountRule.Validate());
        poEx.Data["detail"]!.ToString()!.ShouldContain("Cannot set authorization on basis of Discount");

        var discountInfo = new MyERP.Core.DomainServices.ItemDiscountInfo
        {
            ItemId = Guid.NewGuid(),
            DiscountPercentage = 0m,
            PriceListRate = 100m,
            Rate = 75m
        };
        discountInfo.EffectiveDiscountPercentage.ShouldBe(25m);
    }

    [Fact]
    public void JobCard_StartTimeLog_And_ProductionPlanItem_ProcessLossGuards()
    {
        var jc = new MyERP.Manufacturing.Entities.JobCard(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 10m, 1);
        jc.StartTimeLog(new DateTime(2026, 10, 20, 9, 0, 0, DateTimeKind.Utc));

        var ex = Should.Throw<BusinessException>(() =>
            jc.StartTimeLog(new DateTime(2026, 10, 20, 9, 30, 0, DateTimeKind.Utc)));
        ex.Data["detail"]!.ToString()!.ShouldContain("already has an open time log");

        var ppi = new MyERP.Manufacturing.Entities.ProductionPlanItem(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "FG-01", Guid.NewGuid(), 100m)
        {
            ProducedQty = 80m,
            ProcessLossQty = 5m
        };
        ppi.PendingProductionQty.ShouldBe(15m);
    }

    [Fact]
    public void SalesInvoiceManager_ValidateDnRequired_ExemptsDropShipSoItems_And_DnItemDeductsReturnedQty()
    {
        var soItemId = Guid.NewGuid();
        var si = new MyERP.Sales.Entities.SalesInvoice(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "SI-001", DateTime.UtcNow.Date);
        si.AddItem(Guid.NewGuid(), "DropShip Widget", 2m, 100m, 0m);
        si.Items[0].SalesOrderItemId = soItemId;

        Should.Throw<BusinessException>(() =>
            MyERP.Sales.DomainServices.SalesInvoiceManager.ValidateDnRequired(si, dnRequired: true));

        Should.NotThrow(() =>
            MyERP.Sales.DomainServices.SalesInvoiceManager.ValidateDnRequired(
                si, dnRequired: true, new System.Collections.Generic.HashSet<Guid> { soItemId }));

        var dnItem = new MyERP.Sales.Entities.DeliveryNoteItem(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Pump", 10m, 100m, 0m)
        {
            ReturnedQty = 3m,
            InstalledQty = 4m
        };
        dnItem.PendingInstallationQty.ShouldBe(3m);
    }
}
