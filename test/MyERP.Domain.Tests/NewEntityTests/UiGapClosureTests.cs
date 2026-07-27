using System;
using MyERP.Inventory.Entities;
using MyERP.Manufacturing;
using MyERP.Manufacturing.Entities;
using MyERP.Sales.Entities;
using MyERP.Workflow;
using MyERP.Workflow.Entities;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace MyERP.NewEntityTests;

/// <summary>
/// Tests for BOM detail view data, Serial No/Batch detail data,
/// Shipment form workflow, and approval request lifecycle —
/// covering UI gaps closed in 2026-07-24 session.
/// </summary>
public class UiGapClosureTests
{
    #region BOM Detail View Data

    [Fact]
    public void BomDetail_MaterialsAndOperations_Combined()
    {
        var bom = new BillOfMaterials(Guid.NewGuid(), Guid.NewGuid(), "BOM-001", Guid.NewGuid());
        bom.Items.Add(new BomItem(Guid.NewGuid(), bom.Id, Guid.NewGuid(), "Raw Material A", 10, 5.50m));
        bom.Items.Add(new BomItem(Guid.NewGuid(), bom.Id, Guid.NewGuid(), "Raw Material B", 5, 12.00m));
        bom.RecalculateCost();

        bom.Items.Count.ShouldBe(2);
        bom.TotalMaterialCost.ShouldBe(115m);
    }

    [Fact]
    public void BomDetail_SecondaryItems_Display()
    {
        var bom = new BillOfMaterials(Guid.NewGuid(), Guid.NewGuid(), "BOM-002", Guid.NewGuid());
        var si = new BomSecondaryItem(Guid.NewGuid(), bom.Id, Guid.NewGuid(),
            SecondaryItemType.ByProduct, 2);
        si.CostAllocationPercentage = 10;
        bom.AddSecondaryItem(si);
        bom.SecondaryItems.Count.ShouldBe(1);
        bom.SecondaryItems[0].SecondaryItemType.ShouldBe(SecondaryItemType.ByProduct);
        bom.SecondaryItems[0].CostAllocationPercentage.ShouldBe(10m);
    }

    [Fact]
    public void BomDetail_Operations_WithCost()
    {
        var bom = new BillOfMaterials(Guid.NewGuid(), Guid.NewGuid(), "BOM-003", Guid.NewGuid());
        var op = new BomOperation(Guid.NewGuid(), bom.Id, Guid.NewGuid(), 10, 60m, Guid.NewGuid());
        op.CalculateCost(50m); // 60min / 60 × 50 = 50
        op.OperatingCost.ShouldBe(50m);
    }

    [Fact]
    public void BomDetail_PhantomItem_Flag()
    {
        var bom = new BillOfMaterials(Guid.NewGuid(), Guid.NewGuid(), "BOM-004", Guid.NewGuid());
        var item = new BomItem(Guid.NewGuid(), bom.Id, Guid.NewGuid(), "Phantom Sub", 1, 100m);
        item.IsPhantom.ShouldBeFalse(); // default
        item.IsPhantom = true;
        item.IsPhantom.ShouldBeTrue();
    }

    [Fact]
    public void BomDetail_SubBom_Link()
    {
        var bom = new BillOfMaterials(Guid.NewGuid(), Guid.NewGuid(), "BOM-005", Guid.NewGuid());
        var item = new BomItem(Guid.NewGuid(), bom.Id, Guid.NewGuid(), "Sub-Assembly", 1, 200m);
        item.SubBomId.ShouldBeNull(); // default
        var subBomId = Guid.NewGuid();
        item.SubBomId = subBomId;
        item.SubBomId.ShouldBe(subBomId);
    }

    #endregion

    #region Serial No Detail Data

    [Fact]
    public void SerialNo_MaintenanceStatus_SetWarranty()
    {
        var serial = new SerialNo(Guid.NewGuid(), Guid.NewGuid(), "SN-001", Guid.NewGuid());
        serial.WarrantyExpiryDate = DateTime.UtcNow.AddDays(30);
        serial.UpdateMaintenanceStatus();
        serial.MaintenanceStatus.ShouldBe("Under Warranty");
    }

    [Fact]
    public void SerialNo_MaintenanceStatus_Expired()
    {
        var serial = new SerialNo(Guid.NewGuid(), Guid.NewGuid(), "SN-002", Guid.NewGuid());
        serial.WarrantyExpiryDate = DateTime.UtcNow.AddDays(-1);
        serial.UpdateMaintenanceStatus();
        serial.MaintenanceStatus.ShouldBe("Out of Warranty");
    }

    [Fact]
    public void SerialNo_AmcExpiry_Tracked()
    {
        var serial = new SerialNo(Guid.NewGuid(), Guid.NewGuid(), "SN-003", Guid.NewGuid());
        serial.AmcExpiryDate.ShouldBeNull(); // default
        serial.AmcExpiryDate = DateTime.UtcNow.AddMonths(6);
        serial.AmcExpiryDate.ShouldNotBeNull();
    }

    [Fact]
    public void SerialNo_PurchaseRate_Settable()
    {
        var serial = new SerialNo(Guid.NewGuid(), Guid.NewGuid(), "SN-004", Guid.NewGuid());
        serial.PurchaseRate = 1500m;
        serial.PurchaseRate.ShouldBe(1500m);
    }

    #endregion

    #region Batch Detail Data

    [Fact]
    public void Batch_ExpiryDate_NotYetExpired()
    {
        var batch = new Batch(Guid.NewGuid(), Guid.NewGuid(), "BATCH-001");
        batch.ExpiryDate = DateTime.UtcNow.AddDays(45);
        batch.IsExpired().ShouldBeFalse();
    }

    [Fact]
    public void Batch_Expired_Detection()
    {
        var batch = new Batch(Guid.NewGuid(), Guid.NewGuid(), "BATCH-002");
        batch.ExpiryDate = DateTime.UtcNow.AddDays(-5);
        batch.IsExpired().ShouldBeTrue();
    }

    [Fact]
    public void Batch_NoExpiry_NeverExpires()
    {
        var batch = new Batch(Guid.NewGuid(), Guid.NewGuid(), "BATCH-003");
        batch.ExpiryDate = null;
        batch.IsExpired().ShouldBeFalse();
    }

    [Fact]
    public void Batch_ShelfLife_Property()
    {
        var batch = new Batch(Guid.NewGuid(), Guid.NewGuid(), "BATCH-004");
        batch.ManufacturingDate = DateTime.UtcNow.AddDays(-10);
        batch.ShelfLifeInDays = 90;
        batch.ManufacturingDate.HasValue.ShouldBeTrue();
        batch.ShelfLifeInDays.ShouldBe(90);
    }

    [Fact]
    public void Batch_SupplierBatchNo_Optional()
    {
        var batch = new Batch(Guid.NewGuid(), Guid.NewGuid(), "BATCH-005");
        batch.SupplierBatchNo.ShouldBeNull();
        batch.SupplierBatchNo = "VENDOR-LOT-789";
        batch.SupplierBatchNo.ShouldBe("VENDOR-LOT-789");
    }

    [Fact]
    public void Batch_Disable_Lifecycle()
    {
        var batch = new Batch(Guid.NewGuid(), Guid.NewGuid(), "BATCH-006");
        batch.IsDisabled.ShouldBeFalse();
        batch.IsDisabled = true;
        batch.IsDisabled.ShouldBeTrue();
    }

    #endregion

    #region Shipment Form Workflow

    [Fact]
    public void Shipment_CarrierFields_Settable()
    {
        var shipment = new Shipment(Guid.NewGuid(), Guid.NewGuid(), "SHP-FORM-01");
        shipment.Carrier = "DHL Express";
        shipment.CarrierService = "Express Worldwide";
        shipment.Carrier.ShouldBe("DHL Express");
        shipment.CarrierService.ShouldBe("Express Worldwide");
    }

    [Fact]
    public void Shipment_WeightFields_Settable()
    {
        var shipment = new Shipment(Guid.NewGuid(), Guid.NewGuid(), "SHP-FORM-02");
        shipment.TotalNetWeight = 25.5m;
        shipment.TotalGrossWeight = 30.0m;
        shipment.WeightUom = "Kg";
        shipment.TotalNetWeight.ShouldBe(25.5m);
        shipment.TotalGrossWeight.ShouldBe(30.0m);
        shipment.WeightUom.ShouldBe("Kg");
    }

    [Fact]
    public void Shipment_ValueAndCurrency_Settable()
    {
        var shipment = new Shipment(Guid.NewGuid(), Guid.NewGuid(), "SHP-FORM-03");
        shipment.ValueOfGoods = 15_000m;
        shipment.CurrencyCode = "MYR";
        shipment.ValueOfGoods.ShouldBe(15_000m);
        shipment.CurrencyCode.ShouldBe("MYR");
    }

    [Fact]
    public void Shipment_FullLifecycle_DraftToDelivered()
    {
        var shipment = new Shipment(Guid.NewGuid(), Guid.NewGuid(), "SHP-FORM-04");
        shipment.Carrier = "FedEx";
        shipment.AddDeliveryNote(Guid.NewGuid(), Guid.NewGuid(), "DN-100", 8_000m);

        shipment.Status.ShouldBe(ShipmentStatus.Draft);
        shipment.Submit();
        shipment.Status.ShouldBe(ShipmentStatus.Booked);
        shipment.MarkInTransit();
        shipment.Status.ShouldBe(ShipmentStatus.InTransit);
        shipment.MarkDelivered(DateTime.UtcNow);
        shipment.Status.ShouldBe(ShipmentStatus.Delivered);
        shipment.DeliveryDate.ShouldNotBeNull();
    }

    [Fact]
    public void Shipment_PickupDeliveryTypes_Settable()
    {
        var shipment = new Shipment(Guid.NewGuid(), Guid.NewGuid(), "SHP-FORM-05");
        shipment.PickupFromType = "Company";
        shipment.DeliveryToType = "Customer";
        shipment.PickupFromType.ShouldBe("Company");
        shipment.DeliveryToType.ShouldBe("Customer");
    }

    [Fact]
    public void Shipment_Notes_OptionalField()
    {
        var shipment = new Shipment(Guid.NewGuid(), Guid.NewGuid(), "SHP-FORM-06");
        shipment.Notes.ShouldBeNull();
        shipment.Notes = "Handle with care - fragile goods";
        shipment.Notes.ShouldContain("fragile");
    }

    #endregion

    #region Approval Workflow Data

    [Fact]
    public void ApprovalRequest_DefaultStatus_Pending()
    {
        var request = new ApprovalRequest(Guid.NewGuid(), Guid.NewGuid(),
            "SalesInvoice", Guid.NewGuid(), 1, Guid.NewGuid());
        request.Status.ShouldBe(ApprovalStatus.Pending);
    }

    [Fact]
    public void ApprovalRequest_Approve_ChangesStatus()
    {
        var request = new ApprovalRequest(Guid.NewGuid(), Guid.NewGuid(),
            "SalesInvoice", Guid.NewGuid(), 1, Guid.NewGuid());
        request.Approve(Guid.NewGuid(), "Approved - within budget");
        request.Status.ShouldBe(ApprovalStatus.Approved);
        request.Remarks.ShouldBe("Approved - within budget");
    }

    [Fact]
    public void ApprovalRequest_Reject_ChangesStatus()
    {
        var request = new ApprovalRequest(Guid.NewGuid(), Guid.NewGuid(),
            "PurchaseOrder", Guid.NewGuid(), 1, Guid.NewGuid());
        request.Reject(Guid.NewGuid(), "Amount exceeds policy");
        request.Status.ShouldBe(ApprovalStatus.Rejected);
        request.Remarks.ShouldBe("Amount exceeds policy");
    }

    [Fact]
    public void ApprovalRequest_DoubleApprove_Throws()
    {
        var request = new ApprovalRequest(Guid.NewGuid(), Guid.NewGuid(),
            "SalesOrder", Guid.NewGuid(), 1, Guid.NewGuid());
        request.Approve(Guid.NewGuid(), null);
        Should.Throw<BusinessException>(() => request.Approve(Guid.NewGuid(), null));
    }

    [Fact]
    public void ApprovalRule_MinimumAmount_Threshold()
    {
        var rule = new ApprovalRule(Guid.NewGuid(), "SalesInvoice", "High Value SI", 1);
        rule.MinimumAmount = 50_000m;
        rule.IsActive = true;
        rule.MinimumAmount.ShouldBe(50_000m);
        rule.IsActive.ShouldBeTrue();
    }

    #endregion
}
