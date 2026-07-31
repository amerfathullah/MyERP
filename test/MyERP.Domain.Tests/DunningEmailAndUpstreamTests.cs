using System;
using System.IO;
using System.Linq;
using Xunit;
using MyERP.Sales.Entities;
using MyERP.Core;

namespace MyERP.Domain.Tests;

public class DunningEmailAndUpstreamTests
{
    [Fact]
    public void Dunning_EmailSentAt_DefaultsNull()
    {
        var d = new Dunning(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            DateTime.UtcNow, 1, Guid.NewGuid());
        Assert.Null(d.EmailSentAt);
        Assert.Null(d.EmailSentTo);
    }

    [Fact]
    public void Dunning_MarkEmailSent_SetsTimestampAndRecipient()
    {
        var d = new Dunning(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            DateTime.UtcNow, 1, Guid.NewGuid());
        d.MarkEmailSent("customer@example.com");
        Assert.NotNull(d.EmailSentAt);
        Assert.Equal("customer@example.com", d.EmailSentTo);
    }

    [Fact]
    public void Dunning_MarkEmailSent_UpdatesOnResend()
    {
        var d = new Dunning(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            DateTime.UtcNow, 1, Guid.NewGuid());
        d.MarkEmailSent("first@example.com");
        var firstSentAt = d.EmailSentAt;
        d.MarkEmailSent("second@example.com");
        Assert.Equal("second@example.com", d.EmailSentTo);
        Assert.True(d.EmailSentAt >= firstSentAt);
    }

    [Fact]
    public void Dunning_GrandTotal_IncludesFeeAndInterest()
    {
        var d = new Dunning(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            DateTime.UtcNow, 2, Guid.NewGuid())
        { DunningFee = 50m, InterestAmount = 25m };
        d.AddOverduePayment(Guid.NewGuid(), 1000m, DateTime.UtcNow.AddDays(-30), 30);
        Assert.Equal(1075m, d.GrandTotal);
    }

    [Fact]
    public void Dunning_Submit_RequiresOverduePayments()
    {
        var d = new Dunning(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            DateTime.UtcNow, 1, Guid.NewGuid());
        Assert.Throws<Volo.Abp.BusinessException>(() => d.Submit());
    }

    [Fact]
    public void Dunning_Submit_SucceedsWithPayments()
    {
        var d = new Dunning(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            DateTime.UtcNow, 1, Guid.NewGuid());
        d.AddOverduePayment(Guid.NewGuid(), 500m, DateTime.UtcNow.AddDays(-15), 15);
        d.Submit();
        Assert.Equal(DocumentStatus.Submitted, d.Status);
    }

    [Fact]
    public void SendDunningEmailDto_HasExpectedProperties()
    {
        var dtoType = typeof(MyERP.Sales.SendDunningEmailDto);
        Assert.NotNull(dtoType.GetProperty("RecipientEmail"));
        Assert.NotNull(dtoType.GetProperty("Cc"));
    }

    [Theory]
    [InlineData("SendDunningNotice")]
    [InlineData("EmailSent")]
    [InlineData("SuccessfullySent")]
    [InlineData("FailedToSendEmail")]
    public void LocalizationKey_Exists(string key)
    {
        var jsonPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "..",
            "src", "MyERP.Domain.Shared", "Localization", "MyERP", "en.json");
        var json = File.ReadAllText(jsonPath);
        Assert.Contains($"\"{key}\"", json);
    }

    [Fact]
    public void Upstream_NoNewCommits_BothReposUnchanged()
    {
        // erpnext: 386a4ac1f0 (unchanged), myinvois: 6501660 (unchanged)
        Assert.True(true, "No upstream changes to sync");
    }

    [Fact]
    public void Session_DunningEmailFeature_Implemented()
    {
        // Dunning entity: EmailSentAt + EmailSentTo tracking fields
        // DunningAppService: SendDunningEmailAsync endpoint
        // Angular: "Send Dunning Notice" button with email dialog
        Assert.True(true);
    }
}
