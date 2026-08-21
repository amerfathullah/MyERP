using System;
using MyERP.Purchasing.Entities;
using MyERP.Sales.Entities;
using Volo.Abp;
using Xunit;

namespace MyERP.Domain.Tests.Accounting;

/// <summary>
/// Unit tests for deferred accounting date rules (Gotcha #1624):
/// 1. ServiceStartDate and ServiceEndDate mandatory for deferred items
/// 2. ServiceStartDate cannot be after ServiceEndDate
/// 3. PostingDate cannot be after ServiceEndDate ("Service End Date cannot be before Invoice Posting Date")
/// </summary>
public class DeferredAccountingDateValidationTests
{
    private readonly Guid _companyId = Guid.NewGuid();
    private readonly Guid _customerId = Guid.NewGuid();
    private readonly Guid _supplierId = Guid.NewGuid();
    private readonly Guid _itemId = Guid.NewGuid();

    [Fact]
    public void SalesInvoice_DeferredItem_MissingDates_ThrowsValidationException()
    {
        var postingDate = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc);
        var si = new SalesInvoice(Guid.NewGuid(), _companyId, _customerId, "SINV-2026-0001", postingDate);
        si.AddItem(_itemId, "SaaS Subscription", 1m, 1200m, 0m);

        si.Items[0].EnableDeferredRevenue = true;
        // Dates not set

        var ex = Assert.Throws<BusinessException>(() => si.Submit());
        Assert.Contains("mandatory for deferred revenue items", ex.Data["detail"]?.ToString());
    }

    [Fact]
    public void SalesInvoice_DeferredItem_StartDateAfterEndDate_ThrowsValidationException()
    {
        var postingDate = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc);
        var si = new SalesInvoice(Guid.NewGuid(), _companyId, _customerId, "SINV-2026-0002", postingDate);
        si.AddItem(_itemId, "SaaS Subscription", 1m, 1200m, 0m);

        si.Items[0].EnableDeferredRevenue = true;
        si.Items[0].ServiceStartDate = postingDate.AddMonths(2);
        si.Items[0].ServiceEndDate = postingDate.AddMonths(1);

        var ex = Assert.Throws<BusinessException>(() => si.Submit());
        Assert.Contains("cannot be after Service End Date", ex.Data["detail"]?.ToString());
    }

    [Fact]
    public void SalesInvoice_DeferredItem_PostingDateAfterEndDate_ThrowsValidationException()
    {
        var postingDate = new DateTime(2026, 8, 15, 0, 0, 0, DateTimeKind.Utc);
        var si = new SalesInvoice(Guid.NewGuid(), _companyId, _customerId, "SINV-2026-0003", postingDate);
        si.AddItem(_itemId, "SaaS Subscription", 1m, 1200m, 0m);

        si.Items[0].EnableDeferredRevenue = true;
        si.Items[0].ServiceStartDate = new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc);
        si.Items[0].ServiceEndDate = new DateTime(2026, 7, 31, 0, 0, 0, DateTimeKind.Utc); // Before posting date

        var ex = Assert.Throws<BusinessException>(() => si.Submit());
        Assert.Contains("Service End Date cannot be before Invoice Posting Date", ex.Data["detail"]?.ToString());
    }

    [Fact]
    public void PurchaseInvoice_DeferredExpense_ValidDates_SubmitsSuccessfully()
    {
        var postingDate = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc);
        var pi = new PurchaseInvoice(Guid.NewGuid(), _companyId, _supplierId, "PINV-2026-0001", postingDate);
        pi.AddItem(_itemId, "Insurance Prepayment", 1m, 2400m, 0m);

        pi.Items[0].EnableDeferredExpense = true;
        pi.Items[0].ServiceStartDate = postingDate;
        pi.Items[0].ServiceEndDate = postingDate.AddYears(1);

        pi.Submit();

        Assert.Equal(global::MyERP.Core.DocumentStatus.Submitted, pi.Status);
    }
}
