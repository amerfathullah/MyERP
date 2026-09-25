using System;
using MyERP.Core;
using MyERP.Purchasing.Entities;
using MyERP.Sales.Entities;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace MyERP.Domain.Tests.Sales;

/// <summary>
/// Unit tests for Quotation revisions and Purchase Order remarks (ERPNext PR #59378 & #59420).
/// </summary>
public class QuotationRevisionTests
{
    private readonly Guid _companyId = Guid.NewGuid();
    private readonly Guid _customerId = Guid.NewGuid();

    [Fact]
    public void Quotation_Defaults_AreActiveWithZeroRevisionIndex()
    {
        var quotation = new Quotation(Guid.NewGuid(), _companyId, _customerId, "QTN-001", DateTime.UtcNow);

        quotation.IsActive.ShouldBeTrue();
        quotation.RevisionIndex.ShouldBe(0);
        quotation.RevisionOfId.ShouldBeNull();
    }

    [Fact]
    public void Quotation_DeactivateAndActivate_TogglesIsActive()
    {
        var quotation = new Quotation(Guid.NewGuid(), _companyId, _customerId, "QTN-001", DateTime.UtcNow);

        quotation.Deactivate();
        quotation.IsActive.ShouldBeFalse();

        quotation.Activate();
        quotation.IsActive.ShouldBeTrue();
    }

    [Fact]
    public void PurchaseOrder_Remarks_CanBeAssigned()
    {
        var po = new PurchaseOrder(Guid.NewGuid(), _companyId, Guid.NewGuid(), "PO-001", DateTime.UtcNow);
        po.Remarks = "Special handling required for fragile items.";

        po.Remarks.ShouldBe("Special handling required for fragile items.");
    }
}
