using System;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Inventory.Entities;
using MyERP.Permissions;
using MyERP.Shared;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Inventory;

#region DTOs

public class RepostItemValuationDto : EntityDto<Guid>
{
    public Guid CompanyId { get; set; }
    public int BasedOn { get; set; }
    public Guid? ItemId { get; set; }
    public Guid? WarehouseId { get; set; }
    public DateTime PostingDate { get; set; }
    public int Status { get; set; }
    public bool RepostGlEntries { get; set; }
    public int TotalAffectedEntries { get; set; }
    public int CurrentIndex { get; set; }
    public string? ErrorLog { get; set; }
    public string? VoucherType { get; set; }
    public Guid? VoucherId { get; set; }
    public bool IsDeduplicated { get; set; }
    public DateTime CreationTime { get; set; }
}

public class CreateRepostItemValuationDto
{
    public Guid CompanyId { get; set; }
    public int BasedOn { get; set; }
    public Guid? ItemId { get; set; }
    public Guid? WarehouseId { get; set; }
    public DateTime PostingDate { get; set; }
    public bool RepostGlEntries { get; set; } = true;
    public string? VoucherType { get; set; }
    public Guid? VoucherId { get; set; }
}

#endregion

/// <summary>
/// Application service for Repost Item Valuation tracking.
/// Per DO-NOT: "Skip advisory locking during SLE repost (causes data corruption under concurrency)"
/// Per DO-NOT: "Process repost item valuation outside configured timeslot"
/// </summary>
[Authorize(MyERPPermissions.StockEntries.Default)]
public class RepostItemValuationAppService : ApplicationService
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
}
