using System;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Support.Entities;
using MyERP.Maintenance;
using MyERP.Maintenance.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;
using MyERP.Core;

namespace MyERP.Support.DomainServices;

public class MaintenanceManager : DomainService
{
    private readonly IRepository<MaintenanceVisit, Guid> _visitRepository;
    private readonly IRepository<WarrantyClaim, Guid> _warrantyClaimRepository;

    public MaintenanceManager(
        IRepository<MaintenanceVisit, Guid> visitRepository,
        IRepository<WarrantyClaim, Guid> warrantyClaimRepository)
    {
        _visitRepository = visitRepository;
        _warrantyClaimRepository = warrantyClaimRepository;
    }

    public async Task CompleteVisitAsync(Guid visitId, MaintenanceVisitStatus status)
    {
        var visit = await _visitRepository.GetAsync(visitId);
        
        if (status == MaintenanceVisitStatus.Completed)
        {
            visit.Complete();
        }
        else if (status == MaintenanceVisitStatus.PartiallyCompleted)
        {
            visit.PartiallyComplete();
        }

        await _visitRepository.UpdateAsync(visit);

        if (visit.WarrantyClaimId.HasValue)
        {
            var claim = await _warrantyClaimRepository.FindAsync(visit.WarrantyClaimId.Value);
            if (claim != null && claim.Status != WarrantyClaimStatus.Cancelled)
            {
                if (status == MaintenanceVisitStatus.Completed && claim.Status != WarrantyClaimStatus.Closed)
                {
                    var resolutionText = visit.Purposes.FirstOrDefault(p => !string.IsNullOrWhiteSpace(p.WorkDone))?.WorkDone
                        ?? $"Completed via Maintenance Visit {visit.Id.ToString()[..8]}";
                    claim.Close(resolutionText);
                    await _warrantyClaimRepository.UpdateAsync(claim);
                }
                else if (status == MaintenanceVisitStatus.PartiallyCompleted && claim.Status == WarrantyClaimStatus.Open)
                {
                    claim.StartWork();
                    await _warrantyClaimRepository.UpdateAsync(claim);
                }
            }
        }
    }
}
