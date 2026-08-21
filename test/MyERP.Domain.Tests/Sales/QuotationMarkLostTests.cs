using System;
using MyERP.Core;
using MyERP.Sales.Entities;
using Volo.Abp;
using Xunit;

namespace MyERP.Domain.Tests.Sales;

/// <summary>
/// Unit tests for Quotation.MarkLost() invariants (Gotcha #2142):
/// 1. Submitted quotation with no SO can be marked as Lost (Rejected).
/// 2. Draft quotation cannot be marked as Lost (throws InvalidStatusTransition).
/// 3. Quotation converted to Sales Order cannot be marked as Lost (throws ValidationFailed).
/// 4. Quotation with partial ordered qty cannot be marked as Lost (throws ValidationFailed).
/// </summary>
public class QuotationMarkLostTests
{
    private readonly Guid _companyId = Guid.NewGuid();
    private readonly Guid _customerId = Guid.NewGuid();

    [Fact]
    public void MarkLost_SubmittedWithNoOrders_SetsStatusToRejected()
    {
        var quotation = new Quotation(Guid.NewGuid(), _companyId, _customerId, "QTN-001", DateTime.UtcNow);
        quotation.AddItem(Guid.NewGuid(), "Item A", 5m, 100m, 0m);
        quotation.Submit();

        quotation.MarkLost();

        Assert.Equal(DocumentStatus.Rejected, quotation.Status);
    }

    [Fact]
    public void MarkLost_Draft_ThrowsInvalidStatusTransition()
    {
        var quotation = new Quotation(Guid.NewGuid(), _companyId, _customerId, "QTN-001", DateTime.UtcNow);
        quotation.AddItem(Guid.NewGuid(), "Item A", 5m, 100m, 0m);

        var ex = Assert.Throws<BusinessException>(() => quotation.MarkLost());
        Assert.Equal(MyERPDomainErrorCodes.InvalidStatusTransition, ex.Code);
    }

    [Fact]
    public void MarkLost_AlreadyConvertedToSalesOrder_ThrowsValidationFailed()
    {
        var quotation = new Quotation(Guid.NewGuid(), _companyId, _customerId, "QTN-001", DateTime.UtcNow);
        quotation.AddItem(Guid.NewGuid(), "Item A", 5m, 100m, 0m);
        quotation.Submit();
        quotation.ConvertedToSalesOrderId = Guid.NewGuid();

        var ex = Assert.Throws<BusinessException>(() => quotation.MarkLost());
        Assert.Equal(MyERPDomainErrorCodes.ValidationFailed, ex.Code);
    }

    [Fact]
    public void MarkLost_PartiallyOrderedItem_ThrowsValidationFailed()
    {
        var quotation = new Quotation(Guid.NewGuid(), _companyId, _customerId, "QTN-001", DateTime.UtcNow);
        quotation.AddItem(Guid.NewGuid(), "Item A", 5m, 100m, 0m);
        quotation.Submit();
        quotation.Items[0].OrderedQty = 2m;

        var ex = Assert.Throws<BusinessException>(() => quotation.MarkLost());
        Assert.Equal(MyERPDomainErrorCodes.ValidationFailed, ex.Code);
    }
}
