using System;
using MyERP.Accounting.Entities;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace MyERP.Accounting;

public class SubscriptionSettingsDomainTests
{
    [Fact]
    public void Should_Create_Valid_SubscriptionSettings()
    {
        var id = Guid.NewGuid();
        var settings = new SubscriptionSettings(
            id,
            gracePeriod: 5,
            cancelAfterGrace: true,
            prorate: true);

        settings.Id.ShouldBe(id);
        settings.GracePeriod.ShouldBe(5);
        settings.CancelAfterGrace.ShouldBeTrue();
        settings.Prorate.ShouldBeTrue();
    }
}
