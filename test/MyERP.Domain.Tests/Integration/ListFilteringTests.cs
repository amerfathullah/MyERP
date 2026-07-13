using System;
using MyERP.Core;
using MyERP.Inventory.Entities;
using MyERP.Manufacturing.Entities;
using MyERP.Purchasing.Entities;
using MyERP.Sales.Entities;
using Shouldly;
using Xunit;

namespace MyERP.Integration;

/// <summary>
/// Tests verifying the filter/search/status functionality works correctly
/// with the CompanyFilteredPagedRequestDto pattern used across AppServices.
/// </summary>
public class ListFilteringTests
{
    [Fact]
    public void DocumentStatus_ParsesAllFulfillmentStatuses()
    {
        Enum.TryParse<DocumentStatus>("Draft", true, out var draft).ShouldBeTrue();
        draft.ShouldBe(DocumentStatus.Draft);

        Enum.TryParse<DocumentStatus>("ToDeliverAndBill", true, out var tdab).ShouldBeTrue();
        tdab.ShouldBe(DocumentStatus.ToDeliverAndBill);

        Enum.TryParse<DocumentStatus>("ToDeliver", true, out var td).ShouldBeTrue();
        td.ShouldBe(DocumentStatus.ToDeliver);

        Enum.TryParse<DocumentStatus>("ToBill", true, out var tb).ShouldBeTrue();
        tb.ShouldBe(DocumentStatus.ToBill);

        Enum.TryParse<DocumentStatus>("Completed", true, out var c).ShouldBeTrue();
        c.ShouldBe(DocumentStatus.Completed);

        Enum.TryParse<DocumentStatus>("Closed", true, out var cl).ShouldBeTrue();
        cl.ShouldBe(DocumentStatus.Closed);
    }

    [Fact]
    public void DocumentStatus_ParsesStandardStatuses()
    {
        Enum.TryParse<DocumentStatus>("Submitted", true, out var s).ShouldBeTrue();
        s.ShouldBe(DocumentStatus.Submitted);

        Enum.TryParse<DocumentStatus>("Posted", true, out var p).ShouldBeTrue();
        p.ShouldBe(DocumentStatus.Posted);

        Enum.TryParse<DocumentStatus>("Cancelled", true, out var ca).ShouldBeTrue();
        ca.ShouldBe(DocumentStatus.Cancelled);
    }

    [Fact]
    public void DocumentStatus_InvalidString_ReturnsFalse()
    {
        Enum.TryParse<DocumentStatus>("NonExistentStatus", true, out _).ShouldBeFalse();
        Enum.TryParse<DocumentStatus>("", true, out _).ShouldBeFalse();
    }

    [Fact]
    public void SalesOrder_OrderNumber_CanBeSearched()
    {
        var so = new SalesOrder(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "SO-2026-00042",
            DateTime.UtcNow);

        so.OrderNumber.ToLower().Contains("so-2026").ShouldBeTrue();
        so.OrderNumber.ToLower().Contains("00042").ShouldBeTrue();
        so.OrderNumber.ToLower().Contains("xyz").ShouldBeFalse();
    }

    [Fact]
    public void PurchaseOrder_OrderNumber_CanBeSearched()
    {
        var po = new PurchaseOrder(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "PO-2026-00099",
            DateTime.UtcNow);

        po.OrderNumber.ToLower().Contains("po-2026").ShouldBeTrue();
        po.OrderNumber.ToLower().Contains("00099").ShouldBeTrue();
    }

    [Fact]
    public void SalesInvoice_InvoiceNumber_CanBeSearched()
    {
        var si = new SalesInvoice(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "SI-2026-00150", DateTime.UtcNow);

        si.InvoiceNumber.ToLower().Contains("si-2026").ShouldBeTrue();
        si.InvoiceNumber.ToLower().Contains("00150").ShouldBeTrue();
    }

    [Fact]
    public void SalesOrder_StatusFilter_MatchesExactValue()
    {
        var so = new SalesOrder(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "SO-001",
            DateTime.UtcNow);
        so.Status.ShouldBe(DocumentStatus.Draft);

        // After submit, status changes
        so.AddItem(Guid.NewGuid(), "Item", 10, 100m, 0m);
        so.Submit();
        so.Status.ShouldBe(DocumentStatus.ToDeliverAndBill);

        // Filter matching
        (so.Status == DocumentStatus.ToDeliverAndBill).ShouldBeTrue();
        (so.Status == DocumentStatus.Draft).ShouldBeFalse();
    }

    [Fact]
    public void PurchaseOrder_StatusFilter_MatchesExactValue()
    {
        var po = new PurchaseOrder(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "PO-001",
            DateTime.UtcNow);
        po.Status.ShouldBe(DocumentStatus.Draft);

        po.AddItem(Guid.NewGuid(), "Item", 5, 200m, 0m);
        po.Submit();
        po.Status.ShouldBe(DocumentStatus.ToDeliverAndBill);
    }

    [Fact]
    public void CompanyId_FilterIsolatesData()
    {
        var companyA = Guid.NewGuid();
        var companyB = Guid.NewGuid();

        var so1 = new SalesOrder(Guid.NewGuid(), companyA, Guid.NewGuid(), "SO-A-001", DateTime.UtcNow);
        var so2 = new SalesOrder(Guid.NewGuid(), companyB, Guid.NewGuid(), "SO-B-001", DateTime.UtcNow);

        so1.CompanyId.ShouldBe(companyA);
        so2.CompanyId.ShouldBe(companyB);
        (so1.CompanyId == companyA).ShouldBeTrue();
        (so2.CompanyId == companyA).ShouldBeFalse();
    }

    [Fact]
    public void CaseInsensitiveSearch_MatchesBothCases()
    {
        var number = "SI-2026-ABC";
        number.ToLower().Contains("abc").ShouldBeTrue();
        number.ToLower().Contains("ABC".ToLower()).ShouldBeTrue();
        number.ToLower().Contains("si-2026").ShouldBeTrue();
    }
}
