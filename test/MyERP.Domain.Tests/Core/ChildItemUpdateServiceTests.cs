using System;
using MyERP.Core.DomainServices;
using MyERP.Purchasing.Entities;
using MyERP.Sales.Entities;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace MyERP.Core;

public class ChildItemUpdateServiceTests
{
    private readonly ChildItemUpdateService _service = new();

    [Fact]
    public void ValidateSalesOrderItemUpdate_OpenItem_Succeeds()
    {
        var item = new SalesOrderItem(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Item", 10, 50, 0, "Unit");
        Should.NotThrow(() => _service.ValidateSalesOrderItemUpdate(item, 12, 55));
    }

    [Fact]
    public void ValidateSalesOrderItemUpdate_ClosedItem_ThrowsValidationFailed()
    {
        var item = new SalesOrderItem(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Item", 10, 50, 0, "Unit")
        {
            IsClosed = true
        };
        var ex = Should.Throw<BusinessException>(() => _service.ValidateSalesOrderItemUpdate(item, 12, 50));
        ex.Code.ShouldBe(MyERPDomainErrorCodes.ValidationFailed);
    }

    [Fact]
    public void ValidatePurchaseOrderItemUpdate_ClosedItem_ThrowsValidationFailed()
    {
        var item = new PurchaseOrderItem(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Item", 10, 50, 0, "Unit")
        {
            IsClosed = true
        };
        var ex = Should.Throw<BusinessException>(() => _service.ValidatePurchaseOrderItemUpdate(item, 10, 55));
        ex.Code.ShouldBe(MyERPDomainErrorCodes.ValidationFailed);
    }

    [Fact]
    public void ValidateSalesOrderItemDeletion_ClosedItem_ThrowsValidationFailed()
    {
        var item = new SalesOrderItem(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Item", 10, 50, 0, "Unit")
        {
            IsClosed = true
        };
        var ex = Should.Throw<BusinessException>(() => _service.ValidateSalesOrderItemDeletion(item));
        ex.Code.ShouldBe(MyERPDomainErrorCodes.ValidationFailed);
    }

    [Fact]
    public void ValidatePurchaseOrderItemDeletion_ClosedItem_ThrowsValidationFailed()
    {
        var item = new PurchaseOrderItem(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Item", 10, 50, 0, "Unit")
        {
            IsClosed = true
        };
        var ex = Should.Throw<BusinessException>(() => _service.ValidatePurchaseOrderItemDeletion(item));
        ex.Code.ShouldBe(MyERPDomainErrorCodes.ValidationFailed);
    }

    [Fact]
    public void ValidateSalesOrderItemStockQty_WithConversionFactor_ComparesInStockUom()
    {
        // 6 boxes with conversion factor 5 = 30 in stock UOM
        var item = new SalesOrderItem(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Item", 6, 10, 0, "Box")
        {
            ConversionFactor = 5,
            DeliveredQty = 2 // 2 boxes = 10 units delivered in stock UOM
        };

        // Reducing to 5 boxes with conversion factor 2 = 10 units (equal to delivered 10) -> succeeds
        Should.NotThrow(() => _service.ValidateSalesOrderItemStockQty(item, 5, 2));

        // Reducing to 4 boxes with conversion factor 2 = 8 units (< delivered 10) -> throws
        var ex = Should.Throw<BusinessException>(() => _service.ValidateSalesOrderItemStockQty(item, 4, 2));
        ex.Code.ShouldBe(MyERPDomainErrorCodes.ValidationFailed);
    }

    [Fact]
    public void ValidatePurchaseOrderItemStockQty_WithConversionFactor_ComparesInStockUom()
    {
        // 6 boxes with conversion factor 5 = 30 in stock UOM
        var item = new PurchaseOrderItem(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Item", 6, 10, 0, "Box")
        {
            ConversionFactor = 5,
            ReceivedQty = 2 // 2 boxes = 10 units received in stock UOM
        };

        // Reducing to 5 boxes with conversion factor 2 = 10 units (equal to received 10) -> succeeds
        Should.NotThrow(() => _service.ValidatePurchaseOrderItemStockQty(item, 5, 2));

        // Reducing to 4 boxes with conversion factor 2 = 8 units (< received 10) -> throws
        var ex = Should.Throw<BusinessException>(() => _service.ValidatePurchaseOrderItemStockQty(item, 4, 2));
        ex.Code.ShouldBe(MyERPDomainErrorCodes.ValidationFailed);
    }
}
