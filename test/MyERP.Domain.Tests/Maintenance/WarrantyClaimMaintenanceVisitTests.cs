using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;
using MyERP.Inventory;
using MyERP.Inventory.Entities;
using MyERP.Maintenance;
using MyERP.Maintenance.Entities;
using MyERP.Sales.Entities;
using NSubstitute;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace MyERP.Domain.Tests.Maintenance;

/// <summary>
/// Unit tests for Warranty Claim maintenance visit workflow & cancel guards.
/// Verifies rules migrated from erpnext/support/doctype/warranty_claim/warranty_claim.py (Gotcha #851 / #829).
/// </summary>
public class WarrantyClaimMaintenanceVisitTests
{
    private readonly IRepository<WarrantyClaim, Guid> _claimRepository = Substitute.For<IRepository<WarrantyClaim, Guid>>();
    private readonly IRepository<Customer, Guid> _customerRepository = Substitute.For<IRepository<Customer, Guid>>();
    private readonly IRepository<Item, Guid> _itemRepository = Substitute.For<IRepository<Item, Guid>>();
    private readonly IRepository<MaintenanceVisit, Guid> _visitRepository = Substitute.For<IRepository<MaintenanceVisit, Guid>>();

    private readonly WarrantyClaimAppService _appService;
    private readonly Guid _companyId = Guid.NewGuid();
    private readonly Guid _customerId = Guid.NewGuid();
    private readonly Guid _itemId = Guid.NewGuid();
    private readonly Guid _claimId = Guid.NewGuid();

    public WarrantyClaimMaintenanceVisitTests()
    {
        _appService = new WarrantyClaimAppService(_claimRepository, _customerRepository, _itemRepository, _visitRepository);
    }

    [Fact]
    public async Task CreateMaintenanceVisitAsync_CreatesVisitLinkedToClaim()
    {
        var claim = new WarrantyClaim(_claimId, _companyId, _customerId, _itemId, DateTime.UtcNow)
        {
            ClaimNumber = "WC-2026-0001",
            Complaint = "Screen flickering issue"
        };
        _claimRepository.GetAsync(_claimId).Returns(claim);
        _itemRepository.FindAsync(_itemId).Returns(new Item(_itemId, _companyId, "ITM-001", "Laptop Display", ItemType.Goods));

        MaintenanceVisit? createdVisit = null;
        await _visitRepository.InsertAsync(Arg.Do<MaintenanceVisit>(v => createdVisit = v));

        var visitId = await _appService.CreateMaintenanceVisitAsync(_claimId);

        Assert.NotNull(createdVisit);
        Assert.Equal(_claimId, createdVisit.WarrantyClaimId);
        Assert.Equal(_customerId, createdVisit.CustomerId);
        Assert.Equal("Breakdown", createdVisit.MaintenanceType);
        Assert.Single(createdVisit.Purposes);
        Assert.Contains("Screen flickering issue", createdVisit.Purposes[0].WorkDone);
    }

    [Fact]
    public async Task CancelAsync_WithActiveMaintenanceVisits_ThrowsValidationException()
    {
        var claim = new WarrantyClaim(_claimId, _companyId, _customerId, _itemId, DateTime.UtcNow);
        _claimRepository.GetAsync(_claimId).Returns(claim);

        var activeVisit = new MaintenanceVisit(Guid.NewGuid(), _companyId, DateTime.UtcNow, "Breakdown")
        {
            WarrantyClaimId = _claimId
        };
        // Visit is Open (not cancelled)
        _visitRepository.GetListAsync(Arg.Any<Expression<Func<MaintenanceVisit, bool>>>())
            .Returns(new List<MaintenanceVisit> { activeVisit });

        // Cancel must be blocked
        await Assert.ThrowsAsync<BusinessException>(() => _appService.CancelAsync(_claimId));
    }

    [Fact]
    public async Task CancelAsync_WithCancelledMaintenanceVisits_Succeeds()
    {
        var claim = new WarrantyClaim(_claimId, _companyId, _customerId, _itemId, DateTime.UtcNow);
        _claimRepository.GetAsync(_claimId).Returns(claim);

        var cancelledVisit = new MaintenanceVisit(Guid.NewGuid(), _companyId, DateTime.UtcNow, "Breakdown")
        {
            WarrantyClaimId = _claimId
        };
        cancelledVisit.Cancel();

        _visitRepository.GetListAsync(Arg.Any<Expression<Func<MaintenanceVisit, bool>>>())
            .Returns(new List<MaintenanceVisit> { cancelledVisit });

        await _appService.CancelAsync(_claimId);
        Assert.Equal(WarrantyClaimStatus.Cancelled, claim.Status);
    }
}
