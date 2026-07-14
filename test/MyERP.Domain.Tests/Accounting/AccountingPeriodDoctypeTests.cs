using System;
using MyERP.Accounting.Entities;
using Shouldly;
using Xunit;

namespace MyERP.Accounting;

/// <summary>
/// Tests for per-doctype accounting period closure.
/// Per ERPNext: each accounting period can close specific document types
/// while leaving others open. The exempted role allows bypass.
/// </summary>
public class AccountingPeriodDoctypeTests
{
    [Fact]
    public void IsClosedForDocumentType_WhenNotClosed_ReturnsFalse()
    {
        var period = CreatePeriod();
        // Not closed at all
        period.IsClosedForDocumentType("SalesInvoice").ShouldBeFalse();
    }

    [Fact]
    public void IsClosedForDocumentType_BlanketClosure_BlocksAllTypes()
    {
        var period = CreatePeriod();
        period.Close();
        // No ClosedDocumentTypes specified → blanket closure
        period.IsClosedForDocumentType("SalesInvoice").ShouldBeTrue();
        period.IsClosedForDocumentType("JournalEntry").ShouldBeTrue();
        period.IsClosedForDocumentType("PaymentEntry").ShouldBeTrue();
    }

    [Fact]
    public void CloseDocumentType_OnlyBlocksSpecificType()
    {
        var period = CreatePeriod();
        period.CloseDocumentType("SalesInvoice");

        period.IsClosed.ShouldBeTrue();
        period.IsClosedForDocumentType("SalesInvoice").ShouldBeTrue();
        period.IsClosedForDocumentType("PurchaseInvoice").ShouldBeFalse();
        period.IsClosedForDocumentType("JournalEntry").ShouldBeFalse();
    }

    [Fact]
    public void CloseDocumentType_MultipleTypes()
    {
        var period = CreatePeriod();
        period.CloseDocumentType("SalesInvoice");
        period.CloseDocumentType("PurchaseInvoice");
        period.CloseDocumentType("JournalEntry");

        period.IsClosedForDocumentType("SalesInvoice").ShouldBeTrue();
        period.IsClosedForDocumentType("PurchaseInvoice").ShouldBeTrue();
        period.IsClosedForDocumentType("JournalEntry").ShouldBeTrue();
        period.IsClosedForDocumentType("PaymentEntry").ShouldBeFalse();
    }

    [Fact]
    public void CloseDocumentType_CaseInsensitive()
    {
        var period = CreatePeriod();
        period.CloseDocumentType("SalesInvoice");

        period.IsClosedForDocumentType("salesinvoice").ShouldBeTrue();
        period.IsClosedForDocumentType("SALESINVOICE").ShouldBeTrue();
    }

    [Fact]
    public void CloseDocumentType_NoDuplicates()
    {
        var period = CreatePeriod();
        period.CloseDocumentType("SalesInvoice");
        period.CloseDocumentType("SalesInvoice"); // duplicate

        period.ClosedDocumentTypes.ShouldBe("SalesInvoice");
    }

    [Fact]
    public void ReopenDocumentType_RemovesFromList()
    {
        var period = CreatePeriod();
        period.CloseDocumentType("SalesInvoice");
        period.CloseDocumentType("PurchaseInvoice");

        period.ReopenDocumentType("SalesInvoice");

        period.IsClosedForDocumentType("SalesInvoice").ShouldBeFalse();
        period.IsClosedForDocumentType("PurchaseInvoice").ShouldBeTrue();
        period.IsClosed.ShouldBeTrue(); // still closed for PI
    }

    [Fact]
    public void ReopenDocumentType_LastType_ReopensPeriod()
    {
        var period = CreatePeriod();
        period.CloseDocumentType("SalesInvoice");

        period.ReopenDocumentType("SalesInvoice");

        period.IsClosed.ShouldBeFalse();
        period.ClosedDocumentTypes.ShouldBeNull();
    }

    [Fact]
    public void ExemptedRole_DefaultsNull()
    {
        var period = CreatePeriod();
        period.ExemptedRole.ShouldBeNull();
    }

    [Fact]
    public void ExemptedRole_CanBeSet()
    {
        var period = CreatePeriod();
        period.ExemptedRole = "Accounts Manager";
        period.ExemptedRole.ShouldBe("Accounts Manager");
    }

    [Fact]
    public void ClosedDocumentTypes_DefaultsNull()
    {
        var period = CreatePeriod();
        period.ClosedDocumentTypes.ShouldBeNull();
    }

    private static AccountingPeriod CreatePeriod()
    {
        return new AccountingPeriod(Guid.NewGuid(), Guid.NewGuid(),
            "January 2026", new DateTime(2026, 1, 1), new DateTime(2026, 1, 31));
    }
}
