using System;
using MyERP.Accounting.Entities;
using Shouldly;
using Xunit;

namespace MyERP.Accounting;

public class AccountsSettingsDomainTests
{
    [Fact]
    public void Should_Create_Valid_AccountsSettings()
    {
        var id = Guid.NewGuid();
        var settings = new AccountsSettings(id);

        settings.Id.ShouldBe(id);
        settings.UnlinkPaymentOnCancellationOfInvoice.ShouldBeTrue();
        settings.EnableSubscription.ShouldBeTrue();
        settings.BookDeferredEntriesBasedOn.ShouldBe("Days");
        settings.DetermineAddressTaxCategoryFrom.ShouldBe("Billing Address");
        settings.AllowStaleExchangeRates.ShouldBeTrue();
        settings.StaleDays.ShouldBe(1);
    }
}
