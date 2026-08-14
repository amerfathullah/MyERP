using System;
using MyERP.Inventory.Entities;
using Shouldly;
using Xunit;

namespace MyERP.Inventory;

public class CustomsTariffNumberTests
{
    [Fact]
    public void Create_SetsProperties()
    {
        var companyId = Guid.NewGuid();
        var ctn = new CustomsTariffNumber(Guid.NewGuid(), companyId, "8471.30.0000", "Laptops and portable computers");

        ctn.CompanyId.ShouldBe(companyId);
        ctn.TariffNumber.ShouldBe("8471.30.0000");
        ctn.Description.ShouldBe("Laptops and portable computers");
    }

    [Fact]
    public void SetTariffNumber_UpdatesNumber()
    {
        var ctn = new CustomsTariffNumber(Guid.NewGuid(), Guid.NewGuid(), "8471.30.0000");
        ctn.SetTariffNumber("8471.41.0000");
        ctn.TariffNumber.ShouldBe("8471.41.0000");
    }

    [Fact]
    public void Item_CustomsTariffNumberId_CanBeAssigned()
    {
        var item = new Item(Guid.NewGuid(), Guid.NewGuid(), "ITEM-001", "Laptop Computer", ItemType.Goods);
        var ctnId = Guid.NewGuid();
        item.CustomsTariffNumberId = ctnId;
        item.CustomsTariffNumberId.ShouldBe(ctnId);
    }
}
