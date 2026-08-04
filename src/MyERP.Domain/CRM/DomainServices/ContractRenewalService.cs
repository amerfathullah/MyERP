using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MyERP.CRM.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;
using Volo.Abp.Guids;

namespace MyERP.CRM.DomainServices;

/// <summary>
/// Domain service for managing contract renewals and expirations.
/// </summary>
public class ContractRenewalService : DomainService
{
    private readonly IRepository<Contract, Guid> _contractRepository;

    public ContractRenewalService(IRepository<Contract, Guid> contractRepository)
    {
        _contractRepository = contractRepository;
    }

    /// <summary>
    /// Processes all active contracts to check for expiration or auto-renewal.
    /// This would typically be called by a background job (e.g., NightlyProcessingWorker).
    /// </summary>
    public async Task ProcessRenewalsAsync(DateTime asOfDate)
    {
        // Find all active contracts that have an end date
        var activeContracts = await _contractRepository.GetListAsync(c => 
            c.Status == ContractStatus.Active && 
            c.EndDate.HasValue && 
            c.EndDate.Value <= asOfDate);

        foreach (var contract in activeContracts)
        {
            if (contract.IsAutoRenewal)
            {
                // Simple auto-renewal logic: extend by 1 year. 
                // A more complex system might look at the original duration.
                var newEndDate = contract.EndDate!.Value.AddYears(1);
                contract.Renew(newEndDate);
            }
            else
            {
                // Mark as inactive if not auto-renewing
                contract.MarkInactive();
            }

            await _contractRepository.UpdateAsync(contract);
        }
    }
}
