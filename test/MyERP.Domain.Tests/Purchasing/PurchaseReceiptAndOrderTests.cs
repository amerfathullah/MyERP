using System;
using MyERP.Core;
using MyERP.Purchasing.Entities;
using Volo.Abp;
using Xunit;

namespace MyERP.Domain.Tests.Purchasing;

/// <summary>
/// Unit tests for Purchase Receipt date validation, accepted/rejected quantities validation,
/// and Purchase Order PerReceived capping formula.
/// Verifies rules migrated from erpnext/buying/doctype/purchase_order & purchase_receipt (Gotchas #370, #488, #538, #1238).
/// </summary>
public class PurchaseReceiptAndOrderTests
{
    private readonly Guid _companyId = Guid.NewGuid();
    private readonly Guid _supplierId = Guid.NewGuid();
    private readonly Guid _warehouseId = Guid.NewGuid();
    private readonly Guid _itemId1 = Guid.NewGuid();
    private readonly Guid _itemId2 = Guid.NewGuid();

    [Fact]
    public void PurchaseOrder_PerReceived_CapsPerItemBeforeSumming()
    {
        var po = new PurchaseOrder(Guid.NewGuid(), _companyId, _supplierId, "PO-2026-0001", DateTime.UtcNow);
        po.AddItem(_itemId1, "Widget A", 10m, 100m, 0m); // Line 1: qty = 10
        po.AddItem(_itemId2, "Widget B", 10m, 100m, 0m); // Line 2: qty = 10
        po.Submit();

        // Line 1 over-received: 15 received against 10 ordered (capped at 100%)
        po.Items[0].ReceivedQty = 15m;
        // Line 2 partially received: 5 received against 10 ordered (50%)
        po.Items[1].ReceivedQty = 5m;

        // SUM(MIN(15, 10) + MIN(5, 10)) / (10 + 10) * 100 = 75%
        Assert.Equal(75m, po.PerReceived);
    }

    [Fact]
    public void PurchaseReceipt_AcceptedRejectedQty_AutoCalculatesReceivedQty()
    {
        var item = new PurchaseReceiptItem(
            Guid.NewGuid(), Guid.NewGuid(), _itemId1, "Widget A", 8m, 50m, 0m)
        {
            RejectedQty = 2m,
            ReceivedQty = 0m // Empty, should auto-derive to 8 + 2 = 10
        };

        item.ValidateAcceptedRejectedQty(isReturn: false);

        Assert.Equal(10m, item.ReceivedQty);
    }

    [Fact]
    public void PurchaseReceipt_AcceptedRejectedQty_Mismatch_ThrowsValidationException()
    {
        var item = new PurchaseReceiptItem(
            Guid.NewGuid(), Guid.NewGuid(), _itemId1, "Widget A", 8m, 50m, 0m)
        {
            RejectedQty = 2m,
            ReceivedQty = 12m // 12 != 8 + 2
        };

        var ex = Assert.Throws<BusinessException>(() => item.ValidateAcceptedRejectedQty(isReturn: false));
        Assert.Equal(MyERPDomainErrorCodes.ValidationFailed, ex.Code);
        Assert.Contains("must equal Accepted Qty", ex.Data["detail"]?.ToString());
    }

    [Fact]
    public void PurchaseReceipt_ValidatePostingDateWithPo_EarlierThanPo_ThrowsValidationException()
    {
        var poDate = DateTime.UtcNow.Date;
        var prDate = poDate.AddDays(-2); // 2 days before PO

        var pr = new PurchaseReceipt(Guid.NewGuid(), _companyId, _supplierId, _warehouseId, "PR-2026-0001", prDate);
        pr.AddItem(_itemId1, "Widget A", 10m, 50m, 0m);

        var ex = Assert.Throws<BusinessException>(() => pr.ValidatePostingDateWithPo(poDate));
        Assert.Equal(MyERPDomainErrorCodes.ValidationFailed, ex.Code);
        Assert.Contains("cannot be before the linked Purchase Order date", ex.Data["detail"]?.ToString());
    }

    [Fact]
    public void PurchaseReceipt_Submit_MissingRejectedWarehouse_ThrowsValidationException()
    {
        var pr = new PurchaseReceipt(Guid.NewGuid(), _companyId, _supplierId, _warehouseId, "PR-2026-0002", DateTime.UtcNow.Date);
        pr.AddItem(_itemId1, "Widget A", 8m, 50m, 0m, rejectedQty: 2m, rejectedWarehouseId: null);

        var ex = Assert.Throws<BusinessException>(() => pr.Submit());
        Assert.Equal(MyERPDomainErrorCodes.ValidationFailed, ex.Code);
        Assert.Contains("Rejected Warehouse is mandatory", ex.Data["detail"]?.ToString());
    }

    [Fact]
    public void PurchaseReceipt_Submit_RejectedWarehouseSameAsTarget_ThrowsValidationException()
    {
        var pr = new PurchaseReceipt(Guid.NewGuid(), _companyId, _supplierId, _warehouseId, "PR-2026-0003", DateTime.UtcNow.Date);
        pr.AddItem(_itemId1, "Widget A", 8m, 50m, 0m, rejectedQty: 2m, rejectedWarehouseId: _warehouseId);

        var ex = Assert.Throws<BusinessException>(() => pr.Submit());
        Assert.Equal(MyERPDomainErrorCodes.ValidationFailed, ex.Code);
        Assert.Contains("cannot be the same", ex.Data["detail"]?.ToString());
    }

    [Fact]
    public void PurchaseReceipt_Submit_FuturePostingDate_ThrowsValidationException()
    {
        var futureDate = DateTime.UtcNow.Date.AddDays(1);
        var pr = new PurchaseReceipt(Guid.NewGuid(), _companyId, _supplierId, _warehouseId, "PR-2026-0004", futureDate);
        pr.AddItem(_itemId1, "Widget A", 10m, 50m, 0m);

        var ex = Assert.Throws<BusinessException>(() => pr.Submit());
        Assert.Equal(MyERPDomainErrorCodes.ValidationFailed, ex.Code);
        Assert.Contains("Posting Date cannot be in the future", ex.Data["detail"]?.ToString());
    }

    [Fact]
    public void PurchaseReceipt_Submit_WithValidRejectedWarehouse_Succeeds()
    {
        var rejectedWarehouseId = Guid.NewGuid();
        var pr = new PurchaseReceipt(Guid.NewGuid(), _companyId, _supplierId, _warehouseId, "PR-2026-0005", DateTime.UtcNow.Date);
        pr.AddItem(_itemId1, "Widget A", 8m, 50m, 0m, rejectedQty: 2m, rejectedWarehouseId: rejectedWarehouseId);

        pr.Submit();

        Assert.Equal(DocumentStatus.Submitted, pr.Status);
        Assert.Equal(10m, pr.Items[0].ReceivedQty);
    }
}
