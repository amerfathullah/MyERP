using System;
using MyERP.CRM.Entities;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace MyERP.CRM;

public class CrmSettingsTests
{
    [Fact]
    public void Should_Create_Valid_CrmSettings()
    {
        var id = Guid.NewGuid();
        var settings = new CrmSettings(
            id,
            "Naming Series",
            true,
            true,
            30,
            true,
            60,
            true,
            true);

        settings.Id.ShouldBe(id);
        settings.CampaignNamingBy.ShouldBe("Naming Series");
        settings.AllowLeadDuplicationBasedOnEmails.ShouldBeTrue();
        settings.AutoCreationOfContact.ShouldBeTrue();
        settings.CloseOpportunityAfterDays.ShouldBe(30);
        settings.EnableOpportunityCreationFromContactUs.ShouldBeTrue();
        settings.DefaultQuotationValidityDays.ShouldBe(60);
        settings.CarryForwardCommunicationAndComments.ShouldBeTrue();
        settings.UpdateTimestampOnNewCommunication.ShouldBeTrue();
    }
}
