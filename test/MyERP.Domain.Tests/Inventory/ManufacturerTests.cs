using System;
using MyERP.Inventory.Entities;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace MyERP.Inventory;

public class ManufacturerTests
{
    [Fact]
    public void Create_SetsProperties()
    {
        var companyId = Guid.NewGuid();
        var mfr = new Manufacturer(Guid.NewGuid(), companyId, "DELL", "Dell Technologies Inc.")
        {
            Website = "https://dell.com",
            Country = "United States",
        };

        mfr.CompanyId.ShouldBe(companyId);
        mfr.ShortName.ShouldBe("DELL");
        mfr.FullName.ShouldBe("Dell Technologies Inc.");
        mfr.Website.ShouldBe("https://dell.com");
        mfr.Country.ShouldBe("United States");
    }

    [Fact]
    public void Create_EmptyShortName_Throws()
    {
        Should.Throw<ArgumentException>(() =>
            new Manufacturer(Guid.NewGuid(), Guid.NewGuid(), ""));
    }

    [Fact]
    public void ItemManufacturer_Create_SetsProperties()
    {
        var companyId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var mfrId = Guid.NewGuid();

        var im = new ItemManufacturer(Guid.NewGuid(), companyId, itemId, mfrId, "PART-12345", isDefault: true)
        {
            Description = "Primary OEM part",
        };

        im.CompanyId.ShouldBe(companyId);
        im.ItemId.ShouldBe(itemId);
        im.ManufacturerId.ShouldBe(mfrId);
        im.ManufacturerPartNo.ShouldBe("PART-12345");
        im.IsDefault.ShouldBeTrue();
        im.Description.ShouldBe("Primary OEM part");
    }
}
