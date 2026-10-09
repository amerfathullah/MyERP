using System;
using MyERP.CRM;
using MyERP.CRM.Entities;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace MyERP.Domain.Tests.CRM;

/// <summary>
/// Tests for Contract domain enhancements migrated from ERPNext PR #59893:
/// - ValidateDates (commit 493af58b5f)
/// - Inactive before start date (commit ecc2b9d5d3)
/// - Cannot add/remove fulfilment terms once signed (commit d5591d9792)
/// - Lapsed fulfilment status past deadline (commit 83768f6963)
/// </summary>
public class ContractLifecycleAndChecklistTests
{
    private readonly Guid _companyId = Guid.NewGuid();
    private readonly Guid _partyId = Guid.NewGuid();

    [Fact]
    public void ValidateDates_Throws_WhenEndDateBeforeStartDate()
    {
        var contract = new Contract(Guid.NewGuid(), _companyId, "CTR-001", "Customer", _partyId, DateTime.UtcNow.Date)
        {
            EndDate = DateTime.UtcNow.Date.AddDays(-1)
        };

        var ex = Should.Throw<BusinessException>(() => contract.ValidateDates());
        ex.Code.ShouldBe(MyERPDomainErrorCodes.ContractEndDateBeforeStartDate);
    }

    [Fact]
    public void ValidateDates_Passes_WhenEndDateAfterStartDate_Or_Null()
    {
        var contract = new Contract(Guid.NewGuid(), _companyId, "CTR-002", "Customer", _partyId, DateTime.UtcNow.Date)
        {
            EndDate = DateTime.UtcNow.Date.AddDays(30)
        };
        contract.ValidateDates();

        contract.EndDate = null;
        contract.ValidateDates();
    }

    [Fact]
    public void Sign_Throws_WhenEndDateBeforeStartDate()
    {
        var contract = new Contract(Guid.NewGuid(), _companyId, "CTR-003", "Customer", _partyId, DateTime.UtcNow.Date)
        {
            EndDate = DateTime.UtcNow.Date.AddDays(-5)
        };

        Should.Throw<BusinessException>(() => contract.Sign(DateTime.UtcNow.Date));
    }

    [Fact]
    public void Sign_SetsInactive_WhenStartDateInFuture()
    {
        var today = DateTime.UtcNow.Date;
        var futureStart = today.AddDays(10);
        var contract = new Contract(Guid.NewGuid(), _companyId, "CTR-004", "Customer", _partyId, futureStart);

        contract.Sign(today);

        // Per PR #59893 commit ecc2b9d5d3: contract remains inactive until its start date arrives
        contract.Status.ShouldBe(ContractStatus.InactiveByExpiry);
    }

    [Fact]
    public void Sign_SetsActive_WhenStartDateTodayOrPast()
    {
        var today = DateTime.UtcNow.Date;
        var contract = new Contract(Guid.NewGuid(), _companyId, "CTR-005", "Customer", _partyId, today);

        contract.Sign(today);

        contract.Status.ShouldBe(ContractStatus.Active);
    }

    [Fact]
    public void UpdateContractStatus_ActivatesContract_WhenStartDateArrived()
    {
        var signingDate = DateTime.UtcNow.Date.AddDays(-10);
        var startDate = DateTime.UtcNow.Date;
        var contract = new Contract(Guid.NewGuid(), _companyId, "CTR-006", "Customer", _partyId, startDate);

        contract.Sign(signingDate);
        contract.Status.ShouldBe(ContractStatus.InactiveByExpiry);

        contract.UpdateContractStatus(startDate);
        contract.Status.ShouldBe(ContractStatus.Active);
    }

    [Fact]
    public void AddFulfilmentItem_Throws_WhenContractAlreadySigned()
    {
        var today = DateTime.UtcNow.Date;
        var contract = new Contract(Guid.NewGuid(), _companyId, "CTR-007", "Customer", _partyId, today);
        contract.Sign(today);

        var item = new ContractFulfilmentChecklistItem(Guid.NewGuid(), contract.Id, "Deliver 10 units");

        var ex = Should.Throw<BusinessException>(() => contract.AddFulfilmentChecklistItem(item));
        ex.Code.ShouldBe(MyERPDomainErrorCodes.FulfilmentTermsCannotBeModifiedAfterSigning);
    }

    [Fact]
    public void RemoveFulfilmentItem_Throws_WhenContractAlreadySigned()
    {
        var today = DateTime.UtcNow.Date;
        var contract = new Contract(Guid.NewGuid(), _companyId, "CTR-008", "Customer", _partyId, today);
        var item = new ContractFulfilmentChecklistItem(Guid.NewGuid(), contract.Id, "Deliver 10 units");
        contract.AddFulfilmentChecklistItem(item);

        contract.Sign(today);

        var ex = Should.Throw<BusinessException>(() => contract.RemoveFulfilmentChecklistItem(item.Id));
        ex.Code.ShouldBe(MyERPDomainErrorCodes.FulfilmentTermsCannotBeModifiedAfterSigning);
    }

    [Fact]
    public void RecalculateFulfilmentStatus_Lapses_WhenDeadlinePassedAndUnfulfilled()
    {
        var today = DateTime.UtcNow.Date;
        var contract = new Contract(Guid.NewGuid(), _companyId, "CTR-009", "Customer", _partyId, today.AddDays(-20))
        {
            RequiresFulfilment = true,
            FulfilmentDeadline = today.AddDays(-5),
        };
        var item = new ContractFulfilmentChecklistItem(Guid.NewGuid(), contract.Id, "Deliver 10 units");
        contract.AddFulfilmentChecklistItem(item);

        contract.RecalculateFulfilmentStatus(today);
        contract.FulfilmentStatus.ShouldBe(ContractFulfilmentStatus.Lapsed);
    }
}
