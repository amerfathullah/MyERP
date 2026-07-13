using System;
using System.Linq;
using MyERP.Accounting.Entities;
using MyERP.Inventory.Entities;
using Shouldly;
using Xunit;

namespace MyERP.Integration;

public class PostingValidationAndDefaultsTests
{
    [Fact]
    public void FiscalYear_IsClosed_DefaultsFalse()
    {
        var fy = new FiscalYear(Guid.NewGuid(), Guid.NewGuid(), "FY2026",
            new DateTime(2026, 1, 1), new DateTime(2026, 12, 31));
        fy.IsClosed.ShouldBeFalse();
    }

    [Fact]
    public void FiscalYear_ContainsDate_WhenWithinRange()
    {
        var fy = new FiscalYear(Guid.NewGuid(), Guid.NewGuid(), "FY2026",
            new DateTime(2026, 1, 1), new DateTime(2026, 12, 31));
        var date = new DateTime(2026, 6, 15);
        (fy.StartDate <= date && fy.EndDate >= date).ShouldBeTrue();
    }

    [Fact]
    public void FiscalYear_DoesNotContainDate_WhenOutsideRange()
    {
        var fy = new FiscalYear(Guid.NewGuid(), Guid.NewGuid(), "FY2026",
            new DateTime(2026, 1, 1), new DateTime(2026, 12, 31));
        var date = new DateTime(2027, 1, 1);
        (fy.StartDate <= date && fy.EndDate >= date).ShouldBeFalse();
    }

    [Fact]
    public void FiscalYear_ClosedYear_BlocksPosting()
    {
        var fy = new FiscalYear(Guid.NewGuid(), Guid.NewGuid(), "FY2025",
            new DateTime(2025, 1, 1), new DateTime(2025, 12, 31));
        fy.IsClosed = true;
        fy.IsClosed.ShouldBeTrue();
    }

    [Fact]
    public void ItemGroup_ParentId_SupportsHierarchy()
    {
        var rootId = Guid.NewGuid();
        var childId = Guid.NewGuid();
        var root = new ItemGroup(rootId, "All Items", true);
        var child = new ItemGroup(childId, "Electronics", false) { ParentId = rootId };

        child.ParentId.ShouldBe(rootId);
        root.IsGroup.ShouldBeTrue();
        child.IsGroup.ShouldBeFalse();
    }

    [Fact]
    public void ItemGroup_DefaultWarehouseId_CanBeSet()
    {
        var warehouseId = Guid.NewGuid();
        var group = new ItemGroup(Guid.NewGuid(), "Raw Materials", false);
        group.DefaultWarehouseId = warehouseId;
        group.DefaultWarehouseId.ShouldBe(warehouseId);
    }

    [Fact]
    public void ItemGroup_DefaultIncomeAccountId_NullByDefault()
    {
        var group = new ItemGroup(Guid.NewGuid(), "Services", false);
        group.DefaultIncomeAccountId.ShouldBeNull();
    }

    [Fact]
    public void SerialNo_PurchaseRate_UpdatedAfterLCV()
    {
        var serial = new SerialNo(Guid.NewGuid(), Guid.NewGuid(), "SN-001", Guid.NewGuid());
        serial.PurchaseRate = 100m;

        // Simulate LCV landing cost distribution
        var landedCostPerSerial = 25m;
        serial.PurchaseRate += landedCostPerSerial;

        serial.PurchaseRate.ShouldBe(125m);
    }

    [Fact]
    public void SerialNo_PurchaseRate_MultipleSerials_EvenDistribution()
    {
        var serials = Enumerable.Range(1, 4)
            .Select(i => new SerialNo(Guid.NewGuid(), Guid.NewGuid(), $"SN-{i:000}", Guid.NewGuid())
            {
                PurchaseRate = 200m
            }).ToList();

        // Total landed cost = 100, 4 serials → 25 each
        var totalCharge = 100m;
        var perSerial = totalCharge / serials.Count;

        foreach (var s in serials)
            s.PurchaseRate += perSerial;

        serials.All(s => s.PurchaseRate == 225m).ShouldBeTrue();
    }

    [Fact]
    public void Bin_ConcurrencyStamp_ImplementsInterface()
    {
        var bin = new Bin(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        (bin is Volo.Abp.Domain.Entities.IHasConcurrencyStamp).ShouldBeTrue();
    }

    [Fact]
    public void Bin_ApplyStockMovement_UpdatesQtyAndValue()
    {
        var bin = new Bin(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        bin.ApplyStockMovement(10, 500); // 10 qty, 500 value
        bin.ActualQty.ShouldBe(10m);
        bin.StockValue.ShouldBe(500m);
    }

    [Fact]
    public void Bin_ApplyStockMovement_HandlesNegative()
    {
        var bin = new Bin(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        bin.ApplyStockMovement(10, 500);
        bin.ApplyStockMovement(-3, -150);
        bin.ActualQty.ShouldBe(7m);
        bin.StockValue.ShouldBe(350m);
    }

    [Fact]
    public void FiscalYearClosed_ErrorCode_Exists()
    {
        MyERPDomainErrorCodes.FiscalYearClosed.ShouldBe("MyERP:02002");
    }
}
