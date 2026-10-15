using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using MyERP.Automation.Entities;
using MyERP.Permissions;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Automation;

[Authorize(MyERPPermissions.BulkTransactionLogs.Default)]
public class BulkTransactionLogAppService : MyERPAppService, IBulkTransactionLogAppService
{
    private readonly IRepository<BulkTransactionLog, Guid> _repository;

    public BulkTransactionLogAppService(IRepository<BulkTransactionLog, Guid> repository)
    {
        _repository = repository;
    }

    public async Task<BulkTransactionLogDto> GetAsync(Guid id)
    {
        var entity = await _repository.GetAsync(id);
        return new BulkTransactionLogMapper().Map(entity);
    }

    public async Task<PagedResultDto<BulkTransactionLogDto>> GetListAsync(GetBulkTransactionLogListDto input)
    {
        var query = await _repository.GetQueryableAsync();

        if (input.FromDate.HasValue)
        {
            query = query.Where(x => x.BatchDate >= input.FromDate.Value);
        }

        if (input.ToDate.HasValue)
        {
            query = query.Where(x => x.BatchDate <= input.ToDate.Value);
        }

        if (!string.IsNullOrWhiteSpace(input.Filter))
        {
            var filter = input.Filter.Trim().ToLower();
            query = query.Where(x => x.Title.ToLower().Contains(filter));
        }

        var totalCount = await AsyncExecuter.CountAsync(query);
        var entities = await AsyncExecuter.ToListAsync(
            query.OrderByDescending(x => x.BatchDate)
                 .Skip(input.SkipCount)
                 .Take(input.MaxResultCount));

        var dtos = entities.Select(new BulkTransactionLogMapper().Map).ToList();
        return new PagedResultDto<BulkTransactionLogDto>(totalCount, dtos);
    }

    [Authorize(MyERPPermissions.BulkTransactionLogs.Create)]
    public async Task<BulkTransactionLogDto> CreateAsync(CreateBulkTransactionLogDto input)
    {
        var entity = new BulkTransactionLog(
            GuidGenerator.Create(),
            input.Title.Trim(),
            input.BatchDate,
            CurrentTenant.Id);

        if (input.Details != null)
        {
            foreach (var d in input.Details)
            {
                entity.AddDetail(
                    GuidGenerator.Create(),
                    d.TransactionName.Trim(),
                    d.FromDocType.Trim(),
                    d.ToDocType.Trim());
            }
        }

        await _repository.InsertAsync(entity);
        return new BulkTransactionLogMapper().Map(entity);
    }

    [Authorize(MyERPPermissions.BulkTransactionLogs.Edit)]
    public async Task<BulkTransactionLogDto> RecordDetailResultAsync(Guid id, Guid detailId, RecordBulkTransactionResultDto input)
    {
        var entity = await _repository.GetAsync(id);

        if (input.IsSuccess)
        {
            entity.RecordSuccess(detailId);
        }
        else
        {
            entity.RecordFailure(detailId, input.ErrorDescription ?? "Failed without error description");
        }

        await _repository.UpdateAsync(entity);
        return new BulkTransactionLogMapper().Map(entity);
    }

    [Authorize(MyERPPermissions.BulkTransactionLogs.Edit)]
    public async Task<BulkTransactionLogDto> RetryDetailAsync(Guid id, Guid detailId)
    {
        var entity = await _repository.GetAsync(id);
        var detail = entity.Details.FirstOrDefault(d => d.Id == detailId);
        if (detail == null)
        {
            throw new UserFriendlyException($"Detail with id {detailId} not found");
        }

        // Per ERPNext PR #60007 / commit a439708aad: do not retry sources a later run already converted
        var allLogsQuery = await _repository.WithDetailsAsync(l => l.Details);
        var alreadySucceededLater = allLogsQuery
            .SelectMany(l => l.Details)
            .Any(d => d.TransactionName == detail.TransactionName
                   && d.FromDocType == detail.FromDocType
                   && d.ToDocType == detail.ToDocType
                   && d.Status == BulkTransactionStatus.Success
                   && d.CreationTime > detail.CreationTime);

        if (alreadySucceededLater)
        {
            detail.RetriedCount++;
            await _repository.UpdateAsync(entity);
            return new BulkTransactionLogMapper().Map(entity);
        }

        entity.RetryDetail(detailId);
        await _repository.UpdateAsync(entity);
        return new BulkTransactionLogMapper().Map(entity);
    }

    [Authorize(MyERPPermissions.BulkTransactionLogs.Delete)]
    public async Task DeleteAsync(Guid id)
    {
        await _repository.DeleteAsync(id);
    }
}
