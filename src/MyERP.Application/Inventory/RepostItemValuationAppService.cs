using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using MyERP.Inventory.Entities;
using MyERP.Permissions;
using MyERP.Shared;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Inventory;

/// <summary>
/// Application service for Repost Item Valuation tracking.
/// Per DO-NOT: "Skip advisory locking during SLE repost (causes data corruption under concurrency)"
/// Per DO-NOT: "Process repost item valuation outside configured timeslot"
/// </summary>
[Authorize(MyERPPermissions.StockEntries.Default)]
public class RepostItemValuationAppService : ApplicationService, IRepostItemValuationAppService
{
    private readonly IRepository<RepostItemValuation, Guid> _repository;

    public RepostItemValuationAppService(IRepository<RepostItemValuation, Guid> repository)
    {
        _repository = repository;
    }

    public async Task<PagedResultDto<RepostItemValuationDto>> GetListAsync(CompanyFilteredPagedRequestDto input)
    {
        var query = await _repository.GetQueryableAsync();
        if (input.CompanyId.HasValue)
            query = query.Where(x => x.CompanyId == input.CompanyId.Value);
        if (!string.IsNullOrWhiteSpace(input.Status) && Enum.TryParse<RepostStatus>(input.Status, true, out var status))
            query = query.Where(x => x.Status == status);

        var count = query.Count();
        var items = query.OrderByDescending(x => x.CreationTime)
            .Skip(input.SkipCount).Take(input.MaxResultCount).ToList();
        return new PagedResultDto<RepostItemValuationDto>(count, items.Select(ObjectMapper.Map<RepostItemValuation, RepostItemValuationDto>).ToList());
    }

    public async Task<RepostItemValuationDto> GetAsync(Guid id)
    {
        var entity = await _repository.GetAsync(id);
        return ObjectMapper.Map<RepostItemValuation, RepostItemValuationDto>(entity);
    }

    /// <summary>
    /// Creates a repost request. Checks for dedup (covered by existing queued/in-progress).
    /// </summary>
    [Authorize(MyERPPermissions.StockEntries.Create)]
    public async Task<RepostItemValuationDto> CreateAsync(CreateRepostItemValuationDto input)
    {
        var entity = new RepostItemValuation(GuidGenerator.Create(), input.CompanyId,
            (RepostMethod)input.BasedOn, input.PostingDate, input.ItemId, input.WarehouseId,
            CurrentTenant.Id);
        entity.RepostGlEntries = input.RepostGlEntries;
        entity.VoucherType = input.VoucherType;
        entity.VoucherId = input.VoucherId;

        // Check if covered by existing queued/in-progress repost
        var query = await _repository.GetQueryableAsync();
        var existingActive = query
            .Where(x => x.CompanyId == input.CompanyId
                        && (x.Status == RepostStatus.Queued || x.Status == RepostStatus.InProgress))
            .ToList();

        foreach (var existing in existingActive)
        {
            if (entity.IsCoveredBy(existing))
            {
                entity.MarkSkipped("Covered by existing repost: " + existing.Id);
                entity.IsDeduplicated = true;
                entity.DedupRepostId = existing.Id;
                break;
            }
        }

        await _repository.InsertAsync(entity);
        return ObjectMapper.Map<RepostItemValuation, RepostItemValuationDto>(entity);
    }

    /// <summary>Get count of pending (queued) reposts for dashboard.</summary>
    public async Task<int> GetPendingCountAsync(Guid companyId)
    {
        var query = await _repository.GetQueryableAsync();
        return query.Count(x => x.CompanyId == companyId && x.Status == RepostStatus.Queued);
    }

    /// <summary>
    /// Restarts a failed, skipped, or cancelled repost operation (Gotcha #6004).
    /// </summary>
    [Authorize(MyERPPermissions.StockEntries.Create)]
    public async Task<RepostItemValuationDto> RestartAsync(Guid id)
    {
        var entity = await _repository.GetAsync(id);
        entity.Restart();

        await _repository.UpdateAsync(entity);
        return MapToDto(entity);
    }

    /// <summary>
    /// Cancels a queued, in-progress, or failed repost operation (Gotcha #6004).
    /// </summary>
    [Authorize(MyERPPermissions.StockEntries.Create)]
    public async Task<RepostItemValuationDto> CancelAsync(Guid id)
    {
        var entity = await _repository.GetAsync(id);
        entity.Cancel();

        await _repository.UpdateAsync(entity);
        return MapToDto(entity);
    }

    /// <summary>
    /// Computes summary metrics for stock repost operations (Gotcha #6004).
    /// </summary>
    public async Task<RepostItemValuationSummaryDto> GetSummaryAsync(Guid companyId)
    {
        var query = await _repository.GetQueryableAsync();
        var companyReposts = query.Where(x => x.CompanyId == companyId).ToList();

        var lastProcessed = companyReposts
            .Where(x => x.Status == RepostStatus.Completed)
            .OrderByDescending(x => x.LastModificationTime ?? x.CreationTime)
            .Select(x => (DateTime?)(x.LastModificationTime ?? x.CreationTime))
            .FirstOrDefault();

        return new RepostItemValuationSummaryDto
        {
            CompanyId = companyId,
            QueuedCount = companyReposts.Count(x => x.Status == RepostStatus.Queued),
            InProgressCount = companyReposts.Count(x => x.Status == RepostStatus.InProgress),
            CompletedCount = companyReposts.Count(x => x.Status == RepostStatus.Completed),
            FailedCount = companyReposts.Count(x => x.Status == RepostStatus.Failed),
            SkippedCount = companyReposts.Count(x => x.Status == RepostStatus.Skipped),
            CancelledCount = companyReposts.Count(x => x.Status == RepostStatus.Cancelled),
            TotalEntriesProcessed = companyReposts.Sum(x => x.CurrentIndex),
            LastProcessedDate = lastProcessed
        };
    }

    private static RepostItemValuationDto MapToDto(RepostItemValuation entity)
    {
        return new RepostItemValuationDto
        {
            Id = entity.Id,
            CompanyId = entity.CompanyId,
            BasedOn = (int)entity.BasedOn,
            ItemId = entity.ItemId,
            WarehouseId = entity.WarehouseId,
            PostingDate = entity.PostingDate,
            Status = (int)entity.Status,
            RepostGlEntries = entity.RepostGlEntries,
            TotalAffectedEntries = entity.TotalAffectedEntries,
            CurrentIndex = entity.CurrentIndex,
            ErrorLog = entity.ErrorLog,
            VoucherType = entity.VoucherType,
            VoucherId = entity.VoucherId,
            IsDeduplicated = entity.IsDeduplicated,
            CreationTime = entity.CreationTime
        };
    }
}
