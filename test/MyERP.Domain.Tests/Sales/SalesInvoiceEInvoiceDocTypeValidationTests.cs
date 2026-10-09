using System;
using MyERP.Purchasing.Entities;
using MyERP.Sales;
using MyERP.Sales.Entities;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace MyERP.Domain.Tests.Sales;

/// <summary>
/// Unit tests for LHDN MyInvois e-Invoice document type validation rules
/// migrated from myinvois PR #73 (commit 37d716d).
/// </summary>
public class SalesInvoiceEInvoiceDocTypeValidationTests
{
    private static SalesInvoice CreateInvoice() =>
        new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "INV-001", DateTime.UtcNow);

    private static PurchaseInvoice CreatePurchaseInvoice() =>
        new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "PINV-001", DateTime.UtcNow);

    [Fact]
    public void ValidateEInvoiceDocumentType_Throws_WhenDebitNoteWithNonDebitNoteType()
    {
        var inv = CreateInvoice();
        inv.IsDebitNote = true;
        inv.EInvoiceDocType = EInvoiceDocumentType.CreditNote;

        var ex = Should.Throw<BusinessException>(() => inv.ValidateEInvoiceDocumentType());
        ex.Data["detail"]!.ToString()!.ShouldContain("03 : Debit Note");
    }

    [Fact]
    public void ValidateEInvoiceDocumentType_Passes_WhenDebitNoteWithDebitNoteType()
    {
        var inv = CreateInvoice();
        inv.IsDebitNote = true;
        inv.EInvoiceDocType = EInvoiceDocumentType.DebitNote;

        inv.ValidateEInvoiceDocumentType();
    }

    [Fact]
    public void ValidateEInvoiceDocumentType_Throws_WhenReturnWithNonCreditNoteType()
    {
        var inv = CreateInvoice();
        inv.IsReturn = true;
        inv.IsReturnRefund = false;
        inv.EInvoiceDocType = EInvoiceDocumentType.Invoice;

        var ex = Should.Throw<BusinessException>(() => inv.ValidateEInvoiceDocumentType());
        ex.Data["detail"]!.ToString()!.ShouldContain("02 : Credit Note");
    }

    [Fact]
    public void ValidateEInvoiceDocumentType_Passes_WhenReturnWithCreditNoteType()
    {
        var inv = CreateInvoice();
        inv.IsReturn = true;
        inv.IsReturnRefund = false;
        inv.EInvoiceDocType = EInvoiceDocumentType.CreditNote;

        inv.ValidateEInvoiceDocumentType();
    }

    [Fact]
    public void ValidateEInvoiceDocumentType_Throws_WhenReturnRefundWithNonRefundNoteType()
    {
        var inv = CreateInvoice();
        inv.IsReturn = true;
        inv.IsReturnRefund = true;
        inv.EInvoiceDocType = EInvoiceDocumentType.CreditNote;

        var ex = Should.Throw<BusinessException>(() => inv.ValidateEInvoiceDocumentType());
        ex.Data["detail"]!.ToString()!.ShouldContain("04 : Refund Note");
    }

    [Fact]
    public void ValidateEInvoiceDocumentType_Passes_WhenReturnRefundWithRefundNoteType()
    {
        var inv = CreateInvoice();
        inv.IsReturn = true;
        inv.IsReturnRefund = true;
        inv.EInvoiceDocType = EInvoiceDocumentType.RefundNote;

        inv.ValidateEInvoiceDocumentType();
    }

    [Fact]
    public void ValidateEInvoiceDocumentType_Throws_WhenStandardInvoiceWithCreditOrDebitOrRefundType()
    {
        var inv = CreateInvoice();
        inv.IsReturn = false;
        inv.IsDebitNote = false;
        inv.EInvoiceDocType = EInvoiceDocumentType.CreditNote;

        var ex = Should.Throw<BusinessException>(() => inv.ValidateEInvoiceDocumentType());
        ex.Data["detail"]!.ToString()!.ShouldContain("Standard Sales Invoice cannot use Credit Note");
    }

    [Fact]
    public void ValidateEInvoiceDocumentType_Passes_WhenStandardInvoiceWithInvoiceType()
    {
        var inv = CreateInvoice();
        inv.IsReturn = false;
        inv.IsDebitNote = false;
        inv.EInvoiceDocType = EInvoiceDocumentType.Invoice;

        inv.ValidateEInvoiceDocumentType();
    }

    [Fact]
    public void PurchaseInvoice_ValidateEInvoiceDocumentType_EnforcesRules()
    {
        var pi = CreatePurchaseInvoice();
        pi.IsReturn = true;
        pi.IsReturnRefund = false;
        pi.EInvoiceDocType = EInvoiceDocumentType.Invoice;

        var ex = Should.Throw<BusinessException>(() => pi.ValidateEInvoiceDocumentType());
        ex.Data["detail"]!.ToString()!.ShouldContain("Self-Billed Credit Note");

        pi.EInvoiceDocType = EInvoiceDocumentType.SelfBilledCreditNote;
        pi.ValidateEInvoiceDocumentType();

        pi.IsReturnRefund = true;
        var ex2 = Should.Throw<BusinessException>(() => pi.ValidateEInvoiceDocumentType());
        ex2.Data["detail"]!.ToString()!.ShouldContain("Self-Billed Refund Note");

        pi.EInvoiceDocType = EInvoiceDocumentType.SelfBilledRefundNote;
        pi.ValidateEInvoiceDocumentType();
    }
}
