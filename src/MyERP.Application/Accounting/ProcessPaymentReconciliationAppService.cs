using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using MyERP.Accounting.BackgroundJobs;
using MyERP.Permissions;
using MyERP.Shared;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Accounting;

/// <summary>
/// Automated counterpart to the manual Payment Reconciliation tool: greedy-matches a party's
/// unreconciled payments against its outstanding invoices via a background job instead of a user
/// picking allocations by hand. See <see cref="Entities.ProcessPaymentReconciliation"/> for lifecycle.
/// </summary>
[Authorize(MyERPPermissions.ProcessPaymentReconciliation.Default)]
public class ProcessPaymentReconciliationAppService : ApplicationService, IProcessPaymentReconciliationAppService
{
    private readonly IRepository<Entities.ProcessPaymentReconciliation, Guid> _repository;
    private readonly IBackgroundJobManager _jobManager;

    public ProcessPaymentReconciliationAppService(
        IRepository<Entities.ProcessPaymentReconciliation, Guid> repository,
        IBackgroundJobManager jobManager)
    {
        _repository = repository;
        _jobManager = jobManager;
    }

    public async Task<PagedResultDto<ProcessPaymentReconciliationDto>> GetListAsync(CompanyFilteredPagedRequestDto input)
    {
        var query = await _repository.GetQueryableAsync();
        if (input.CompanyId.HasValue)
            query = query.Where(x => x.CompanyId == input.CompanyId.Value);

        var count = query.Count();
        var items = query.OrderByDescending(x => x.CreationTime)
            .Skip(input.SkipCount).Take(input.MaxResultCount).ToList();

        return new PagedResultDto<ProcessPaymentReconciliationDto>(count, items.Select(ToDto).ToList());
    }

    public async Task<ProcessPaymentReconciliationDto> GetAsync(Guid id)
    {
        var entity = await _repository.GetAsync(id);
        return ToDto(entity);
    }

    [Authorize(MyERPPermissions.ProcessPaymentReconciliation.Create)]
    public async Task<ProcessPaymentReconciliationDto> CreateAsync(CreateProcessPaymentReconciliationDto input)
    {
        var entity = new Entities.ProcessPaymentReconciliation(
            GuidGenerator.Create(), input.CompanyId, input.PartyType, input.PartyId,
            input.ReceivablePayableAccountId, input.DefaultAdvanceAccountId, CurrentTenant.Id);

        await _repository.InsertAsync(entity);
        return ToDto(entity);
    }

    /// <summary>Blocks Submit when another Queued/Running request already exists for the same
    /// (company, party type, party, receivable/payable account, advance account) combination —
    /// per ERPNext's dedup gate, prevents two concurrent auto-reconciliations racing the same party's
    /// PLE and over-allocating.</summary>
    [Authorize(MyERPPermissions.ProcessPaymentReconciliation.Submit)]
    public async Task<ProcessPaymentReconciliationDto> SubmitAsync(Guid id)
    {
        var entity = await _repository.GetAsync(id);

        var query = await _repository.GetQueryableAsync();
        var alreadyActive = query.Any(x =>
            x.Id != entity.Id
            && x.CompanyId == entity.CompanyId
            && x.PartyType == entity.PartyType
            && x.PartyId == entity.PartyId
            && x.ReceivablePayableAccountId == entity.ReceivablePayableAccountId
            && x.DefaultAdvanceAccountId == entity.DefaultAdvanceAccountId
            && (x.Status == Entities.ProcessPaymentReconciliationStatus.Queued
                || x.Status == Entities.ProcessPaymentReconciliationStatus.Running));

        if (alreadyActive)
            throw new BusinessException(MyERPDomainErrorCodes.ProcessPaymentReconciliationAlreadyActive);

        entity.Submit();
        await _repository.UpdateAsync(entity, autoSave: true);

        await _jobManager.EnqueueAsync(new ProcessPaymentReconciliationJobArgs
        {
            RequestId = entity.Id,
            TenantId = entity.TenantId,
        });

        return ToDto(entity);
    }

    [Authorize(MyERPPermissions.ProcessPaymentReconciliation.Cancel)]
    public async Task<ProcessPaymentReconciliationDto> CancelAsync(Guid id)
    {
        var entity = await _repository.GetAsync(id);
        entity.Cancel();
        await _repository.UpdateAsync(entity, autoSave: true);
        return ToDto(entity);
    }

    private static ProcessPaymentReconciliationDto ToDto(Entities.ProcessPaymentReconciliation entity) => new()
    {
        Id = entity.Id,
        CompanyId = entity.CompanyId,
        PartyType = entity.PartyType,
        PartyId = entity.PartyId,
        ReceivablePayableAccountId = entity.ReceivablePayableAccountId,
        DefaultAdvanceAccountId = entity.DefaultAdvanceAccountId,
        Status = (int)entity.Status,
        StatusName = entity.Status.ToString(),
        ReconciledCount = entity.ReconciledCount,
        ErrorLog = entity.ErrorLog,
        CreationTime = entity.CreationTime,
    };
}
