using System;
using MyERP.Core;
using MyERP.Sales.Entities;
using Xunit;

namespace MyERP.Domain.Tests.Sales;

/// <summary>
/// Unit tests for Quotation expiration invariants (Gotcha #964):
/// 1. IsExpired is true when ValidUntil date is in the past and Quotation is submitted without conversion.
/// 2. IsExpired is false when ValidUntil is in the future, or when quotation is Draft or already converted.
/// </summary>
public class QuotationExpirationTests
{
    private readonly Guid _companyId = Guid.NewGuid();
    private readonly Guid _customerId = Guid.NewGuid();

    [Fact]
    public void Quotation_IsExpired_TrueWhenValidUntilPassedAndSubmitted()
    {
        var quotation = new Quotation(Guid.NewGuid(), _companyId, _customerId, "QTN-001", DateTime.UtcNow.AddDays(-10));
        quotation.ValidUntil = DateTime.UtcNow.AddDays(-2);
        quotation.AddItem(Guid.NewGuid(), "Widget", 1m, 100m, 0m);
        quotation.Submit();

        Assert.True(quotation.IsExpired);
    }

    [Fact]
    public void Quotation_IsExpired_FalseWhenDraft()
    {
        var quotation = new Quotation(Guid.NewGuid(), _companyId, _customerId, "QTN-001", DateTime.UtcNow.AddDays(-10));
        quotation.ValidUntil = DateTime.UtcNow.AddDays(-2);
        quotation.AddItem(Guid.NewGuid(), "Widget", 1m, 100m, 0m);

        Assert.False(quotation.IsExpired); // Draft is not expired
    }

    [Fact]
    public void Quotation_IsExpired_FalseWhenValidUntilInFuture()
    {
        var quotation = new Quotation(Guid.NewGuid(), _companyId, _customerId, "QTN-001", DateTime.UtcNow);
        quotation.ValidUntil = DateTime.UtcNow.AddDays(15);
        quotation.AddItem(Guid.NewGuid(), "Widget", 1m, 100m, 0m);
        quotation.Submit();

        Assert.False(quotation.IsExpired);
    }

    [Fact]
    public void Quotation_IsExpired_FalseWhenAlreadyConverted()
    {
        var quotation = new Quotation(Guid.NewGuid(), _companyId, _customerId, "QTN-001", DateTime.UtcNow.AddDays(-10));
        quotation.ValidUntil = DateTime.UtcNow.AddDays(-2);
        quotation.AddItem(Guid.NewGuid(), "Widget", 1m, 100m, 0m);
        quotation.Submit();
        quotation.ConvertedToSalesOrderId = Guid.NewGuid();

        Assert.False(quotation.IsExpired);
    }
}
