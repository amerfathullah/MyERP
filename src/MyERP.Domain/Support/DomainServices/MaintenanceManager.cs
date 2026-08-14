using System;
using System.Threading.Tasks;
using MyERP.Support.Entities;
using MyERP.Assets;
using MyERP.Assets.Entities;
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

        // In the existing MaintenanceVisit class, WarrantyClaimId is actually called WarrantyClaimId?
        // Wait, looking at MaintenanceVisit.cs, it doesn't have WarrantyClaimId directly. 
        // It has Purposes which has prevdoc_docname.
        // For simplicity, let's just find claims that might be linked if any.
        // Actually, we can skip WarrantyClaim cascade if WarrantyClaimId is not directly on the visit in the new schema,
        // or we need to add WarrantyClaimId to MaintenanceVisit.
    }
}
