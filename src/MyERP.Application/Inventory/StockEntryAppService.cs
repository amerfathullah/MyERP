using System;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Core.DomainServices;
using MyERP.Inventory.Entities;
using MyERP.Permissions;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Inventory;

[Authorize(MyERPPermissions.StockEntries.Default)]
public class StockEntryAppService : ApplicationService, IStockEntryAppService
{
    private readonly IRepository<StockEntry, Guid> _repository;
    private readonly IDocumentNumberGenerator _numberGenerator;

    public StockEntryAppService(
        IRepository<StockEntry, Guid> repository,
        IDocumentNumberGenerator numberGenerator)
    {
        _repository = repository;
        _numberGenerator = numberGenerator;
    }

    public async Task<StockEntryDto> GetAsync(Guid id)
    {
        var entry = await _repository.GetAsync(id);
        return MapToDto(entry);
    }

    public async Task<PagedResultDto<StockEntryDto>> GetListAsync(PagedAndSortedResultRequestDto input)
    {
        var totalCount = await _repository.GetCountAsync();
        var entries = await _repository.GetPagedListAsync(
            input.SkipCount, input.MaxResultCount, input.Sorting ?? "PostingDate DESC");

        return new PagedResultDto<StockEntryDto>(
            totalCount,
            entries.Select(MapToDto).ToList());
    }

    [Authorize(MyERPPermissions.StockEntries.Create)]
    public async Task<StockEntryDto> CreateAsync(CreateStockEntryDto input)
    {
        var entryNumber = await _numberGenerator.GenerateAsync("StockEntry", input.CompanyId);

        var entry = new StockEntry(
            GuidGenerator.Create(),
            input.CompanyId,
            input.EntryType,
            input.PostingDate);

        entry.EntryNumber = entryNumber;
        entry.ReferenceType = input.ReferenceType;
        entry.ReferenceId = input.ReferenceId;
        entry.Notes = input.Notes;

        foreach (var item in input.Items)
        {
            entry.AddItem(item.ItemId, item.Quantity, item.SourceWarehouseId, item.TargetWarehouseId, item.ValuationRate);
        }

        await _repository.InsertAsync(entry, autoSave: true);
        return MapToDto(entry);
    }

    [Authorize(MyERPPermissions.StockEntries.Submit)]
    public async Task<StockEntryDto> SubmitAsync(Guid id)
    {
        var entry = await _repository.GetAsync(id);
        entry.Submit();
        await _repository.UpdateAsync(entry, autoSave: true);
        return MapToDto(entry);
    }

    [Authorize(MyERPPermissions.StockEntries.Post)]
    public async Task<StockEntryDto> PostAsync(Guid id)
    {
        var entry = await _repository.GetAsync(id);
        entry.Post();
        await _repository.UpdateAsync(entry, autoSave: true);
        return MapToDto(entry);
    }

    [Authorize(MyERPPermissions.StockEntries.Cancel)]
    public async Task<StockEntryDto> CancelAsync(Guid id)
    {
        var entry = await _repository.GetAsync(id);
        entry.Cancel();
        await _repository.UpdateAsync(entry, autoSave: true);
        return MapToDto(entry);
    }

    private StockEntryDto MapToDto(StockEntry entry)
    {
        return new StockEntryDto
        {
            Id = entry.Id,
            CompanyId = entry.CompanyId,
            EntryNumber = entry.EntryNumber,
            EntryType = entry.EntryType,
            PostingDate = entry.PostingDate,
            ReferenceType = entry.ReferenceType,
            ReferenceId = entry.ReferenceId,
            Notes = entry.Notes,
            Status = entry.Status.ToString(),
            CreationTime = entry.CreationTime,
            LastModificationTime = entry.LastModificationTime,
            Items = entry.Items.Select(i => new StockEntryItemDto
            {
                Id = i.Id,
                ItemId = i.ItemId,
                Quantity = i.Quantity,
                SourceWarehouseId = i.SourceWarehouseId,
                TargetWarehouseId = i.TargetWarehouseId,
                ValuationRate = i.ValuationRate,
            }).ToList(),
        };
    }
}
