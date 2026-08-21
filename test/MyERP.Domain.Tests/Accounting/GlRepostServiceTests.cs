using System;
using MyERP.Accounting.DomainServices;
using Shouldly;
using Xunit;

namespace MyERP.Accounting;

public class GlRepostServiceTests
{
    [Fact]
    public void AllowedVoucherTypes_ContainsCriticalTypes()
    {
        GlRepostService.IsRepostAllowed("SalesInvoice").ShouldBeTrue();
        GlRepostService.IsRepostAllowed("PurchaseInvoice").ShouldBeTrue();
        GlRepostService.IsRepostAllowed("JournalEntry").ShouldBeTrue();
        GlRepostService.IsRepostAllowed("PurchaseReceipt").ShouldBeTrue();
        GlRepostService.IsRepostAllowed("DeliveryNote").ShouldBeTrue();
        GlRepostService.IsRepostAllowed("StockEntry").ShouldBeTrue();
    }

    [Fact]
    public void AllowedVoucherTypes_ExcludesPaymentEntry()
    {
        // Removed deliberately: PE's GL repost would need to also reverse+rebuild PLE across
        // potentially multiple allocated references and re-derive its exchange-gain/loss JE — see
        // GlRepostService.AllowedVoucherTypes's doc comment. Silently reposting only the main GL
        // while leaving PLE stale would be worse than not supporting repost for it at all.
        GlRepostService.IsRepostAllowed("PaymentEntry").ShouldBeFalse();
    }

    [Fact]
    public void AllowedVoucherTypes_RejectsNonWhitelisted()
    {
        GlRepostService.IsRepostAllowed("Quotation").ShouldBeFalse();
        GlRepostService.IsRepostAllowed("SalesOrder").ShouldBeFalse();
        GlRepostService.IsRepostAllowed("PurchaseOrder").ShouldBeFalse();
        GlRepostService.IsRepostAllowed("MaterialRequest").ShouldBeFalse();
        GlRepostService.IsRepostAllowed("").ShouldBeFalse();
    }

    [Fact]
    public void AllowedVoucherTypes_CaseInsensitive()
    {
        GlRepostService.IsRepostAllowed("salesinvoice").ShouldBeTrue();
        GlRepostService.IsRepostAllowed("PURCHASEINVOICE").ShouldBeTrue();
        GlRepostService.IsRepostAllowed("stockentry").ShouldBeTrue();
    }

    [Fact]
    public void GlRepostResult_TotalProcessed_SumsAllCategories()
    {
        var result = new GlRepostResult(5, 2, 1, new() { "error1" });
        result.TotalProcessed.ShouldBe(8);
        result.HasErrors.ShouldBeTrue();
    }

    [Fact]
    public void GlRepostResult_NoErrors_HasErrorsFalse()
    {
        var result = new GlRepostResult(10, 0, 0, new());
        result.HasErrors.ShouldBeFalse();
    }

    [Fact]
    public void GlRepostResult_ZeroProcessed()
    {
        var result = new GlRepostResult(0, 0, 0, new());
        result.TotalProcessed.ShouldBe(0);
        result.HasErrors.ShouldBeFalse();
    }

    [Fact]
    public void AllowedVoucherTypes_Has6Types()
    {
        GlRepostService.AllowedVoucherTypes.Count.ShouldBe(6);
    }
}
