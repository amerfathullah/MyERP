using System;
using System.Threading.Tasks;
using MyERP.Inventory.Entities;
using MyERP.Inventory.DomainServices;
using MyERP.Manufacturing.DomainServices;
using MyERP.Manufacturing.Entities;
using MyERP.Manufacturing;
using MyERP.Purchasing.DomainServices;
using MyERP.Purchasing.Entities;
using MyERP.Purchasing;
using MyERP.Sales.Entities;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace MyERP.NewEntitiesAndServices;

public class NewEntitiesAndServicesTests
{
    // ========== UOM Entity Tests ==========

    [Fact]
    public void Uom_Create_SetsDefaults()
    {
        var uom = new Uom(Guid.NewGuid(), "Kilogram");
        uom.Name.ShouldBe("Kilogram");
        uom.MustBeWholeNumber.ShouldBeFalse();
        uom.IsEnabled.ShouldBeTrue();
        uom.Category.ShouldBeNull();
    }

    [Fact]
    public void Uom_ValidateWholeNumber_PassesForWholeNumber()
    {
        var uom = new Uom(Guid.NewGuid(), "Unit") { MustBeWholeNumber = true };
        Should.NotThrow(() => uom.ValidateWholeNumber(5m));
    }

    [Fact]
    public void Uom_ValidateWholeNumber_ThrowsForFractional()
    {
        var uom = new Uom(Guid.NewGuid(), "Unit") { MustBeWholeNumber = true };
        Should.Throw<BusinessException>(() => uom.ValidateWholeNumber(5.5m));
    }

    [Fact]
    public void Uom_ValidateWholeNumber_SkipsWhenNotRequired()
    {
        var uom = new Uom(Guid.NewGuid(), "Kg") { MustBeWholeNumber = false };
        Should.NotThrow(() => uom.ValidateWholeNumber(3.14m));
    }

    [Fact]
    public void Uom_ValidateWholeNumber_PassesWithinTolerance()
    {
        var uom = new Uom(Guid.NewGuid(), "Unit") { MustBeWholeNumber = true };
        // 5.00000001 is within tolerance (0.0000001)
        Should.NotThrow(() => uom.ValidateWholeNumber(5.00000001m));
    }

    [Fact]
    public void Uom_EmptyName_Throws()
    {
        Should.Throw<ArgumentException>(() => new Uom(Guid.NewGuid(), ""));
    }

    // ========== ItemDefault Entity Tests ==========

    [Fact]
    public void ItemDefault_Create_SetsRequiredFields()
    {
        var itemId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        var id = new ItemDefault(Guid.NewGuid(), itemId, companyId);

        id.ItemId.ShouldBe(itemId);
        id.CompanyId.ShouldBe(companyId);
        id.DefaultWarehouseId.ShouldBeNull();
        id.IncomeAccountId.ShouldBeNull();
        id.ExpenseAccountId.ShouldBeNull();
        id.DefaultDiscountPercentage.ShouldBe(0);
    }

    [Fact]
    public void ItemDefault_CanSetAllFields()
    {
        var id = new ItemDefault(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        var warehouseId = Guid.NewGuid();
        var incomeId = Guid.NewGuid();
        var expenseId = Guid.NewGuid();

        id.DefaultWarehouseId = warehouseId;
        id.IncomeAccountId = incomeId;
        id.ExpenseAccountId = expenseId;
        id.DefaultDiscountPercentage = 5m;

        id.DefaultWarehouseId.ShouldBe(warehouseId);
        id.IncomeAccountId.ShouldBe(incomeId);
        id.ExpenseAccountId.ShouldBe(expenseId);
        id.DefaultDiscountPercentage.ShouldBe(5m);
    }

    // ========== PutawayRule Entity Tests ==========

    [Fact]
    public void PutawayRule_Create_SetsDefaults()
    {
        var rule = new PutawayRule(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        rule.StockCapacity.ShouldBe(0);
        rule.Priority.ShouldBe(1);
        rule.IsEnabled.ShouldBeTrue();
        rule.ItemId.ShouldBeNull();
    }

    [Fact]
    public void PutawayRule_GetAvailableCapacity_UnlimitedWhenZero()
    {
        var rule = new PutawayRule(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid())
        {
            StockCapacity = 0
        };
        rule.GetAvailableCapacity(500m).ShouldBe(decimal.MaxValue);
    }

    [Fact]
    public void PutawayRule_GetAvailableCapacity_CalculatesCorrectly()
    {
        var rule = new PutawayRule(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid())
        {
            StockCapacity = 100
        };
        rule.GetAvailableCapacity(60m).ShouldBe(40m);
    }

    [Fact]
    public void PutawayRule_GetAvailableCapacity_NeverNegative()
    {
        var rule = new PutawayRule(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid())
        {
            StockCapacity = 50
        };
        rule.GetAvailableCapacity(80m).ShouldBe(0m);
    }

    // ========== InstallationNote Entity Tests ==========

    [Fact]
    public void InstallationNote_Create_SetsDefaults()
    {
        var note = new InstallationNote(Guid.NewGuid(), Guid.NewGuid(), "IN-001",
            Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow);

        note.Status.ShouldBe(Core.DocumentStatus.Draft);
        note.Items.Count.ShouldBe(0);
        note.InstallationNumber.ShouldBe("IN-001");
    }

    [Fact]
    public void InstallationNote_AddItem_InDraft()
    {
        var note = new InstallationNote(Guid.NewGuid(), Guid.NewGuid(), "IN-001",
            Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow);

        note.AddItem(Guid.NewGuid(), 5m, "SN-001");
        note.Items.Count.ShouldBe(1);
        note.Items[0].Qty.ShouldBe(5m);
        note.Items[0].SerialNo.ShouldBe("SN-001");
    }

    [Fact]
    public void InstallationNote_AddItem_ZeroQty_Throws()
    {
        var note = new InstallationNote(Guid.NewGuid(), Guid.NewGuid(), "IN-001",
            Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow);

        Should.Throw<ArgumentException>(() => note.AddItem(Guid.NewGuid(), 0));
    }

    [Fact]
    public void InstallationNote_AddItem_AfterSubmit_Throws()
    {
        var note = new InstallationNote(Guid.NewGuid(), Guid.NewGuid(), "IN-001",
            Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow);
        note.AddItem(Guid.NewGuid(), 1m);
        note.Submit();

        Should.Throw<BusinessException>(() => note.AddItem(Guid.NewGuid(), 1m));
    }

    [Fact]
    public void InstallationNote_ValidateDate_BeforeDN_Throws()
    {
        var note = new InstallationNote(Guid.NewGuid(), Guid.NewGuid(), "IN-001",
            Guid.NewGuid(), Guid.NewGuid(), new DateTime(2026, 7, 10));

        Should.Throw<BusinessException>(() =>
            note.ValidateInstallationDate(new DateTime(2026, 7, 15)));
    }

    [Fact]
    public void InstallationNote_ValidateDate_AfterDN_Passes()
    {
        var note = new InstallationNote(Guid.NewGuid(), Guid.NewGuid(), "IN-001",
            Guid.NewGuid(), Guid.NewGuid(), new DateTime(2026, 7, 15));

        Should.NotThrow(() =>
            note.ValidateInstallationDate(new DateTime(2026, 7, 10)));
    }

    [Fact]
    public void InstallationNote_Submit_Cancel_Lifecycle()
    {
        var note = new InstallationNote(Guid.NewGuid(), Guid.NewGuid(), "IN-001",
            Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow);
        note.AddItem(Guid.NewGuid(), 5m);

        note.Submit();
        note.Status.ShouldBe(Core.DocumentStatus.Submitted);

        note.Cancel();
        note.Status.ShouldBe(Core.DocumentStatus.Cancelled);
    }

    // ========== BOM BackflushBasedOn Tests ==========

    [Fact]
    public void Bom_BackflushBasedOn_DefaultsNull()
    {
        var bom = new BillOfMaterials(Guid.NewGuid(), Guid.NewGuid(), "BOM-001", Guid.NewGuid());
        bom.BackflushBasedOn.ShouldBeNull();
    }

    [Fact]
    public void Bom_BackflushBasedOn_CanBeSet()
    {
        var bom = new BillOfMaterials(Guid.NewGuid(), Guid.NewGuid(), "BOM-001", Guid.NewGuid());
        bom.BackflushBasedOn = "Material Transferred for Manufacture";
        bom.BackflushBasedOn.ShouldBe("Material Transferred for Manufacture");
    }

    [Fact]
    public void Bom_RoutingId_DefaultsNull()
    {
        var bom = new BillOfMaterials(Guid.NewGuid(), Guid.NewGuid(), "BOM-001", Guid.NewGuid());
        bom.RoutingId.ShouldBeNull();
    }

    // ========== RoutingOperation BatchSize Tests ==========

    [Fact]
    public void RoutingOperation_BatchSize_DefaultsZero()
    {
        var routing = new Routing(Guid.NewGuid(), "Test Routing");
        routing.AddOperation(Guid.NewGuid(), 1, 30m);
        routing.Operations[0].BatchSize.ShouldBe(0);
    }

    // ========== WorkOrderManager Tests ==========

    [Fact]
    public void WorkOrderMaterialRequirement_StoresValues()
    {
        var req = new WorkOrderMaterialRequirement
        {
            ItemId = Guid.NewGuid(),
            RequiredQty = 10m,
            Rate = 5.50m,
            SourceWarehouseId = Guid.NewGuid()
        };

        req.RequiredQty.ShouldBe(10m);
        req.Rate.ShouldBe(5.50m);
        req.SourceWarehouseId.ShouldNotBeNull();
    }

    // ========== StockEntryManager Tests ==========

    [Fact]
    public void StockEntryManager_ValidateTransferQty_WithinLimit_Passes()
    {
        var mgr = new StockEntryManager(null!, null!);
        Should.NotThrow(() => mgr.ValidateTransferQty(
            requiredQty: 100m, transferredQty: 40m, requestedQty: 50m));
    }

    [Fact]
    public void StockEntryManager_ValidateTransferQty_ExceedsLimit_Throws()
    {
        var mgr = new StockEntryManager(null!, null!);
        Should.Throw<BusinessException>(() => mgr.ValidateTransferQty(
            requiredQty: 100m, transferredQty: 80m, requestedQty: 30m));
    }

    [Fact]
    public void StockEntryManager_ValidateTransferQty_ReturnBypass()
    {
        var mgr = new StockEntryManager(null!, null!);
        Should.NotThrow(() => mgr.ValidateTransferQty(
            requiredQty: 100m, transferredQty: 100m, requestedQty: 50m,
            isReturn: true));
    }

    [Fact]
    public void StockEntryManager_ValidateTransferQty_MaterialTransferredBypass()
    {
        var mgr = new StockEntryManager(null!, null!);
        Should.NotThrow(() => mgr.ValidateTransferQty(
            requiredQty: 100m, transferredQty: 100m, requestedQty: 50m,
            isMaterialTransferredMode: true));
    }

    [Fact]
    public void StockEntryManager_ValidateTransferQty_ExactLimit_Passes()
    {
        var mgr = new StockEntryManager(null!, null!);
        Should.NotThrow(() => mgr.ValidateTransferQty(
            requiredQty: 100m, transferredQty: 60m, requestedQty: 40m));
    }

    // ========== MaterialRequestManager Tests ==========

    [Fact]
    public void MaterialRequestManager_ValidateForSubmission_EmptyItems_Throws()
    {
        var mgr = new MaterialRequestManager(null!);
        var mr = new MaterialRequest(Guid.NewGuid(), Guid.NewGuid(), "MR-001",
            MaterialRequestType.Purchase, DateTime.UtcNow);

        Should.Throw<BusinessException>(() => mgr.ValidateForSubmission(mr));
    }

    [Fact]
    public void MaterialRequestManager_IsFullyFulfilled_NoItems_ReturnsFalse()
    {
        var mgr = new MaterialRequestManager(null!);
        var mr = new MaterialRequest(Guid.NewGuid(), Guid.NewGuid(), "MR-001",
            MaterialRequestType.Purchase, DateTime.UtcNow);

        mgr.IsFullyFulfilled(mr).ShouldBeFalse();
    }

    [Fact]
    public void MaterialRequestManager_IsFullyFulfilled_PartiallyOrdered_ReturnsFalse()
    {
        var mgr = new MaterialRequestManager(null!);
        var mr = new MaterialRequest(Guid.NewGuid(), Guid.NewGuid(), "MR-001",
            MaterialRequestType.Purchase, DateTime.UtcNow);
        mr.AddItem(Guid.NewGuid(), "Item A", 100m, "Unit", null);
        mr.Items[0].OrderedQuantity = 50m; // 50% ordered

        mgr.IsFullyFulfilled(mr).ShouldBeFalse();
    }

    [Fact]
    public void MaterialRequestManager_IsFullyFulfilled_FullyOrdered_ReturnsTrue()
    {
        var mgr = new MaterialRequestManager(null!);
        var mr = new MaterialRequest(Guid.NewGuid(), Guid.NewGuid(), "MR-001",
            MaterialRequestType.Purchase, DateTime.UtcNow);
        mr.AddItem(Guid.NewGuid(), "Item A", 100m, "Unit", null);
        mr.Items[0].OrderedQuantity = 100m;

        mgr.IsFullyFulfilled(mr).ShouldBeTrue();
    }

    [Fact]
    public void MaterialRequestManager_GetPendingQty_Calculates()
    {
        var mr = new MaterialRequest(Guid.NewGuid(), Guid.NewGuid(), "MR-X",
            MaterialRequestType.Purchase, DateTime.UtcNow);
        mr.AddItem(Guid.NewGuid(), "Item A", 100m, "Unit", null);
        mr.Items[0].OrderedQuantity = 40m;
        MaterialRequestManager.GetPendingQty(mr.Items[0]).ShouldBe(60m);
    }

    [Fact]
    public void MaterialRequestManager_GetPendingQty_NeverNegative()
    {
        var mr = new MaterialRequest(Guid.NewGuid(), Guid.NewGuid(), "MR-Y",
            MaterialRequestType.Purchase, DateTime.UtcNow);
        mr.AddItem(Guid.NewGuid(), "Item B", 100m, "Unit", null);
        mr.Items[0].OrderedQuantity = 150m;
        MaterialRequestManager.GetPendingQty(mr.Items[0]).ShouldBe(0m);
    }

    // ========== WorkOrder RecordMaterialTransfer ==========

    [Fact]
    public void WorkOrder_RecordMaterialTransfer_IncreasesTotal()
    {
        var wo = new WorkOrder(Guid.NewGuid(), Guid.NewGuid(), "WO-001",
            Guid.NewGuid(), Guid.NewGuid(), 100m);
        wo.Submit();
        wo.RecordMaterialTransfer(50m);

        wo.MaterialTransferred.ShouldBe(50m);
        wo.Status.ShouldBe(Manufacturing.WorkOrderStatus.NotStarted);
    }

    [Fact]
    public void WorkOrder_RecordMaterialTransfer_Cumulative()
    {
        var wo = new WorkOrder(Guid.NewGuid(), Guid.NewGuid(), "WO-001",
            Guid.NewGuid(), Guid.NewGuid(), 100m);
        wo.Submit();
        wo.RecordMaterialTransfer(30m);
        wo.RecordMaterialTransfer(40m);

        wo.MaterialTransferred.ShouldBe(70m);
    }
}
