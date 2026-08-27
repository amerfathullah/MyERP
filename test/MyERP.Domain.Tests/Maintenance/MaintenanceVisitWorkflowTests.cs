using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MyERP.Core;
using MyERP.Maintenance;
using MyERP.Maintenance.Entities;
using NSubstitute;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace MyERP.Domain.Tests.Maintenance;

/// <summary>
/// Unit tests for Maintenance Visit Warranty Claim Linking and Summary Metrics.
/// Verifies rules from erpnext/maintenance/doctype/maintenance_visit (#6010 / #4171).
/// </summary>
public class MaintenanceVisitWorkflowTests
{
    private readonly IRepository<MaintenanceVisit, Guid> _visitRepo = Substitute.For<IRepository<MaintenanceVisit, Guid>>();
    private readonly IRepository<WarrantyClaim, Guid> _claimRepo = Substitute.For<IRepository<WarrantyClaim, Guid>>();
    private readonly MaintenanceVisitAppService _appService;

    private readonly Guid _companyId = Guid.NewGuid();
    private readonly Guid _customerId = Guid.NewGuid();
    private readonly Guid _itemId = Guid.NewGuid();
    private readonly Guid _serialNoId = Guid.NewGuid();

    public MaintenanceVisitWorkflowTests()
    {
        _appService = new MaintenanceVisitAppService(_visitRepo, _claimRepo);
    }

    [Fact]
    public async Task MakeFromWarrantyClaimAsync_ValidClaim_PrepopulatesVisitDto()
    {
        var claimId = Guid.NewGuid();
        var claim = new WarrantyClaim(claimId, _companyId, _customerId, _itemId, new DateTime(2026, 6, 1))
        {
            SerialNoId = _serialNoId,
            Complaint = "Motor overheating during continuous cycle"
        };

        _claimRepo.GetAsync(claimId).Returns(Task.FromResult(claim));

        var dto = await _appService.MakeFromWarrantyClaimAsync(claimId);

        Assert.NotNull(dto);
        Assert.Equal(_companyId, dto.CompanyId);
        Assert.Equal(_customerId, dto.CustomerId);
        Assert.Equal(claimId, dto.WarrantyClaimId);
        Assert.Equal(2, dto.MaintenanceType); // Breakdown
        Assert.Single(dto.Purposes);
        var purpose = dto.Purposes[0];
        Assert.Equal(_itemId, purpose.ItemId);
        Assert.Equal(_serialNoId, purpose.SerialNoId);
        Assert.Equal("Motor overheating during continuous cycle", purpose.WorkDone);
    }

    [Fact]
    public async Task MakeFromWarrantyClaimAsync_CancelledClaim_ThrowsValidationException()
    {
        var claimId = Guid.NewGuid();
        var claim = new WarrantyClaim(claimId, _companyId, _customerId, _itemId, new DateTime(2026, 6, 1));
        claim.Cancel();

        _claimRepo.GetAsync(claimId).Returns(Task.FromResult(claim));

        var ex = await Assert.ThrowsAsync<BusinessException>(() => _appService.MakeFromWarrantyClaimAsync(claimId));
        Assert.Equal(MyERPDomainErrorCodes.InvalidStatusTransition, ex.Code);
    }

    [Fact]
    public async Task GetSummaryAsync_ReturnsAccurateMetrics()
    {
        var visitId = Guid.NewGuid();
        var visit = new MaintenanceVisit(visitId, _companyId, new DateTime(2026, 6, 10), "Scheduled")
        {
            CustomerId = _customerId
        };
        visit.AddPurpose(new MaintenanceVisitPurpose(Guid.NewGuid(), visitId, "Check oil level"));
        visit.AddPurpose(new MaintenanceVisitPurpose(Guid.NewGuid(), visitId, "Replace air filter"));

        _visitRepo.GetAsync(visitId).Returns(Task.FromResult(visit));

        var summary = await _appService.GetSummaryAsync(visitId);

        Assert.NotNull(summary);
        Assert.Equal(visitId, summary.Id);
        Assert.Equal("Scheduled", summary.MaintenanceType);
        Assert.Equal(2, summary.TotalPurposesCount);
        Assert.True(summary.CanSubmit);
        Assert.False(summary.CanCancel);
    }
}
