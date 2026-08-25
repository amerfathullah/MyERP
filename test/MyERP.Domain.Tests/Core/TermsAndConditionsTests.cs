using System;
using MyERP.Core;
using MyERP.Core.Entities;
using Xunit;

namespace MyERP.Domain.Tests.Core;

public class TermsAndConditionsTests
{
    private readonly Guid _companyId = Guid.NewGuid();

    [Fact]
    public void TermsAndConditions_Creation_SetsPropertiesCorrectly()
    {
        var tnc = new TermsAndConditions(Guid.NewGuid(), _companyId, "Standard Sales Terms")
        {
            Terms = "<p>Payment due within 30 days. Late penalty 1.5% per month.</p>",
            IsSelling = true,
            IsBuying = false,
            IsDisabled = false,
            CopyAttachmentsToTransaction = true
        };

        Assert.Equal("Standard Sales Terms", tnc.Title);
        Assert.Contains("Payment due within 30 days", tnc.Terms);
        Assert.True(tnc.IsSelling);
        Assert.False(tnc.IsBuying);
        Assert.False(tnc.IsDisabled);
        Assert.True(tnc.CopyAttachmentsToTransaction);
    }

    [Fact]
    public void TermsAndConditions_Constructor_ThrowsOnEmptyTitle()
    {
        Assert.Throws<ArgumentException>(() =>
            new TermsAndConditions(Guid.NewGuid(), _companyId, "")
        );
    }

    [Fact]
    public void TermsAndConditions_Constructor_ThrowsOnEmptyCompany()
    {
        Assert.Throws<ArgumentException>(() =>
            new TermsAndConditions(Guid.NewGuid(), Guid.Empty, "Valid Title")
        );
    }
}
