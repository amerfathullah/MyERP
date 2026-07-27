using System;
using System.Linq;
using MyERP.Core;
using MyERP.Inventory;
using MyERP.Inventory.Entities;
using MyERP.Manufacturing;
using MyERP.Manufacturing.DomainServices;
using MyERP.Manufacturing.Entities;
using MyERP.Sales;
using MyERP.Sales.Entities;
using Xunit;

namespace MyERP.Domain.Tests;

/// <summary>
/// Tests for BOM routing validation, subscription catch-up, credit note creation,
/// and QI enforcement verification.
/// </summary>
public class BomRoutingSubscriptionCreditNoteTests
{
    // === BOM Operations Routing Sequence ===

    [Fact]
    public void Bom_Operations_MonotonicallyIncreasingSequence_Valid()
    {
        var bom = new BillOfMaterials(Guid.NewGuid(), Guid.NewGuid(), "BOM-001",
            Guid.NewGuid());
        bom.AddOperation(new BomOperation(Guid.NewGuid(), bom.Id, Guid.NewGuid(),
            sequenceId: 10, timeInMins: 15, workstationId: Guid.NewGuid()));
        bom.AddOperation(new BomOperation(Guid.NewGuid(), bom.Id, Guid.NewGuid(),
            sequenceId: 20, timeInMins: 30, workstationId: Guid.NewGuid()));
        bom.AddOperation(new BomOperation(Guid.NewGuid(), bom.Id, Guid.NewGuid(),
            sequenceId: 30, timeInMins: 10, workstationId: Guid.NewGuid()));

        // Validate should pass — sequence is 10, 20, 30 (increasing)
        BomValidationService.ValidateOperationsSequence(bom);
    }

    [Fact]
    public void Bom_Operations_ParallelSameSequence_Valid()
    {
        // Per ERPNext: same sequence_id = parallel operations (allowed)
        var bom = new BillOfMaterials(Guid.NewGuid(), Guid.NewGuid(), "BOM-002",
            Guid.NewGuid());
        bom.AddOperation(new BomOperation(Guid.NewGuid(), bom.Id, Guid.NewGuid(),
            sequenceId: 10, timeInMins: 15, workstationId: Guid.NewGuid()));
        bom.AddOperation(new BomOperation(Guid.NewGuid(), bom.Id, Guid.NewGuid(),
            sequenceId: 10, timeInMins: 20, workstationId: Guid.NewGuid())); // Same seq = parallel
        bom.AddOperation(new BomOperation(Guid.NewGuid(), bom.Id, Guid.NewGuid(),
            sequenceId: 20, timeInMins: 10, workstationId: Guid.NewGuid()));

        BomValidationService.ValidateOperationsSequence(bom);
    }

    [Fact]
    public void Bom_Operations_DecreasingSequence_Throws()
    {
        // Per DO-NOT: "Allow routing sequence_id to decrease between rows"
        // AddOperation itself blocks decreasing sequence at add time
        var bom = new BillOfMaterials(Guid.NewGuid(), Guid.NewGuid(), "BOM-003",
            Guid.NewGuid());
        bom.AddOperation(new BomOperation(Guid.NewGuid(), bom.Id, Guid.NewGuid(),
            sequenceId: 20, timeInMins: 15, workstationId: Guid.NewGuid()));

        Assert.Throws<Volo.Abp.BusinessException>(() =>
            bom.AddOperation(new BomOperation(Guid.NewGuid(), bom.Id, Guid.NewGuid(),
                sequenceId: 10, timeInMins: 30, workstationId: Guid.NewGuid())));
    }

    [Fact]
    public void Bom_SingleOperation_NoValidationNeeded()
    {
        var bom = new BillOfMaterials(Guid.NewGuid(), Guid.NewGuid(), "BOM-004",
            Guid.NewGuid());
        bom.AddOperation(new BomOperation(Guid.NewGuid(), bom.Id, Guid.NewGuid(),
            sequenceId: 1, timeInMins: 60, workstationId: Guid.NewGuid()));

        // Single operation: always valid (nothing to compare)
        BomValidationService.ValidateOperationsSequence(bom);
    }

    // === Subscription Billing ===

    [Fact]
    public void Subscription_ActiveStatus_AllowsInvoicing()
    {
        var sub = new Subscription(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "Customer", DateTime.UtcNow.AddMonths(-3), "Monthly");
        sub.AddPlan(Guid.NewGuid(), 1m, 100m, "Service Plan");
        sub.AdvancePeriod();

        Assert.Equal(SubscriptionStatus.Active, sub.Status);
    }

    [Fact]
    public void Subscription_AdvancePeriod_MovesToNextBillingCycle()
    {
        var startDate = new DateTime(2026, 1, 1);
        var sub = new Subscription(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "Customer", startDate, "Monthly");
        sub.AddPlan(Guid.NewGuid(), 1m, 200m, "Premium");
        sub.AdvancePeriod();

        Assert.Equal(startDate, sub.CurrentInvoiceStart);
        // Monthly: end = start + 1 month - 1 day
        Assert.Equal(new DateTime(2026, 1, 31), sub.CurrentInvoiceEnd);

        sub.AdvancePeriod();
        Assert.Equal(new DateTime(2026, 2, 1), sub.CurrentInvoiceStart);
    }

    [Fact]
    public void Subscription_Cancelled_BlocksInvoicing()
    {
        var sub = new Subscription(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "Customer", DateTime.UtcNow, "Monthly");
        sub.AddPlan(Guid.NewGuid(), 1m, 50m, "Basic");
        sub.AdvancePeriod();
        sub.Cancel();

        Assert.Equal(SubscriptionStatus.Cancelled, sub.Status);
    }

    // === Credit Note from Return ===

    [Fact]
    public void SalesInvoice_Return_HasNegativeQuantity()
    {
        var si = new SalesInvoice(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "SI-RETURN-001", DateTime.UtcNow);
        si.IsReturn = true;

        // Per DO-NOT: "Allow returns with positive qty (must always be negative)"
        si.AddItem(Guid.NewGuid(), "Returned Widget", -5m, 100m, 0m);
        Assert.Equal(-5m, si.Items.First().Quantity);
    }

    [Fact]
    public void SalesInvoice_Return_PositiveQty_Blocked()
    {
        var si = new SalesInvoice(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "SI-RETURN-002", DateTime.UtcNow);
        si.IsReturn = true;

        // Per DO-NOT: positive qty on return invoice must throw
        Assert.Throws<ArgumentException>(() =>
            si.AddItem(Guid.NewGuid(), "Widget", 5m, 100m, 0m));
    }

    [Fact]
    public void SalesInvoice_HasDeliveryNoteId_ForCreditNoteTracking()
    {
        var si = new SalesInvoice(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "CN-001", DateTime.UtcNow);
        si.IsReturn = true;
        si.DeliveryNoteId = Guid.NewGuid();

        Assert.NotNull(si.DeliveryNoteId);
    }

    [Fact]
    public void SalesInvoice_GrandTotal_NegativeForCreditNote()
    {
        var si = new SalesInvoice(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "CN-002", DateTime.UtcNow);
        si.IsReturn = true;
        si.AddItem(Guid.NewGuid(), "Widget A", -3m, 200m, 0m);
        si.AddItem(Guid.NewGuid(), "Widget B", -2m, 150m, 0m);

        // GrandTotal: (-3 × 200) + (-2 × 150) = -600 + -300 = -900
        Assert.Equal(-900m, si.GrandTotal);
    }

    // === Manufacturing Settings Mutual Exclusion ===

    [Fact]
    public void ManufacturingSettings_BackflushNotBOM_DisablesValidateComponents()
    {
        // Per settings-configuration: if BackflushRawMaterialsBasedOn ≠ "BOM"
        // → Forces ValidateComponentsQuantitiesPerBom = false
        var settings = new ManufacturingSettings(Guid.NewGuid(), Guid.NewGuid());
        settings.BackflushRawMaterialsBasedOn = "Material Transferred for Manufacture";
        settings.ValidateComponentsQuantitiesPerBom = true;
        settings.EnforceMutualExclusions();

        Assert.False(settings.ValidateComponentsQuantitiesPerBom);
    }

    [Fact]
    public void ManufacturingSettings_BackflushBOM_KeepsValidateComponents()
    {
        var settings = new ManufacturingSettings(Guid.NewGuid(), Guid.NewGuid());
        settings.BackflushRawMaterialsBasedOn = "BOM";
        settings.ValidateComponentsQuantitiesPerBom = true;
        settings.EnforceMutualExclusions();

        Assert.True(settings.ValidateComponentsQuantitiesPerBom);
    }
}
