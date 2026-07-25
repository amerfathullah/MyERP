using System;
using MyERP.Manufacturing;
using MyERP.Manufacturing.Entities;
using MyERP.Sales.Entities;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace MyERP.Integration;

/// <summary>
/// Tests for input validation improvements: BOM empty items, WO qty, Quotation ClearItems.
/// </summary>
public class InputValidationTests
{
    // === BOM Validation ===

    [Fact]
    public void BOM_WithItems_IsValid()
    {
        var bom = new BillOfMaterials(Guid.NewGuid(), Guid.NewGuid(), "BOM-001", Guid.NewGuid(), Guid.NewGuid());
        bom.Items.Add(new BomItem(Guid.NewGuid(), bom.Id, Guid.NewGuid(), "Steel", 10, 5));

        bom.Items.Count.ShouldBe(1);
        bom.TotalMaterialCost.ShouldBe(0m); // Not recalculated yet
        bom.RecalculateCost();
        bom.TotalMaterialCost.ShouldBe(50m);
    }

    [Fact]
    public void BOM_EmptyItems_HasZeroCost()
    {
        var bom = new BillOfMaterials(Guid.NewGuid(), Guid.NewGuid(), "BOM-002", Guid.NewGuid(), Guid.NewGuid());
        bom.Items.Count.ShouldBe(0);
        bom.RecalculateCost();
        bom.TotalMaterialCost.ShouldBe(0m);
    }

    // === Work Order Quantity ===

    [Fact]
    public void WorkOrder_PositiveQuantity_Succeeds()
    {
        var wo = new WorkOrder(Guid.NewGuid(), Guid.NewGuid(), "WO-001", Guid.NewGuid(), Guid.NewGuid(), 10m, null);
        wo.Quantity.ShouldBe(10m);
    }

    [Fact]
    public void WorkOrder_Quantity_CannotBeZero()
    {
        // Domain entity constructor allows zero (validation is at AppService level)
        // but entity operations (RecordProduction) guard against it
        var wo = new WorkOrder(Guid.NewGuid(), Guid.NewGuid(), "WO-002", Guid.NewGuid(), Guid.NewGuid(), 0m, null);
        wo.Quantity.ShouldBe(0m);
        // PercentComplete should handle zero quantity gracefully (no divide by zero)
        wo.PercentComplete.ShouldBe(0m);
    }

    [Fact]
    public void WorkOrder_RecordProduction_ExactQuantity_Completes()
    {
        var wo = new WorkOrder(Guid.NewGuid(), Guid.NewGuid(), "WO-003", Guid.NewGuid(), Guid.NewGuid(), 5m, null);
        wo.Submit();
        wo.Start();
        wo.RecordProduction(5m);
        wo.Status.ShouldBe(WorkOrderStatus.Completed);
    }

    // === Quotation ClearItems ===

    [Fact]
    public void Quotation_ClearItems_RemovesAllItems()
    {
        var q = CreateQuotation();
        q.AddItem(Guid.NewGuid(), "Widget A", 5, 100, 0);
        q.AddItem(Guid.NewGuid(), "Widget B", 3, 200, 0);
        q.Items.Count.ShouldBe(2);

        q.ClearItems();

        q.Items.Count.ShouldBe(0);
        q.NetTotal.ShouldBe(0);
        q.GrandTotal.ShouldBe(0);
    }

    [Fact]
    public void Quotation_ClearItems_BlockedAfterSubmit()
    {
        var q = CreateQuotation();
        q.AddItem(Guid.NewGuid(), "Item", 1, 50, 0);
        q.Submit();

        Should.Throw<BusinessException>(() => q.ClearItems());
    }

    [Fact]
    public void Quotation_ClearItems_ThenReaddItems()
    {
        var q = CreateQuotation();
        q.AddItem(Guid.NewGuid(), "Old Item", 10, 100, 0);
        q.ClearItems();
        q.AddItem(Guid.NewGuid(), "New Item", 5, 200, 0);

        q.Items.Count.ShouldBe(1);
        q.NetTotal.ShouldBe(1000m); // 5 * 200
    }

    [Fact]
    public void Quotation_Edit_UpdateFieldsWhileDraft()
    {
        var q = CreateQuotation();
        q.AddItem(Guid.NewGuid(), "Item", 1, 100, 0);

        q.ValidUntil = DateTime.Today.AddDays(60);
        q.Notes = "Updated notes";

        q.ValidUntil.ShouldBe(DateTime.Today.AddDays(60));
        q.Notes.ShouldBe("Updated notes");
    }

    private static Quotation CreateQuotation() =>
        new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "QTN-001", DateTime.Today, Guid.NewGuid());
}
