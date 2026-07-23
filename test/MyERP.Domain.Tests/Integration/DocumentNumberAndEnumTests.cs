using System;
using System.Collections.Generic;
using System.Linq;
using MyERP.Inventory;
using MyERP.Inventory.Entities;
using MyERP.Purchasing.Entities;
using Shouldly;
using Xunit;

namespace MyERP.Integration;

/// <summary>
/// Tests for document number assignment and stock entry type enum mapping fixes.
/// </summary>
public class DocumentNumberAndEnumTests
{
    // === QualityInspection Number ===

    [Fact]
    public void QualityInspection_HasInspectionNumberProperty()
    {
        var qi = new QualityInspection(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            InspectionType.Incoming, DateTime.Today, Guid.NewGuid());

        qi.InspectionNumber.ShouldBeNull(); // null before assignment
        qi.InspectionNumber = "QI-2026-00001";
        qi.InspectionNumber.ShouldBe("QI-2026-00001");
    }

    [Fact]
    public void QualityInspection_DefaultDraftStatus()
    {
        var qi = new QualityInspection(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            InspectionType.Outgoing, DateTime.Today, Guid.NewGuid());

        qi.Status.ShouldBe(InspectionStatus.Draft);
    }

    // === SupplierQuotation Number ===

    [Fact]
    public void SupplierQuotation_HasQuotationNumberProperty()
    {
        var sq = new SupplierQuotation(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            DateTime.Today, Guid.NewGuid());

        sq.QuotationNumber.ShouldBeNull();
        sq.QuotationNumber = "SQ-2026-00001";
        sq.QuotationNumber.ShouldBe("SQ-2026-00001");
    }

    [Fact]
    public void SupplierQuotation_CanAddItems()
    {
        var sq = new SupplierQuotation(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            DateTime.Today, Guid.NewGuid());
        sq.AddItem(Guid.NewGuid(), 10, 25, "Steel Bolts");

        sq.Items.Count.ShouldBe(1);
        sq.NetTotal.ShouldBe(250m);
    }

    // === Stock Entry Type Enum Values ===

    [Fact]
    public void StockEntryType_MaterialReceipt_Is_0()
    {
        ((int)StockEntryType.MaterialReceipt).ShouldBe(0);
    }

    [Fact]
    public void StockEntryType_MaterialIssue_Is_1()
    {
        ((int)StockEntryType.MaterialIssue).ShouldBe(1);
    }

    [Fact]
    public void StockEntryType_MaterialTransfer_Is_2()
    {
        ((int)StockEntryType.MaterialTransfer).ShouldBe(2);
    }

    [Fact]
    public void StockEntryType_Manufacture_Is_4()
    {
        ((int)StockEntryType.Manufacture).ShouldBe(4);
    }

    [Fact]
    public void StockEntryType_AllValues_AreDistinct()
    {
        var values = Enum.GetValues<StockEntryType>();
        var distinct = new HashSet<int>(values.Select(v => (int)v));
        distinct.Count.ShouldBe(values.Length);
    }

    [Fact]
    public void StockEntryType_Has14Members()
    {
        Enum.GetValues<StockEntryType>().Length.ShouldBe(14);
    }
}
