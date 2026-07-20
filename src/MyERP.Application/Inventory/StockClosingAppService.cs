using System;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Inventory.DomainServices;
using MyERP.Inventory.Entities;
using MyERP.Permissions;
using MyERP.Shared;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Inventory;

public class StockClosingEntryDto : EntityDto<Guid>
{
    public Guid CompanyId { get; set; }
    public DateTime ToDate { get; set; }
    public int Status { get; set; }
    public int TotalEntries { get; set; }
    public decimal TotalStockValue { get; set; }
    public Guid? PreviousClosingEntryId { get; set; }
    public DateTime? ScannedFromDate { get; set; }
    public DateTime CreationTime { get; set; }
}

public class CreateStockClosingDto
{
    public Guid CompanyId { get; set; }
    public DateTime ToDate { get; set; }
}

/// <summary>
/// AppService for Stock Closing Entry — period-end stock balance snapshots.
/// Delegates to StockClosingService for incremental closing generation.
/// </summary>
[Authorize(MyERPPermissions.StockEntries.Default)]
public class StockClosingAppService : ApplicationService
{
    private readonly StockClosingService _closingService;
    private readonly IRepository<StockClosingEntry, Guid> _repository;

    public StockClosingAppService(
        StockClosingService closingService,
        IRepository<StockClosingEntry, Guid> repository)
    {
        _closingService = closingService;
        _repository = repository;
    }

    public async Task<PagedResultDto<StockClosingEntryDto>> GetListAsync(CompanyFilteredPagedRequestDto input)
    {
        var query = await _repository.GetQueryableAsync();

        if (input.CompanyId.HasValue)
            query = query.Where(x => x.CompanyId == input.CompanyId.Value);

        if (!string.IsNullOrWhiteSpace(input.Status) && Enum.TryParse<StockClosingStatus>(input.Status, true, out var status))
            query = query.Where(x => x.Status == status);

        var totalCount = query.Count();
        var items = query.OrderByDescending(c => c.ToDate)
            .Skip(input.SkipCount).Take(input.MaxResultCount).ToList();
        return new PagedResultDto<StockClosingEntryDto>(totalCount, items.Select(x => ObjectMapper.Map<StockClosingEntry, StockClosingEntryDto>(x)).ToList());
    }

    public async Task<StockClosingEntryDto> GetAsync(Guid id)
    {
        var entry = await _repository.GetAsync(id);
        return ObjectMapper.Map<StockClosingEntry, StockClosingEntryDto>(entry);
    }

    /// <summary>
    /// Generate a new stock closing entry for a company up to the specified date.
    /// Uses incremental logic (builds on previous closing + SLE delta).
    /// </summary>
    [Authorize(MyERPPermissions.StockEntries.Create)]
    public async Task<StockClosingEntryDto> GenerateAsync(CreateStockClosingDto input)
    {
        // Check no existing submitted closing covers this date
        var isCovered = await _closingService.IsDateCoveredByClosingAsync(input.CompanyId, input.ToDate);
        if (isCovered)
            throw new BusinessException("MyERP:05029")
                .WithData("toDate", input.ToDate.ToString("dd/MM/yyyy"));

        var closing = await _closingService.GenerateClosingAsync(
            input.CompanyId, input.ToDate, CurrentTenant.Id);
        return ObjectMapper.Map<StockClosingEntry, StockClosingEntryDto>(closing);
    }

    /// <summary>
    /// Submit a draft stock closing entry, freezing the data.
    /// </summary>
    [Authorize(MyERPPermissions.StockEntries.Submit)]
    public async Task<StockClosingEntryDto> SubmitAsync(Guid id)
    {
        var entry = await _repository.GetAsync(id);
        entry.Submit();
        await _repository.UpdateAsync(entry);
        return ObjectMapper.Map<StockClosingEntry, StockClosingEntryDto>(entry);
    }

    /// <summary>
    /// Cancel a submitted stock closing entry, allowing reposting for covered dates.
    /// </summary>
    [Authorize(MyERPPermissions.StockEntries.Cancel)]
    public async Task<StockClosingEntryDto> CancelAsync(Guid id)
    {
        var entry = await _repository.GetAsync(id);
        entry.Cancel();
        await _repository.UpdateAsync(entry);
        return ObjectMapper.Map<StockClosingEntry, StockClosingEntryDto>(entry);
    }
}
