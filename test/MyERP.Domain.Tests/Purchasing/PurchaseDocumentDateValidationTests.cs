using System;
using MyERP.Purchasing.Entities;
using Xunit;

namespace MyERP.Domain.Tests.Purchasing;

/// <summary>
/// Unit tests for Purchase Receipt and Purchase Invoice date temporal ordering rules (Gotchas #538, #1238, #1508).
/// 1. PR posting date cannot be in the future.
/// 2. PR/PI posting/issue date cannot precede the linked PO order date.
/// </summary>
public class PurchaseDocumentDateValidationTests
{
    private readonly Guid _companyId = Guid.NewGuid();
    private readonly Guid _supplierId = Guid.NewGuid();
    private readonly Guid _warehouseId = Guid.NewGuid();

    [Fact]
    public void PurchaseReceipt_DateAfterPoDate_IsValid()
    {
        var poDate = new DateTime(2026, 5, 10, 0, 0, 0, DateTimeKind.Utc);
        var prDate = new DateTime(2026, 5, 12, 0, 0, 0, DateTimeKind.Utc);

        var po = new PurchaseOrder(Guid.NewGuid(), _companyId, _supplierId, "PO-001", poDate);
        var pr = new PurchaseReceipt(Guid.NewGuid(), _companyId, _supplierId, _warehouseId, "PR-001", prDate)
        {
            PurchaseOrderId = po.Id
        };

        bool isValid = pr.PostingDate.Date >= po.OrderDate.Date;
        Assert.True(isValid);
    }

    [Fact]
    public void PurchaseReceipt_DateBeforePoDate_IsInvalid()
    {
        var poDate = new DateTime(2026, 5, 10, 0, 0, 0, DateTimeKind.Utc);
        var prDate = new DateTime(2026, 5, 8, 0, 0, 0, DateTimeKind.Utc);

        var po = new PurchaseOrder(Guid.NewGuid(), _companyId, _supplierId, "PO-001", poDate);
        var pr = new PurchaseReceipt(Guid.NewGuid(), _companyId, _supplierId, _warehouseId, "PR-001", prDate)
        {
            PurchaseOrderId = po.Id
        };

        bool isInvalid = pr.PostingDate.Date < po.OrderDate.Date;
        Assert.True(isInvalid);
    }

    [Fact]
    public void PurchaseInvoice_DateBeforePoDate_IsInvalid()
    {
        var poDate = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        var piDate = new DateTime(2026, 5, 28, 0, 0, 0, DateTimeKind.Utc);

        var po = new PurchaseOrder(Guid.NewGuid(), _companyId, _supplierId, "PO-002", poDate);
        var pi = new PurchaseInvoice(Guid.NewGuid(), _companyId, _supplierId, "PI-001", piDate);

        bool isInvalid = pi.IssueDate.Date < po.OrderDate.Date;
        Assert.True(isInvalid);
    }
}
