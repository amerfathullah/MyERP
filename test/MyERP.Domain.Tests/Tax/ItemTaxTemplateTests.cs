using System;
using MyERP.Tax.Entities;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace MyERP.Tax;

public class ItemTaxTemplateTests
{
    [Fact]
    public void Create_SetsDefaults()
    {
        var template = new ItemTaxTemplate(Guid.NewGuid(), Guid.NewGuid(), "SST 6%");
        template.Title.ShouldBe("SST 6%");
        template.IsDisabled.ShouldBeFalse();
        template.Details.ShouldBeEmpty();
    }

    [Fact]
    public void AddDetail_WithRate()
    {
        var template = new ItemTaxTemplate(Guid.NewGuid(), Guid.NewGuid(), "SST 6%");
        var accountId = Guid.NewGuid();
        template.AddDetail(accountId, 6m);
        template.Details.Count.ShouldBe(1);
        template.Details[0].TaxRate.ShouldBe(6m);
        template.Details[0].NotApplicable.ShouldBeFalse();
    }

    [Fact]
    public void AddDetail_NotApplicable_SetsZeroRate()
    {
        var template = new ItemTaxTemplate(Guid.NewGuid(), Guid.NewGuid(), "Exempt");
        var accountId = Guid.NewGuid();
        template.AddDetail(accountId, 99m, notApplicable: true);
        template.Details[0].TaxRate.ShouldBe(0); // N/A sentinel overrides rate
        template.Details[0].NotApplicable.ShouldBeTrue();
    }

    [Fact]
    public void AddDetail_DuplicateAccount_Throws()
    {
        var template = new ItemTaxTemplate(Guid.NewGuid(), Guid.NewGuid(), "Test");
        var accountId = Guid.NewGuid();
        template.AddDetail(accountId, 6m);
        Should.Throw<BusinessException>(() => template.AddDetail(accountId, 8m));
    }

    [Fact]
    public void GetRateForAccount_Found_ReturnsRate()
    {
        var template = new ItemTaxTemplate(Guid.NewGuid(), Guid.NewGuid(), "SST");
        var accountId = Guid.NewGuid();
        template.AddDetail(accountId, 6m);
        template.GetRateForAccount(accountId).ShouldBe(6m);
    }

    [Fact]
    public void GetRateForAccount_NotFound_ReturnsNull()
    {
        var template = new ItemTaxTemplate(Guid.NewGuid(), Guid.NewGuid(), "SST");
        template.AddDetail(Guid.NewGuid(), 6m);
        template.GetRateForAccount(Guid.NewGuid()).ShouldBeNull();
    }

    [Fact]
    public void GetRateForAccount_NotApplicable_ReturnsNull()
    {
        var template = new ItemTaxTemplate(Guid.NewGuid(), Guid.NewGuid(), "Exempt");
        var accountId = Guid.NewGuid();
        template.AddDetail(accountId, 0, notApplicable: true);
        template.GetRateForAccount(accountId).ShouldBeNull(); // exclude entirely
    }
}
