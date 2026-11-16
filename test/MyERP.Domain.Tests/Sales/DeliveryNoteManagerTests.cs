using System;
using MyERP.Core;
using MyERP.Sales.Entities;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace MyERP.Sales;

public class DeliveryNoteManagerTests
{
    [Fact]
    public void DN_DefaultStatus_IsDraft()
    {
        var dn = CreateDN();
        dn.Status.ShouldBe(DocumentStatus.Draft);
    }

    [Fact]
    public void DN_IsReturn_DefaultFalse()
    {
        var dn = CreateDN();
        dn.IsReturn.ShouldBeFalse();
        dn.ReturnAgainstId.ShouldBeNull();
    }

    [Fact]
    public void DN_AddItem_InDraft_Succeeds()
    {
        var dn = CreateDN();
        dn.AddItem(Guid.NewGuid(), "Widget", 10, 100, 0);
        dn.Items.Count.ShouldBe(1);
    }

    [Fact]
    public void DN_Submit_WithItems_Succeeds()
    {
        var dn = CreateDN();
        dn.AddItem(Guid.NewGuid(), "Widget", 10, 100, 0);
        dn.Submit();
        dn.Status.ShouldBe(DocumentStatus.Submitted);
    }

    [Fact]
    public void DN_Submit_WithoutItems_Throws()
    {
        var dn = CreateDN();
        Should.Throw<BusinessException>(() => dn.Submit());
    }

    [Fact]
    public void DN_Cancel_AfterSubmit_Succeeds()
    {
        var dn = CreateDN();
        dn.AddItem(Guid.NewGuid(), "Widget", 10, 100, 0);
        dn.Submit();
        dn.Cancel();
        dn.Status.ShouldBe(DocumentStatus.Cancelled);
    }

    [Fact]
    public void DN_TotalCalculation()
    {
        var dn = CreateDN();
        dn.AddItem(Guid.NewGuid(), "Widget A", 5, 100, 10);
        dn.AddItem(Guid.NewGuid(), "Widget B", 3, 200, 20);

        dn.NetTotal.ShouldBe(1100m); // 500 + 600
        dn.TaxAmount.ShouldBe(30m);  // 10 + 20
        dn.GrandTotal.ShouldBe(1130m);
    }

    [Fact]
    public void DN_Amendment_DefaultValues()
    {
        var dn = CreateDN();
        dn.AmendedFromId.ShouldBeNull();
        dn.AmendmentIndex.ShouldBe(0);
    }

    [Fact]
    public void DN_BillingAddress_DefaultNull()
    {
        var dn = CreateDN();
        dn.BillingAddressId.ShouldBeNull();
        dn.ShippingAddressId.ShouldBeNull();
    }

    [Fact]
    public void DN_ValuationRate_DefaultZero()
    {
        var dn = CreateDN();
        dn.AddItem(Guid.NewGuid(), "Widget", 10, 100, 0);
        dn.Items[0].ValuationRate.ShouldBe(0);
    }

    [Fact]
    public void CalculateSettledReturnQtyByDeliveryRow_SharesOverbilledSoQtyAcrossChronologicalDeliveries()
    {
        // Per ERPNext PR #60607: when SO is billed directly beyond net delivered qty,
        // returned DN rows settle earliest deliveries first without needing duplicate credit notes.
        var soItemId = Guid.NewGuid();
        var dnRow1 = Guid.NewGuid();
        var dnRow2 = Guid.NewGuid();

        // Ordered 10, Billed against SO = 10, DN1 delivered 6 & returned 2, DN2 delivered 4 & returned 2
        // Net delivered = 6, Total billed = 10 => Overbilled = 4, settled across DN1 (2) then DN2 (2)
        var summary = new Dictionary<Guid, (decimal TotalBilledStockQty, decimal BilledAgainstSoStockQty, decimal NetDeliveredStockQty)>
        {
            [soItemId] = (10m, 10m, 6m)
        };
        var rows = new[]
        {
            (dnRow1, soItemId, 6m, 1m, 0m, -2m),
            (dnRow2, soItemId, 4m, 1m, 0m, -2m),
        };

        var settled = DeliveryNoteManager.CalculateSettledReturnQtyByDeliveryRow(summary, rows);

        settled[dnRow1].ShouldBe(2m);
        settled[dnRow2].ShouldBe(2m);
    }

    private static DeliveryNote CreateDN()
    {
        return new DeliveryNote(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), "DN-001", DateTime.UtcNow);
    }
}
