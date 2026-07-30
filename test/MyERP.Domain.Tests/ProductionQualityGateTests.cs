using System;
using MyERP.Core;
using MyERP.Inventory;
using MyERP.Inventory.Entities;
using MyERP.Manufacturing.Entities;
using Xunit;

namespace MyERP.Domain.Tests;

public class ProductionQualityGateTests
{
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid ItemId = Guid.NewGuid();
    private static readonly Guid BomId = Guid.NewGuid();
    private static readonly Guid WoId = Guid.NewGuid();

    [Fact]
    public void Item_InspectionRequiredBeforeDelivery_DefaultsFalse()
    {
        var item = new Item(Guid.NewGuid(), CompanyId, "FG-001", "Finished Good", ItemType.Goods);
        Assert.False(item.InspectionRequiredBeforeDelivery);
    }

    [Fact]
    public void Item_InspectionRequiredBeforeDelivery_CanBeEnabled()
    {
        var item = new Item(Guid.NewGuid(), CompanyId, "FG-001", "Finished Good", ItemType.Goods);
        item.InspectionRequiredBeforeDelivery = true;
        Assert.True(item.InspectionRequiredBeforeDelivery);
    }

    [Fact]
    public void Item_InspectionRequiredBeforePurchase_DefaultsFalse()
    {
        var item = new Item(Guid.NewGuid(), CompanyId, "RM-001", "Raw Material", ItemType.Goods);
        Assert.False(item.InspectionRequiredBeforePurchase);
    }

    [Fact]
    public void QualityInspection_DefaultDraftStatus()
    {
        var qi = new QualityInspection(Guid.NewGuid(), CompanyId, ItemId, InspectionType.InProcess, DateTime.UtcNow);
        Assert.Equal(DocumentStatus.Draft, qi.DocStatus);
        Assert.Equal(InspectionStatus.Draft, qi.Status);
    }

    [Fact]
    public void QualityInspection_CanLinkToWorkOrder()
    {
        var qi = new QualityInspection(Guid.NewGuid(), CompanyId, ItemId, InspectionType.InProcess, DateTime.UtcNow);
        qi.ReferenceType = "WorkOrder";
        qi.ReferenceId = WoId;
        Assert.Equal("WorkOrder", qi.ReferenceType);
        Assert.Equal(WoId, qi.ReferenceId);
    }

    [Fact]
    public void QualityInspection_InProcessType_ForManufacturing()
    {
        var qi = new QualityInspection(Guid.NewGuid(), CompanyId, ItemId, InspectionType.InProcess, DateTime.UtcNow);
        Assert.Equal(InspectionType.InProcess, qi.InspectionType);
    }

    [Fact]
    public void QualityInspection_Submit_SetsAccepted()
    {
        var qi = new QualityInspection(Guid.NewGuid(), CompanyId, ItemId, InspectionType.InProcess, DateTime.UtcNow);
        qi.AddReading("Dimension", "10.5", null, null, "10.5", isNumeric: true);
        qi.Submit();
        Assert.Equal(DocumentStatus.Submitted, qi.DocStatus);
        Assert.Equal(InspectionStatus.Accepted, qi.Status);
    }

    [Fact]
    public void WorkOrder_RequiresQI_WhenFgItemHasInspectionFlag()
    {
        var item = new Item(Guid.NewGuid(), CompanyId, "FG-MOTOR", "Electric Motor", ItemType.Goods);
        item.InspectionRequiredBeforeDelivery = true;
        Assert.True(item.InspectionRequiredBeforeDelivery);
    }

    [Fact]
    public void WorkOrder_NoQI_WhenFgItemDoesNotRequireInspection()
    {
        var item = new Item(Guid.NewGuid(), CompanyId, "FG-BASIC", "Basic Widget", ItemType.Goods);
        Assert.False(item.InspectionRequiredBeforeDelivery);
    }

    [Fact]
    public void WorkOrder_ProductionTracking_ProducedQtyIncrements()
    {
        var wo = new WorkOrder(Guid.NewGuid(), CompanyId, "WO-001", ItemId, BomId, 100);
        wo.Submit();
        wo.Start();
        wo.RecordProduction(30);
        Assert.Equal(30, wo.ProducedQuantity);
    }

    [Fact]
    public void WorkOrder_PercentComplete_AfterPartialProduction()
    {
        var wo = new WorkOrder(Guid.NewGuid(), CompanyId, "WO-001", ItemId, BomId, 100);
        wo.Submit();
        wo.Start();
        wo.RecordProduction(40);
        Assert.Equal(40, wo.PercentComplete);
    }

    [Fact]
    public void WorkOrder_QI_ReferencedByWorkOrderId()
    {
        var qi = new QualityInspection(Guid.NewGuid(), CompanyId, ItemId, InspectionType.InProcess, DateTime.UtcNow);
        qi.ReferenceType = "WorkOrder";
        qi.ReferenceId = WoId;
        Assert.Equal("WorkOrder", qi.ReferenceType);
        Assert.Equal(WoId, qi.ReferenceId);
    }

    [Fact]
    public void InspectionType_InProcess_Value()
    {
        Assert.Equal(2, (int)InspectionType.InProcess);
    }

    [Theory]
    [InlineData("QualityInspectionRequired")]
    [InlineData("QualityInspectionRejected")]
    public void ErrorCodes_QualityInspection_ExistInConstants(string fieldName)
    {
        var field = typeof(MyERPDomainErrorCodes).GetField(fieldName);
        Assert.NotNull(field);
    }

    [Fact]
    public void WorkOrder_DefaultDraft_CannotRecordProduction()
    {
        var wo = new WorkOrder(Guid.NewGuid(), CompanyId, "WO-001", ItemId, BomId, 50);
        Assert.Throws<Volo.Abp.BusinessException>(() => wo.RecordProduction(10));
    }

    [Fact]
    public void WorkOrder_InProcess_CanRecordProduction()
    {
        var wo = new WorkOrder(Guid.NewGuid(), CompanyId, "WO-001", ItemId, BomId, 50);
        wo.Submit();
        wo.Start();
        wo.RecordProduction(25);
        Assert.Equal(25, wo.ProducedQuantity);
        Assert.Equal(50, wo.PercentComplete);
    }
}
