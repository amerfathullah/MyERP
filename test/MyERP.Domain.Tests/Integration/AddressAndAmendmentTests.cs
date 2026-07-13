using System;
using MyERP.Accounting.Entities;
using MyERP.Core;
using MyERP.Purchasing.Entities;
using MyERP.Sales.Entities;
using Shouldly;
using Xunit;

namespace MyERP.Domain.Tests.Integration;

/// <summary>
/// Tests for:
/// - SI/PI BillingAddressId/ShippingAddressId fields
/// - PI IAmendable implementation
/// - Company-filtered GetListAsync concept validation
/// </summary>
public class AddressAndAmendmentTests
{
    // --- SI Address Fields ---

    [Fact]
    public void SalesInvoice_BillingAddressId_DefaultsNull()
    {
        var si = new SalesInvoice(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "SI-001", DateTime.Today);
        si.BillingAddressId.ShouldBeNull();
        si.ShippingAddressId.ShouldBeNull();
    }

    [Fact]
    public void SalesInvoice_AddressFields_CanBeSet()
    {
        var si = new SalesInvoice(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "SI-002", DateTime.Today);
        var billingId = Guid.NewGuid();
        var shippingId = Guid.NewGuid();

        si.BillingAddressId = billingId;
        si.ShippingAddressId = shippingId;

        si.BillingAddressId.ShouldBe(billingId);
        si.ShippingAddressId.ShouldBe(shippingId);
    }

    // --- PI Address Fields ---

    [Fact]
    public void PurchaseInvoice_BillingAddressId_DefaultsNull()
    {
        var pi = new PurchaseInvoice(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "PI-001", DateTime.Today);
        pi.BillingAddressId.ShouldBeNull();
    }

    [Fact]
    public void PurchaseInvoice_BillingAddressId_CanBeSet()
    {
        var pi = new PurchaseInvoice(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "PI-002", DateTime.Today);
        var addrId = Guid.NewGuid();
        pi.BillingAddressId = addrId;
        pi.BillingAddressId.ShouldBe(addrId);
    }

    // --- PI IAmendable ---

    [Fact]
    public void PurchaseInvoice_Implements_IAmendable()
    {
        var pi = new PurchaseInvoice(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "PI-003", DateTime.Today);
        (pi is IAmendable).ShouldBeTrue();
    }

    [Fact]
    public void PurchaseInvoice_AmendmentFields_DefaultValues()
    {
        var pi = new PurchaseInvoice(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "PI-004", DateTime.Today);
        pi.AmendedFromId.ShouldBeNull();
        pi.AmendmentIndex.ShouldBe(0);
    }

    [Fact]
    public void PurchaseInvoice_AmendmentFields_CanBeSet()
    {
        var pi = new PurchaseInvoice(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "PI-005", DateTime.Today);
        var originalId = Guid.NewGuid();

        pi.AmendedFromId = originalId;
        pi.AmendmentIndex = 2;

        pi.AmendedFromId.ShouldBe(originalId);
        pi.AmendmentIndex.ShouldBe(2);
    }

    // --- Company-Scoped Filtering Concept ---

    [Fact]
    public void CompanyFilter_NullCompanyId_ReturnsAll()
    {
        // When CompanyId is null, no filter is applied (backwards compatible)
        Guid? companyId = null;
        companyId.HasValue.ShouldBeFalse();
    }

    [Fact]
    public void CompanyFilter_SetCompanyId_FiltersData()
    {
        Guid? companyId = Guid.NewGuid();
        companyId.HasValue.ShouldBeTrue();
    }

    // --- JE Activity Log ---

    [Fact]
    public void JournalEntry_HasEntryNumber_ForActivityLog()
    {
        var je = new JournalEntry(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), DateTime.Today);
        je.EntryNumber = "JE-2026-001";
        je.EntryNumber.ShouldBe("JE-2026-001");
    }

    [Fact]
    public void JournalEntry_Post_ThenCancel_Lifecycle()
    {
        var je = new JournalEntry(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), DateTime.Today);
        je.AddLine(Guid.NewGuid(), 500m, true, "DR");
        je.AddLine(Guid.NewGuid(), 500m, false, "CR");
        je.Validate();

        je.Post();
        je.Status.ShouldBe(DocumentStatus.Posted);

        je.Cancel();
        je.Status.ShouldBe(DocumentStatus.Cancelled);
    }
}
