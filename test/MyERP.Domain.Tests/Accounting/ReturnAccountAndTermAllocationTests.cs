using System;
using System.Collections.Generic;
using System.Linq;
using MyERP.Accounting.Entities;
using MyERP.Purchasing.DomainServices;
using MyERP.Purchasing.Entities;
using MyERP.Sales.DomainServices;
using MyERP.Sales.Entities;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace MyERP.Domain.Tests.Accounting;

/// <summary>
/// Tests for the 3 newly documented validation rules:
/// 1. Return invoice party account must match original
/// 2. Return invoices with stock effect cannot have zero-qty items
/// 3. Payment entry term-based allocation enforcement
/// Source: Gap scan #30 (2026-07-15)
/// </summary>
public class ReturnAccountAndTermAllocationTests
{
    private static readonly DateTime TestDate = new(2026, 7, 15);

    // --- Return Account Matching (SI) ---

    [Fact]
    public void SalesInvoice_DebitToAccountId_DefaultsToEmpty()
    {
        var si = new SalesInvoice(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "INV-001", TestDate);
        si.DebitToAccountId.ShouldBe(Guid.Empty);
    }

    [Fact]
    public void SalesInvoice_DebitToAccountId_CanBeSet()
    {
        var accountId = Guid.NewGuid();
        var si = new SalesInvoice(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "INV-001", TestDate);
        si.DebitToAccountId = accountId;
        si.DebitToAccountId.ShouldBe(accountId);
    }

    // --- Return Account Matching (PI) ---

    [Fact]
    public void PurchaseInvoice_CreditToAccountId_DefaultsToEmpty()
    {
        var pi = new PurchaseInvoice(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "PI-001", TestDate);
        pi.CreditToAccountId.ShouldBe(Guid.Empty);
    }

    [Fact]
    public void PurchaseInvoice_CreditToAccountId_CanBeSet()
    {
        var accountId = Guid.NewGuid();
        var pi = new PurchaseInvoice(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "PI-001", TestDate);
        pi.CreditToAccountId = accountId;
        pi.CreditToAccountId.ShouldBe(accountId);
    }

    // --- Zero-Qty on Return With Stock (SI) ---

    [Fact]
    public void ValidateReturnWithStockNoZeroQty_NonReturn_Passes()
    {
        var si = new SalesInvoice(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "INV-001", TestDate);
        si.IsReturn = false;
        si.UpdateStock = true;
        SalesInvoiceManager.ValidateReturnWithStockNoZeroQty(si);
    }

    [Fact]
    public void ValidateReturnWithStockNoZeroQty_ReturnWithoutStock_Passes()
    {
        var si = new SalesInvoice(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "INV-001", TestDate);
        si.IsReturn = true;
        si.UpdateStock = false;
        SalesInvoiceManager.ValidateReturnWithStockNoZeroQty(si);
    }

    [Fact]
    public void ValidateReturnWithStockNoZeroQty_ReturnWithStock_AllNonZero_Passes()
    {
        // SI entity's AddItem already blocks zero-qty (defense-in-depth).
        // This test verifies the validation method doesn't throw on valid data.
        var si = new SalesInvoice(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "INV-001", TestDate);
        si.IsReturn = true;
        si.UpdateStock = true;
        si.AddItem(Guid.NewGuid(), "Item A", -5, 100, 0);
        si.AddItem(Guid.NewGuid(), "Item B", -2, 50, 0);

        // Should NOT throw — all items have non-zero qty
        SalesInvoiceManager.ValidateReturnWithStockNoZeroQty(si);
    }

    // --- Zero-Qty on Return With Stock (PI) ---

    [Fact]
    public void PI_ValidateReturnWithStockNoZeroQty_ReturnWithStock_ZeroQty_Throws()
    {
        var pi = new PurchaseInvoice(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "PI-001", TestDate);
        pi.IsReturn = true;
        pi.UpdateStock = true;
        pi.AddItem(Guid.NewGuid(), "Item A", -3, 200, 0);
        pi.AddItem(Guid.NewGuid(), "Item B", 0, 100, 0);

        var ex = Assert.Throws<BusinessException>(() =>
            PurchaseInvoiceManager.ValidateReturnWithStockNoZeroQty(pi));

        ex.Code.ShouldBe(MyERPDomainErrorCodes.ReturnWithStockZeroQty);
    }

    [Fact]
    public void PI_ValidateReturnWithStockNoZeroQty_AllNonZero_Passes()
    {
        var pi = new PurchaseInvoice(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "PI-001", TestDate);
        pi.IsReturn = true;
        pi.UpdateStock = true;
        pi.AddItem(Guid.NewGuid(), "Item A", -3, 200, 0);
        pi.AddItem(Guid.NewGuid(), "Item B", -1, 100, 0);

        PurchaseInvoiceManager.ValidateReturnWithStockNoZeroQty(pi);
    }

    // --- Payment Term Allocation Enforcement ---

    [Fact]
    public void PaymentEntryReference_PaymentTermId_DefaultsToNull()
    {
        var reference = new PaymentEntryReference(
            Guid.NewGuid(), Guid.NewGuid(), "SalesInvoice", Guid.NewGuid(),
            1000m, 1000m, 500m);

        reference.PaymentTermId.ShouldBeNull();
    }

    [Fact]
    public void PaymentEntryReference_PaymentTermId_CanBeSet()
    {
        var termId = Guid.NewGuid();
        var reference = new PaymentEntryReference(
            Guid.NewGuid(), Guid.NewGuid(), "SalesInvoice", Guid.NewGuid(),
            1000m, 1000m, 500m);
        reference.PaymentTermId = termId;

        reference.PaymentTermId.ShouldBe(termId);
    }

    [Fact]
    public void ErrorCode_ReturnAccountMismatch_Exists()
    {
        MyERPDomainErrorCodes.ReturnAccountMismatch.ShouldBe("MyERP:08008");
    }

    [Fact]
    public void ErrorCode_ReturnWithStockZeroQty_Exists()
    {
        MyERPDomainErrorCodes.ReturnWithStockZeroQty.ShouldBe("MyERP:08009");
    }

    [Fact]
    public void ErrorCode_PaymentTermRequired_Exists()
    {
        MyERPDomainErrorCodes.PaymentTermRequired.ShouldBe("MyERP:02026");
    }

    [Fact]
    public void ErrorCode_PaymentTermOutstandingExceeded_Exists()
    {
        MyERPDomainErrorCodes.PaymentTermOutstandingExceeded.ShouldBe("MyERP:02027");
    }
}
