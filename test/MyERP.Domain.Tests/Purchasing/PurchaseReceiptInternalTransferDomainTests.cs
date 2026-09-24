using System;
using MyERP.Core;
using MyERP.Purchasing.Entities;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace MyERP.Tests.Purchasing;

public class PurchaseReceiptInternalTransferDomainTests
{
    private readonly Guid _companyId = Guid.NewGuid();
    private readonly Guid _supplierId = Guid.NewGuid();
    private readonly Guid _warehouseId = Guid.NewGuid();
    private readonly Guid _fromWarehouseId = Guid.NewGuid();
    private readonly Guid _rejectedWarehouseId = Guid.NewGuid();
    private readonly Guid _itemId = Guid.NewGuid();

    private PurchaseReceipt CreateReceipt(bool isReturn = false)
    {
        var pr = new PurchaseReceipt(
            Guid.NewGuid(), _companyId, _supplierId, _warehouseId, "PR-TRANS-001", DateTime.UtcNow.Date);
        pr.IsReturn = isReturn;
        return pr;
    }

    [Fact]
    public void AddItem_WithFromWarehouseId_SetsProperty()
    {
        var pr = CreateReceipt();
        pr.AddItem(
            _itemId, "Transfer Part", 7m, 100m, 0m, "Unit",
            fromWarehouseId: _fromWarehouseId,
            rejectedQty: 3m,
            rejectedWarehouseId: _rejectedWarehouseId);

        var item = pr.Items[0];
        item.Quantity.ShouldBe(7m);
        item.RejectedQty.ShouldBe(3m);
        item.FromWarehouseId.ShouldBe(_fromWarehouseId);
        item.RejectedWarehouseId.ShouldBe(_rejectedWarehouseId);
    }

    [Fact]
    public void Submit_SameFromAndTargetWarehouse_ThrowsValidationException()
    {
        var pr = CreateReceipt();
        pr.AddItem(
            _itemId, "Same WH Part", 10m, 50m, 0m, "Unit",
            warehouseId: _warehouseId,
            fromWarehouseId: _warehouseId);

        var ex = Should.Throw<BusinessException>(() => pr.Submit());
        ex.Code.ShouldBe(MyERPDomainErrorCodes.ValidationFailed);
    }

    [Fact]
    public void Submit_FromWarehouseOnSubcontractedReceipt_ThrowsValidationException()
    {
        var pr = CreateReceipt();
        pr.IsSubcontracted = true;
        pr.AddItem(
            _itemId, "Subcontract Part", 10m, 50m, 0m, "Unit",
            fromWarehouseId: _fromWarehouseId);

        var ex = Should.Throw<BusinessException>(() => pr.Submit());
        ex.Code.ShouldBe(MyERPDomainErrorCodes.ValidationFailed);
    }

    [Fact]
    public void Submit_ReturnWithOnlyRejectedQty_Succeeds()
    {
        var pr = CreateReceipt(isReturn: true);
        pr.AddItem(
            _itemId, "Fully Rejected Return", 0m, 50m, 0m, "Unit",
            rejectedQty: -5m,
            rejectedWarehouseId: _rejectedWarehouseId);

        pr.Submit();
        pr.Status.ShouldBe(DocumentStatus.Submitted);
    }

    [Fact]
    public void Submit_ReturnWithoutNegativeQtyOrRejectedQty_Throws()
    {
        var pr = CreateReceipt(isReturn: true);
        Should.Throw<ArgumentException>(() =>
            pr.AddItem(_itemId, "Invalid Return", 0m, 50m, 0m, "Unit", rejectedQty: 0m));
    }
}
