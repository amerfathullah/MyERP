using System;
using System.Collections.Generic;
using MyERP.Core;
using MyERP.Inventory.DomainServices;
using MyERP.Inventory.Entities;
using MyERP.Manufacturing;
using MyERP.Manufacturing.Entities;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace MyERP.Inventory;

public class InventoryDomainGuardTests
{
    private readonly Guid _companyId = Guid.NewGuid();

    // =========================================================================
    // PR #60132 (commit 2b3fadb4e9): Shipment Parcel Template positive dimensions/weight
    // =========================================================================

    [Fact]
    public void ShipmentParcelTemplate_Should_Create_With_Positive_Dimensions_And_Weight()
    {
        var template = new ShipmentParcelTemplate(
            Guid.NewGuid(),
            "Standard Box",
            length: 30m,
            width: 20m,
            height: 15m,
            weight: 2.5m);

        template.Length.ShouldBe(30m);
        template.Width.ShouldBe(20m);
        template.Height.ShouldBe(15m);
        template.Weight.ShouldBe(2.5m);
    }

    [Theory]
    [InlineData(0, 10, 10, 5, "Length")]
    [InlineData(-5, 10, 10, 5, "Length")]
    [InlineData(10, 0, 10, 5, "Width")]
    [InlineData(10, 10, 0, 5, "Height")]
    [InlineData(10, 10, 10, 0, "Weight")]
    [InlineData(10, 10, 10, -1, "Weight")]
    public void ShipmentParcelTemplate_Should_Reject_NonPositive_Dimensions_Or_Weight(
        decimal length, decimal width, decimal height, decimal weight, string expectedField)
    {
        var ex = Should.Throw<BusinessException>(() =>
            new ShipmentParcelTemplate(
                Guid.NewGuid(),
                "Invalid Box",
                length,
                width,
                height,
                weight));

        ex.Code.ShouldBe(MyERPDomainErrorCodes.ParcelTemplateDimensionsMustBePositive);
        ex.Data["field"].ShouldBe(expectedField);
    }

    // =========================================================================
    // PR #60319 (commit 19c81cec44): Validate opening stock serial & batch settings
    // =========================================================================

    [Fact]
    public void ValidateOpeningStockSettings_Should_Reject_Serialized_Item_Without_SerialNoSeries()
    {
        var item = new Item(Guid.NewGuid(), _companyId, "ITEM-SER-01", "Serialized Item", ItemType.Goods)
        {
            MaintainStock = true,
            HasSerialNo = true,
            SerialNoSeries = null
        };

        var ex = Should.Throw<BusinessException>(() =>
            item.ValidateOpeningStockSettings(openingStock: 5m));

        ex.Code.ShouldBe(MyERPDomainErrorCodes.OpeningStockRequiresSerialNoSeries);
    }

    [Fact]
    public void ValidateOpeningStockSettings_Should_Allow_Serialized_Item_With_SerialNoSeries()
    {
        var item = new Item(Guid.NewGuid(), _companyId, "ITEM-SER-02", "Serialized Item", ItemType.Goods)
        {
            MaintainStock = true,
            HasSerialNo = true,
            SerialNoSeries = "SN-.#####"
        };

        Should.NotThrow(() => item.ValidateOpeningStockSettings(openingStock: 5m));
    }

    [Fact]
    public void ValidateOpeningStockSettings_Should_Reject_Batched_Item_Without_CreateNewBatch()
    {
        var item = new Item(Guid.NewGuid(), _companyId, "ITEM-BAT-01", "Batched Item", ItemType.Goods)
        {
            MaintainStock = true,
            HasBatchNo = true,
            CreateNewBatch = false
        };

        var ex = Should.Throw<BusinessException>(() =>
            item.ValidateOpeningStockSettings(openingStock: 10m));

        ex.Code.ShouldBe(MyERPDomainErrorCodes.OpeningStockRequiresCreateNewBatch);
    }

    [Fact]
    public void ValidateOpeningStockSettings_Should_Allow_Zero_OpeningStock()
    {
        var item = new Item(Guid.NewGuid(), _companyId, "ITEM-SER-BAT-01", "Tracked Item", ItemType.Goods)
        {
            MaintainStock = true,
            HasSerialNo = true,
            HasBatchNo = true,
            CreateNewBatch = false
        };

        Should.NotThrow(() => item.ValidateOpeningStockSettings(openingStock: 0m));
    }

    // =========================================================================
    // PR #60128 (commit ccfecedb81): Item Variant Settings copy fields & UOM preservation
    // =========================================================================

    [Theory]
    [InlineData("barcodes")]
    [InlineData("attributes")]
    [InlineData("has_variants")]
    [InlineData("variant_of")]
    [InlineData("Barcodes")]
    [InlineData("VariantAttributes")]
    public void ValidateCopyFields_Should_Reject_Invalid_Variant_Copy_Fields(string invalidField)
    {
        var service = new ItemVariantService(null!, null!);

        var ex = Should.Throw<BusinessException>(() =>
            service.ValidateCopyFields(new[] { "description", invalidField }));

        ex.Code.ShouldBe(MyERPDomainErrorCodes.InvalidVariantCopyField);
        ex.Data["field"].ShouldBe(invalidField);
    }

    [Fact]
    public void CopyAttributesToVariant_Should_Preserve_Existing_Variant_Uoms_When_AllowDifferentUom_Enabled()
    {
        var service = new ItemVariantService(null!, null!);

        var template = new Item(Guid.NewGuid(), _companyId, "TPL-01", "Template", ItemType.Goods)
        {
            HasVariants = true,
            Uom = "Nos",
            SalesUom = "Box",
            PurchaseUom = "Carton",
            Brand = "UpdatedBrand"
        };

        var variant = new Item(Guid.NewGuid(), _companyId, "TPL-01-RED", "Template - Red", ItemType.Goods)
        {
            VariantOfId = template.Id,
            Uom = "Kg",
            SalesUom = "Gram",
            PurchaseUom = "Ton",
            Brand = "OldBrand"
        };

        // Sync existing variant with allowDifferentUom = true -> UOMs stay on variant's values
        service.CopyAttributesToVariant(template, variant, isNewVariant: false, allowDifferentUom: true);

        variant.Uom.ShouldBe("Kg");
        variant.SalesUom.ShouldBe("Gram");
        variant.PurchaseUom.ShouldBe("Ton");
        variant.Brand.ShouldBe("UpdatedBrand");

        // Sync existing variant with allowDifferentUom = false -> UOMs overwritten from template
        service.CopyAttributesToVariant(template, variant, isNewVariant: false, allowDifferentUom: false);

        variant.Uom.ShouldBe("Nos");
        variant.SalesUom.ShouldBe("Box");
        variant.PurchaseUom.ShouldBe("Carton");
    }

    // =========================================================================
    // PR #60130 (commit 3b014ad602): Item Alternative & Work Order Stock Entry validation
    // =========================================================================

    [Fact]
    public void ValidateWorkOrderAlternativeItems_Should_Reject_When_WorkOrder_Disallows_Alternative_Items()
    {
        var origItemId = Guid.NewGuid();
        var altItemId = Guid.NewGuid();
        var fgItemId = Guid.NewGuid();
        var woId = Guid.NewGuid();

        var wo = new WorkOrder(woId, _companyId, "WO-001", fgItemId, Guid.NewGuid(), 10m)
        {
            AllowAlternativeItem = false
        };

        var entry = new StockEntry(Guid.NewGuid(), _companyId, StockEntryType.MaterialTransferForManufacture, DateTime.UtcNow)
        {
            WorkOrderId = woId
        };
        var itemRow = entry.AddItem(altItemId, 5m, Guid.NewGuid(), Guid.NewGuid());
        itemRow.OriginalItemId = origItemId;

        var altRecord = new ItemAlternative(Guid.NewGuid(), _companyId, origItemId, altItemId, twoWay: false);
        var manager = new StockEntryManager(null!, null!, null!);

        var ex = Should.Throw<BusinessException>(() =>
            manager.ValidateWorkOrderAlternativeItems(entry, wo, new[] { altRecord }));

        ex.Code.ShouldBe(MyERPDomainErrorCodes.WorkOrderDoesNotAllowAlternativeItems);
    }

    [Fact]
    public void ValidateWorkOrderAlternativeItems_Should_Reject_Unregistered_Alternative_And_Respect_TwoWay()
    {
        var origItemId = Guid.NewGuid();
        var altItemId = Guid.NewGuid();
        var fgItemId = Guid.NewGuid();
        var woId = Guid.NewGuid();

        var wo = new WorkOrder(woId, _companyId, "WO-002", fgItemId, Guid.NewGuid(), 10m)
        {
            AllowAlternativeItem = true
        };

        var entry = new StockEntry(Guid.NewGuid(), _companyId, StockEntryType.MaterialTransferForManufacture, DateTime.UtcNow)
        {
            WorkOrderId = woId
        };
        var itemRow = entry.AddItem(altItemId, 5m, Guid.NewGuid(), Guid.NewGuid());
        itemRow.OriginalItemId = origItemId;

        // Reverse 1-way (alt -> orig with TwoWay = false) must NOT allow substituting orig with alt
        var reverseOneWay = new ItemAlternative(Guid.NewGuid(), _companyId, altItemId, origItemId, twoWay: false);
        var manager = new StockEntryManager(null!, null!, null!);

        var ex = Should.Throw<BusinessException>(() =>
            manager.ValidateWorkOrderAlternativeItems(entry, wo, new[] { reverseOneWay }));
        ex.Code.ShouldBe(MyERPDomainErrorCodes.ItemIsNotAlternativeOfOriginal);

        // Reverse 2-way (alt <-> orig with TwoWay = true) MUST allow substituting orig with alt
        reverseOneWay.TwoWay = true;
        Should.NotThrow(() =>
            manager.ValidateWorkOrderAlternativeItems(entry, wo, new[] { reverseOneWay }));
    }

    [Fact]
    public void ValidateWorkOrderAlternativeItems_Should_Allow_Consuming_Or_Returning_Already_Transferred_Alternative_After_Approval_Removed()
    {
        var origItemId = Guid.NewGuid();
        var altItemId = Guid.NewGuid();
        var fgItemId = Guid.NewGuid();
        var woId = Guid.NewGuid();
        var wipWarehouseId = Guid.NewGuid();
        var fgWarehouseId = Guid.NewGuid();

        // Work order later had AllowAlternativeItem turned off and ItemAlternative deleted
        var wo = new WorkOrder(woId, _companyId, "WO-003", fgItemId, Guid.NewGuid(), 10m)
        {
            AllowAlternativeItem = false
        };

        // Previously submitted MaterialTransferForManufacture entry that transferred altItemId for origItemId
        var transferEntry = new StockEntry(Guid.NewGuid(), _companyId, StockEntryType.MaterialTransferForManufacture, DateTime.UtcNow)
        {
            WorkOrderId = woId
        };
        var transferredRow = transferEntry.AddItem(altItemId, 5m, Guid.NewGuid(), wipWarehouseId);
        transferredRow.OriginalItemId = origItemId;
        transferEntry.Submit();

        // Now Manufacture entry consumes the already-transferred alternative item
        var manufactureEntry = new StockEntry(Guid.NewGuid(), _companyId, StockEntryType.Manufacture, DateTime.UtcNow)
        {
            WorkOrderId = woId,
            FgCompletedQty = 10m
        };
        var consumedRow = manufactureEntry.AddItem(altItemId, 5m, wipWarehouseId, null);
        consumedRow.OriginalItemId = origItemId;
        manufactureEntry.AddItem(fgItemId, 10m, null, fgWarehouseId, isFinishedItem: true);

        var manager = new StockEntryManager(null!, null!, null!);

        // Should succeed even with empty alternatives list and AllowAlternativeItem = false
        Should.NotThrow(() =>
            manager.ValidateWorkOrderAlternativeItems(
                manufactureEntry,
                wo,
                Array.Empty<ItemAlternative>(),
                new[] { transferEntry }));
    }
}
