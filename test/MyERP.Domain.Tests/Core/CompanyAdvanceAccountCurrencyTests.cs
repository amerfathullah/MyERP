using System;
using MyERP.Core;
using MyERP.Core.Entities;
using Xunit;

namespace MyERP.Domain.Tests.Core;

/// <summary>
/// Unit tests for Company Advance Payment accounts properties and DTO mappings (Gotcha #510).
/// </summary>
public class CompanyAdvanceAccountCurrencyTests
{
    [Fact]
    public void Company_AdvanceAccounts_PropertiesCanBeSet()
    {
        var company = new Company(Guid.NewGuid(), "Test Co", Guid.NewGuid())
        {
            CurrencyCode = "MYR",
            BookAdvancePaymentsInSeparatePartyAccount = true,
            DefaultAdvanceReceivedAccountId = Guid.NewGuid(),
            DefaultAdvancePaidAccountId = Guid.NewGuid()
        };

        Assert.True(company.BookAdvancePaymentsInSeparatePartyAccount);
        Assert.NotNull(company.DefaultAdvanceReceivedAccountId);
        Assert.NotNull(company.DefaultAdvancePaidAccountId);
    }

    [Fact]
    public void CreateUpdateCompanyDto_AdvanceAccounts_MappedProperly()
    {
        var receivedId = Guid.NewGuid();
        var paidId = Guid.NewGuid();

        var dto = new CreateUpdateCompanyDto
        {
            Name = "Acme Corp",
            CurrencyCode = "MYR",
            BookAdvancePaymentsInSeparatePartyAccount = true,
            DefaultAdvanceReceivedAccountId = receivedId,
            DefaultAdvancePaidAccountId = paidId
        };

        Assert.True(dto.BookAdvancePaymentsInSeparatePartyAccount);
        Assert.Equal(receivedId, dto.DefaultAdvanceReceivedAccountId);
        Assert.Equal(paidId, dto.DefaultAdvancePaidAccountId);
    }
}
