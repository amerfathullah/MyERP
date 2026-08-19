using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MyERP.CRM.Entities;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace MyERP.CRM.BackgroundJobs;

/// <summary>
/// Updates contract lifecycle status for expired contracts.
/// ERPNext equivalent: crm/doctype/contract/contract.py update_status_for_contracts (daily scheduler).
/// </summary>
public class ContractStatusUpdateJob : AsyncBackgroundJob<ContractStatusUpdateJobArgs>, ITransientDependency
{
    private readonly IRepository<Contract, Guid> _repository;
    private readonly ILogger<ContractStatusUpdateJob> _logger;

    public ContractStatusUpdateJob(IRepository<Contract, Guid> repository, ILogger<ContractStatusUpdateJob> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public override async Task ExecuteAsync(ContractStatusUpdateJobArgs args)
    {
        var asOfDate = args.AsOfDate ?? DateTime.UtcNow.Date;

        var query = await _repository.GetQueryableAsync();
        var expiredContracts = query
            .Where(c => c.CompanyId == args.CompanyId &&
                        c.Status == ContractStatus.Active &&
                        c.EndDate.HasValue &&
                        c.EndDate.Value < asOfDate)
            .ToList();

        var updatedCount = 0;
        foreach (var contract in expiredContracts)
        {
            contract.MarkInactive(failedRenewal: false);
            await _repository.UpdateAsync(contract);
            updatedCount++;
        }

        _logger.LogInformation("ContractStatusUpdateJob transitioned {Count} expired contracts to InactiveByExpiry for company {CompanyId} as of {Date}",
            updatedCount, args.CompanyId, asOfDate);
    }
}

public class ContractStatusUpdateJobArgs
{
    public Guid CompanyId { get; set; }
    public Guid? TenantId { get; set; }
    public DateTime? AsOfDate { get; set; }
}
