using System;
using MyERP.Inventory;
using MyERP.Inventory.Entities;
using MyERP.Sales.DomainServices;
using Volo.Abp;
using Xunit;

namespace MyERP.Domain.Tests.Sales;

/// <summary>
/// Unit tests for DiscountCeilingValidationService (Gotcha #3222).
/// Enforces per-item max_discount limits and discount percentage boundaries [0, 100].
/// </summary>
public class DiscountCeilingValidationTests
{
    private readonly Guid _companyId = Guid.NewGuid();

    [Fact]
    public void ValidateItemDiscount_WithinMaxDiscount_Passes()
    {
        var item = new Item(Guid.NewGuid(), _companyId, "ITEM-001", "Widget", ItemType.Goods)
        {
            MaxDiscount = 20m
        };

        var service = new DiscountCeilingValidationService(null!);

        // Should not throw
        service.ValidateItemDiscount(item, 15m);
        service.ValidateItemDiscount(item, 20m);
        service.ValidateItemDiscount(item, 0m);
    }

    [Fact]
    public void ValidateItemDiscount_ExceedsMaxDiscount_ThrowsMaxDiscountExceeded()
    {
        var item = new Item(Guid.NewGuid(), _companyId, "ITEM-002", "Gadget", ItemType.Goods)
        {
            MaxDiscount = 10m
        };

        var service = new DiscountCeilingValidationService(null!);

        var ex = Assert.Throws<BusinessException>(() => service.ValidateItemDiscount(item, 15m));
        Assert.Equal(MyERPDomainErrorCodes.MaxDiscountExceeded, ex.Code);
    }

    [Fact]
    public void ValidateItemDiscount_NoMaxDiscountSet_AllowsAnyValidPercentage()
    {
        var item = new Item(Guid.NewGuid(), _companyId, "ITEM-003", "Service", ItemType.Service)
        {
            MaxDiscount = null
        };

        var service = new DiscountCeilingValidationService(null!);

        // Should not throw
        service.ValidateItemDiscount(item, 50m);
        service.ValidateItemDiscount(item, 100m);
    }

    [Theory]
    [InlineData(-5)]
    [InlineData(105)]
    public void ValidateItemDiscount_InvalidRange_ThrowsInvalidDiscountPercentage(decimal invalidPct)
    {
        var item = new Item(Guid.NewGuid(), _companyId, "ITEM-004", "Widget", ItemType.Goods)
        {
            MaxDiscount = 50m
        };

        var service = new DiscountCeilingValidationService(null!);

        var ex = Assert.Throws<BusinessException>(() => service.ValidateItemDiscount(item, invalidPct));
        Assert.Equal(MyERPDomainErrorCodes.InvalidDiscountPercentage, ex.Code);
    }
}
