using System;
using System.Collections.Generic;
using MyERP.CRM;
using MyERP.CRM.Entities;
using MyERP.Sales.Entities;
using Volo.Abp;
using Xunit;

namespace MyERP.Domain.Tests.CrmTests;

/// <summary>
/// Unit tests for Contract, Subscription, and Sales Team validation rules:
/// - Contract status derived from signing and date range (Gotcha #1155)
/// - Subscription billing interval and calendar month alignment rules (Gotchas #544, #545)
/// - Sales Team allocated percentage 100% total invariant (Gotcha #301)
/// </summary>
public class ContractAndSubscriptionValidationTests
{
    private readonly Guid _companyId = Guid.NewGuid();
    private readonly Guid _partyId = Guid.NewGuid();

    [Fact]
    public void Contract_Unsigned_StatusIsUnsigned()
    {
        var contract = new Contract(Guid.NewGuid(), _companyId, "CTR-2026-0001", "Customer", _partyId, DateTime.UtcNow);
        contract.UpdateContractStatus(DateTime.UtcNow);

        Assert.Equal(ContractStatus.Unsigned, contract.Status);
    }

    [Fact]
    public void Contract_SignedWithinDates_StatusIsActive()
    {
        var now = DateTime.UtcNow;
        var contract = new Contract(Guid.NewGuid(), _companyId, "CTR-2026-0002", "Customer", _partyId, now.AddDays(-10))
        {
            EndDate = now.AddDays(30)
        };
        contract.Sign(now.AddDays(-10));
        contract.UpdateContractStatus(now);

        Assert.Equal(ContractStatus.Active, contract.Status);
    }

    [Fact]
    public void Contract_SignedPastEndDate_StatusIsInactiveByExpiry()
    {
        var now = DateTime.UtcNow;
        var contract = new Contract(Guid.NewGuid(), _companyId, "CTR-2026-0003", "Customer", _partyId, now.AddDays(-40))
        {
            EndDate = now.AddDays(-10)
        };
        contract.Sign(now.AddDays(-40));
        contract.UpdateContractStatus(now);

        Assert.Equal(ContractStatus.InactiveByExpiry, contract.Status);
    }

    [Fact]
    public void Subscription_EndDateLessThanOneCycle_ThrowsValidationException()
    {
        var start = DateTime.UtcNow.Date;
        var sub = new Subscription(Guid.NewGuid(), _companyId, _partyId, "Customer", start, "Monthly")
        {
            EndDate = start.AddDays(15) // Less than 1 month
        };

        var ex = Assert.Throws<BusinessException>(() => sub.ValidateSubscriptionPeriod());
        Assert.Equal(MyERPDomainErrorCodes.ValidationFailed, ex.Code);
        Assert.Contains("must exceed at least one full billing cycle", ex.Data["detail"]?.ToString());
    }

    [Fact]
    public void Subscription_FollowCalendarMonths_RequiresEndDateAndMonthlyInterval()
    {
        var start = DateTime.UtcNow.Date;
        var sub1 = new Subscription(Guid.NewGuid(), _companyId, _partyId, "Customer", start, "Monthly")
        {
            FollowCalendarMonths = true,
            EndDate = null // Missing EndDate
        };

        var ex1 = Assert.Throws<BusinessException>(() => sub1.ValidateSubscriptionPeriod());
        Assert.Equal(MyERPDomainErrorCodes.ValidationFailed, ex1.Code);
        Assert.Contains("End Date is mandatory", ex1.Data["detail"]?.ToString());

        var sub2 = new Subscription(Guid.NewGuid(), _companyId, _partyId, "Customer", start, "Quarterly")
        {
            FollowCalendarMonths = true,
            EndDate = start.AddMonths(6)
        };

        var ex2 = Assert.Throws<BusinessException>(() => sub2.ValidateSubscriptionPeriod());
        Assert.Equal(MyERPDomainErrorCodes.ValidationFailed, ex2.Code);
        Assert.Contains("Billing Interval must be Monthly", ex2.Data["detail"]?.ToString());
    }

    [Fact]
    public void SalesTeamEntry_ValidateAllocatedPercentages_Not100_ThrowsValidationException()
    {
        var entries = new List<SalesTeamEntry>
        {
            new(Guid.NewGuid(), Guid.NewGuid(), "SalesOrder", Guid.NewGuid(), 60m, 1000m, 5m),
            new(Guid.NewGuid(), Guid.NewGuid(), "SalesOrder", Guid.NewGuid(), 30m, 1000m, 5m) // Total = 90%
        };

        var ex = Assert.Throws<BusinessException>(() => SalesTeamEntry.ValidateAllocatedPercentages(entries));
        Assert.Equal(MyERPDomainErrorCodes.ValidationFailed, ex.Code);
        Assert.Contains("must sum to exactly 100%", ex.Data["detail"]?.ToString());
    }

    [Fact]
    public void SalesTeamEntry_ValidateAllocatedPercentages_Exact100_Succeeds()
    {
        var entries = new List<SalesTeamEntry>
        {
            new(Guid.NewGuid(), Guid.NewGuid(), "SalesOrder", Guid.NewGuid(), 60m, 1000m, 5m),
            new(Guid.NewGuid(), Guid.NewGuid(), "SalesOrder", Guid.NewGuid(), 40m, 1000m, 5m) // Total = 100%
        };

        SalesTeamEntry.ValidateAllocatedPercentages(entries); // No exception
    }
}
