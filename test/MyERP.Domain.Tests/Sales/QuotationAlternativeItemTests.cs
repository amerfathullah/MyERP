using System;
using MyERP.Sales.Entities;
using Shouldly;
using Xunit;

namespace MyERP.Sales;

public class QuotationAlternativeItemTests
{
    private static Quotation NewQuotation()
        => new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "QTN-ALT-1", DateTime.UtcNow.Date);

    [Fact]
    public void AlternativeRows_AreExcludedFromTotals()
    {
        var q = NewQuotation();
        q.AddItem(Guid.NewGuid(), "Primary", 1, 100m, 6m);
        q.AddItem(Guid.NewGuid(), "Alternative", 1, 90m, 5m, isAlternative: true);

        q.NetTotal.ShouldBe(100m);
        q.TaxAmount.ShouldBe(6m);
        q.GrandTotal.ShouldBe(106m);
    }

    [Fact]
    public void Submit_FlagsRowFollowedByAlternative()
    {
        var q = NewQuotation();
        q.AddItem(Guid.NewGuid(), "Primary", 1, 100m, 0m);
        q.AddItem(Guid.NewGuid(), "Alternative", 1, 90m, 0m, isAlternative: true);
        q.AddItem(Guid.NewGuid(), "Plain", 1, 10m, 0m);
        q.Submit();

        q.Items[0].HasAlternativeItem.ShouldBeTrue();
        q.Items[1].HasAlternativeItem.ShouldBeFalse();
        q.Items[2].HasAlternativeItem.ShouldBeFalse();
    }

    [Fact]
    public void PerOrdered_IgnoresUnorderedAlternativeSet()
    {
        var q = NewQuotation();
        q.AddItem(Guid.NewGuid(), "Plain", 1, 10m, 0m);
        q.AddItem(Guid.NewGuid(), "Primary", 1, 100m, 0m);
        q.AddItem(Guid.NewGuid(), "Alternative", 1, 90m, 0m, isAlternative: true);
        q.Submit();

        // Only the plain row is ordered; the primary/alternative set was never ordered.
        q.Items[0].OrderedQty = 1;
        q.PerOrdered.ShouldBe(100m);
        q.OrderStatus.ShouldBe("Ordered");
    }
}
