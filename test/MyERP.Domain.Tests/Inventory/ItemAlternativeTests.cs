using System;
using MyERP.Inventory.Entities;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace MyERP.Inventory;

public class ItemAlternativeTests
{
    [Fact]
    public void Create_SetsProperties()
    {
        var companyId = Guid.NewGuid();
        var itemA = Guid.NewGuid();
        var itemB = Guid.NewGuid();

        var ia = new ItemAlternative(Guid.NewGuid(), companyId, itemA, itemB, twoWay: true);
        ia.CompanyId.ShouldBe(companyId);
        ia.ItemId.ShouldBe(itemA);
        ia.AlternativeItemId.ShouldBe(itemB);
        ia.TwoWay.ShouldBeTrue();
    }

    [Fact]
    public void Create_SameItem_Throws()
    {
        var sameItem = Guid.NewGuid();
        Should.Throw<BusinessException>(() =>
            new ItemAlternative(Guid.NewGuid(), Guid.NewGuid(), sameItem, sameItem));
    }

    [Fact]
    public void Create_EmptyGuid_Throws()
    {
        Should.Throw<BusinessException>(() =>
            new ItemAlternative(Guid.NewGuid(), Guid.NewGuid(), Guid.Empty, Guid.NewGuid()));
    }
}
