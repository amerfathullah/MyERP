using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.CRM;
using MyERP.CRM.Entities;
using MyERP.Dtos;
using MyERP.Inventory;
using MyERP.Inventory.Entities;
using MyERP.Manufacturing;
using MyERP.Manufacturing.Entities;
using MyERP.Sales;
using MyERP.Sales.Entities;
using NSubstitute;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace MyERP.Domain.Tests.Manufacturing;

public class UpstreamPR60161_60279_60283Tests
{
    // =========================================================================
    // ERPNext PR #60161 / commit 1334acafe2 & 7ecdd4a02c:
    // Respect unreserved work order components in reservation status
    // =========================================================================

    [Fact]
    public void WorkOrder_ReservationStatus_UnreservedComponents_ReturnsStockPartiallyReserved()
    {
        var wo = new WorkOrder(Guid.NewGuid(), Guid.NewGuid(), "WO-001", Guid.NewGuid(), Guid.NewGuid(), 10m)
        {
            ReserveStock = true
        };

        var item1 = new WorkOrderItem(Guid.NewGuid(), wo.Id, Guid.NewGuid(), "RM 1", 10m);
        item1.SetStockReservedQty(10m);
        var item2 = new WorkOrderItem(Guid.NewGuid(), wo.Id, Guid.NewGuid(), "RM 2", 10m);
        item2.SetStockReservedQty(0m);

        wo.RequiredItems.Add(item1);
        wo.RequiredItems.Add(item2);

        // Per commit 1334acafe2: if any required item has StockReservedQty < RequiredQuantity, StockPartiallyReserved
        var status = wo.GetReservationStatus();
        status.ShouldBe(WorkOrderStatus.StockPartiallyReserved);
    }

    [Fact]
    public void WorkOrder_ReservationStatus_PartialQtyReserved_ReturnsStockPartiallyReserved()
    {
        var wo = new WorkOrder(Guid.NewGuid(), Guid.NewGuid(), "WO-002", Guid.NewGuid(), Guid.NewGuid(), 10m)
        {
            ReserveStock = true
        };

        var item1 = new WorkOrderItem(Guid.NewGuid(), wo.Id, Guid.NewGuid(), "RM 1", 10m);
        item1.SetStockReservedQty(5m);
        var item2 = new WorkOrderItem(Guid.NewGuid(), wo.Id, Guid.NewGuid(), "RM 2", 10m);
        item2.SetStockReservedQty(10m);

        wo.RequiredItems.Add(item1);
        wo.RequiredItems.Add(item2);

        var status = wo.GetReservationStatus();
        status.ShouldBe(WorkOrderStatus.StockPartiallyReserved);
    }

    [Fact]
    public void WorkOrder_ReservationStatus_AllComponentsReserved_ReturnsStockReserved()
    {
        var wo = new WorkOrder(Guid.NewGuid(), Guid.NewGuid(), "WO-003", Guid.NewGuid(), Guid.NewGuid(), 10m)
        {
            ReserveStock = true
        };

        var item1 = new WorkOrderItem(Guid.NewGuid(), wo.Id, Guid.NewGuid(), "RM 1", 10m);
        item1.SetStockReservedQty(10m);
        var item2 = new WorkOrderItem(Guid.NewGuid(), wo.Id, Guid.NewGuid(), "RM 2", 10m);
        item2.SetStockReservedQty(12m); // Over-reserved or exact is considered fully reserved

        wo.RequiredItems.Add(item1);
        wo.RequiredItems.Add(item2);

        var status = wo.GetReservationStatus();
        status.ShouldBe(WorkOrderStatus.StockReserved);
    }

    [Fact]
    public void WorkOrder_ReservationStatus_NoReservations_ReturnsFallbackStatus()
    {
        var wo = new WorkOrder(Guid.NewGuid(), Guid.NewGuid(), "WO-004", Guid.NewGuid(), Guid.NewGuid(), 10m)
        {
            ReserveStock = true
        };

        var item1 = new WorkOrderItem(Guid.NewGuid(), wo.Id, Guid.NewGuid(), "RM 1", 10m);
        var item2 = new WorkOrderItem(Guid.NewGuid(), wo.Id, Guid.NewGuid(), "RM 2", 10m);
        wo.RequiredItems.Add(item1);
        wo.RequiredItems.Add(item2);

        var status = wo.GetReservationStatus(WorkOrderStatus.NotStarted);
        status.ShouldBe(WorkOrderStatus.NotStarted);
    }

    [Fact]
    public void WorkOrder_Submit_UpdatesReservationStatusWhenReserveStockEnabled()
    {
        var wo = new WorkOrder(Guid.NewGuid(), Guid.NewGuid(), "WO-005", Guid.NewGuid(), Guid.NewGuid(), 10m)
        {
            ReserveStock = true
        };

        var item1 = new WorkOrderItem(Guid.NewGuid(), wo.Id, Guid.NewGuid(), "RM 1", 10m);
        item1.SetStockReservedQty(10m);
        var item2 = new WorkOrderItem(Guid.NewGuid(), wo.Id, Guid.NewGuid(), "RM 2", 10m);
        item2.SetStockReservedQty(10m);
        wo.RequiredItems.Add(item1);
        wo.RequiredItems.Add(item2);

        wo.Submit();
        wo.Status.ShouldBe(WorkOrderStatus.StockReserved);
    }

    [Fact]
    public void WorkOrder_Start_AllowsTransitionsFromReservedStatuses()
    {
        var wo1 = new WorkOrder(Guid.NewGuid(), Guid.NewGuid(), "WO-006", Guid.NewGuid(), Guid.NewGuid(), 10m) { ReserveStock = true };
        var item1 = new WorkOrderItem(Guid.NewGuid(), wo1.Id, Guid.NewGuid(), "RM 1", 10m);
        item1.SetStockReservedQty(10m);
        wo1.RequiredItems.Add(item1);
        wo1.Submit();
        wo1.Status.ShouldBe(WorkOrderStatus.StockReserved);
        wo1.Start();
        wo1.Status.ShouldBe(WorkOrderStatus.InProcess);

        var wo2 = new WorkOrder(Guid.NewGuid(), Guid.NewGuid(), "WO-007", Guid.NewGuid(), Guid.NewGuid(), 10m) { ReserveStock = true };
        var item2 = new WorkOrderItem(Guid.NewGuid(), wo2.Id, Guid.NewGuid(), "RM 1", 10m);
        item2.SetStockReservedQty(5m);
        wo2.RequiredItems.Add(item2);
        wo2.Submit();
        wo2.Status.ShouldBe(WorkOrderStatus.StockPartiallyReserved);
        wo2.Start();
        wo2.Status.ShouldBe(WorkOrderStatus.InProcess);
    }

    [Fact]
    public void WorkOrder_RecordMaterialTransfer_ResetsFromReservedToNotStarted()
    {
        var wo = new WorkOrder(Guid.NewGuid(), Guid.NewGuid(), "WO-008", Guid.NewGuid(), Guid.NewGuid(), 10m) { ReserveStock = true };
        var item = new WorkOrderItem(Guid.NewGuid(), wo.Id, Guid.NewGuid(), "RM 1", 10m);
        item.SetStockReservedQty(10m);
        wo.RequiredItems.Add(item);
        wo.Submit();
        wo.Status.ShouldBe(WorkOrderStatus.StockReserved);

        wo.RecordMaterialTransfer(5m);
        wo.Status.ShouldBe(WorkOrderStatus.NotStarted);
    }

    // =========================================================================
    // ERPNext PR #60283 / commit 14baff2a3d:
    // Default quality inspection sample size to one
    // =========================================================================

    [Fact]
    public void QualityInspection_DefaultSampleSize_IsOne()
    {
        var qi = new QualityInspection(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), InspectionType.Incoming, DateTime.UtcNow);
        qi.SampleSize.ShouldBe(1m);

        var dto = new CreateQualityInspectionDto();
        dto.SampleSize.ShouldBe(1m);
    }

    // =========================================================================
    // ERPNext PR #60318 / commit 56d058f26c:
    // Validate item UOM conversion factor is strictly positive in constructor
    // =========================================================================

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-0.001)]
    public void UomConversion_Constructor_ThrowsWhenNonPositive(decimal factor)
    {
        var ex = Should.Throw<BusinessException>(() => new UomConversion(Guid.NewGuid(), "Box", "Unit", factor));
        ex.Code.ShouldBe(MyERPDomainErrorCodes.ValidationFailed);
        (ex.Data["detail"]?.ToString() ?? string.Empty).ShouldContain("greater than zero");
    }

    [Fact]
    public void UomConversion_Constructor_SucceedsWhenPositive()
    {
        var conv = new UomConversion(Guid.NewGuid(), "Box", "Unit", 12m);
        conv.ConversionFactor.ShouldBe(12m);
    }

    // =========================================================================
    // ERPNext PR #60279 / commit 9f9cf26639:
    // Link lead opportunities to customer on creation, unlink on customer delete
    // =========================================================================

    [Fact]
    public void Opportunity_CanBeLinkedToCustomer()
    {
        var opp = new Opportunity(Guid.NewGuid(), Guid.NewGuid(), "OPP-001", "Deal 1");
        var leadId = Guid.NewGuid();
        opp.LeadId = leadId;
        opp.CustomerId.ShouldBeNull();

        var customerId = Guid.NewGuid();
        opp.CustomerId = customerId;
        opp.CustomerId.ShouldBe(customerId);

        // Revert on customer delete
        opp.CustomerId = null;
        opp.CustomerId.ShouldBeNull();
        opp.LeadId.ShouldBe(leadId);
    }
}
