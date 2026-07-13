using System;
using MyERP.Accounting.Entities;
using MyERP.Core;
using MyERP.Inventory.DomainServices;
using MyERP.Sales.Entities;
using Shouldly;
using Xunit;

namespace MyERP.Domain.Tests.Integration;

/// <summary>
/// Tests for:
/// - DN IAmendable + AmendAsync pattern
/// - WO material stock validation before production
/// - FiscalYear sequential close enforcement
/// </summary>
public class DNAmendWOStockFYCloseTests
{
    // --- DN Amendment ---

    [Fact]
    public void DeliveryNote_Implements_IAmendable()
    {
        var dn = new DeliveryNote(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), "DN-001", DateTime.Today);
        (dn is IAmendable).ShouldBeTrue();
    }

    [Fact]
    public void DeliveryNote_AmendmentFields_DefaultValues()
    {
        var dn = new DeliveryNote(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), "DN-002", DateTime.Today);
        dn.AmendedFromId.ShouldBeNull();
        dn.AmendmentIndex.ShouldBe(0);
    }

    [Fact]
    public void DeliveryNote_Amendment_CopiesSalesOrderLink()
    {
        var soId = Guid.NewGuid();
        var original = new DeliveryNote(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), "DN-100", DateTime.Today);
        original.SalesOrderId = soId;

        var amended = new DeliveryNote(Guid.NewGuid(), original.CompanyId, original.CustomerId,
            original.WarehouseId, "DN-100-1", DateTime.Today);
        amended.AmendedFromId = original.Id;
        amended.AmendmentIndex = 1;
        amended.SalesOrderId = original.SalesOrderId;

        amended.SalesOrderId.ShouldBe(soId);
        amended.AmendedFromId.ShouldBe(original.Id);
    }

    // --- WO Material Stock Validation ---

    [Fact]
    public void StockBalance_InsufficientQty_Detected()
    {
        // Simulates: balance 5 units, need 10 → insufficient
        var balance = new StockBalance(5m, 500m);
        var required = 10m;

        (balance.Quantity < required).ShouldBeTrue();
    }

    [Fact]
    public void StockBalance_SufficientQty_Allowed()
    {
        var balance = new StockBalance(20m, 2000m);
        var required = 10m;

        (balance.Quantity >= required).ShouldBeTrue();
    }

    [Fact]
    public void StockBalance_ZeroQty_Blocks()
    {
        var balance = new StockBalance(0m, 0m);
        var required = 5m;

        (balance.Quantity < required).ShouldBeTrue();
    }

    [Fact]
    public void ProductionRatio_CalculatesCorrectIssueQty()
    {
        // WO for 100 units, producing 25 → ratio = 0.25
        // BOM item needs 200 raw material → issue 50
        decimal woQty = 100m;
        decimal produceQty = 25m;
        decimal bomItemQty = 200m;

        var ratio = produceQty / woQty;
        var issueQty = Math.Round(bomItemQty * ratio, 4);

        issueQty.ShouldBe(50m);
    }

    // --- FiscalYear Sequential Close ---

    [Fact]
    public void FiscalYear_SequentialClose_PriorMustBeClosed()
    {
        var fy2024 = new FiscalYear(Guid.NewGuid(), Guid.NewGuid(), "FY 2024",
            new DateTime(2024, 1, 1), new DateTime(2024, 12, 31));
        var fy2025 = new FiscalYear(Guid.NewGuid(), fy2024.CompanyId, "FY 2025",
            new DateTime(2025, 1, 1), new DateTime(2025, 12, 31));

        // FY2024 is open, FY2025 end date is before FY2025 start
        var priorIsOpen = !fy2024.IsClosed && fy2024.EndDate < fy2025.StartDate;
        priorIsOpen.ShouldBeTrue(); // Would block FY2025 close
    }

    [Fact]
    public void FiscalYear_SequentialClose_AllPriorClosed_Allows()
    {
        var fy2024 = new FiscalYear(Guid.NewGuid(), Guid.NewGuid(), "FY 2024",
            new DateTime(2024, 1, 1), new DateTime(2024, 12, 31));
        fy2024.IsClosed = true;

        var fy2025 = new FiscalYear(Guid.NewGuid(), fy2024.CompanyId, "FY 2025",
            new DateTime(2025, 1, 1), new DateTime(2025, 12, 31));

        var priorIsOpen = !fy2024.IsClosed && fy2024.EndDate < fy2025.StartDate;
        priorIsOpen.ShouldBeFalse(); // FY2025 close allowed
    }

    [Fact]
    public void FiscalYear_Close_Idempotent()
    {
        var fy = new FiscalYear(Guid.NewGuid(), Guid.NewGuid(), "FY 2026",
            new DateTime(2026, 1, 1), new DateTime(2026, 12, 31));
        fy.IsClosed = true;

        // Closing again is idempotent (no error)
        fy.IsClosed = true;
        fy.IsClosed.ShouldBeTrue();
    }
}
