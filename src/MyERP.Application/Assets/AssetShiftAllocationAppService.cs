using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using MyERP.Assets.DomainServices;
using MyERP.Assets.Entities;
using MyERP.Permissions;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Assets;

[Authorize(MyERPPermissions.Assets.Default)]
public class AssetShiftAllocationAppService : ApplicationService, IAssetShiftAllocationAppService
{
    private readonly IRepository<AssetShiftAllocation, Guid> _repository;
    private readonly IRepository<DepreciationScheduleEntry, Guid> _scheduleRepository;
    private readonly IRepository<AssetShiftFactor, Guid> _shiftFactorRepository;
    private readonly AssetShiftReallocationService _reallocationService;

    public AssetShiftAllocationAppService(
        IRepository<AssetShiftAllocation, Guid> repository,
        IRepository<DepreciationScheduleEntry, Guid> scheduleRepository,
        IRepository<AssetShiftFactor, Guid> shiftFactorRepository,
        AssetShiftReallocationService reallocationService)
    {
        _repository = repository;
        _scheduleRepository = scheduleRepository;
        _shiftFactorRepository = shiftFactorRepository;
        _reallocationService = reallocationService;
    }

    public async Task<AssetShiftAllocationDto> GetAsync(Guid id)
    {
        var query = await _repository.WithDetailsAsync(a => a.Lines);
        var entity = await AsyncExecuter.FirstOrDefaultAsync(query, a => a.Id == id);
        if (entity == null) throw new BusinessException(MyERPDomainErrorCodes.EntityNotFound);
        return await MapToDtoAsync(entity);
    }

    public async Task<PagedResultDto<AssetShiftAllocationDto>> GetListAsync(PagedAndSortedResultRequestDto input)
    {
        var query = await _repository.WithDetailsAsync(a => a.Lines);
        var totalCount = await AsyncExecuter.CountAsync(query);
        var items = await AsyncExecuter.ToListAsync(
            query.OrderByDescending(a => a.CreationTime)
                .Skip(input.SkipCount)
                .Take(input.MaxResultCount));

        var dtos = new List<AssetShiftAllocationDto>();
        foreach (var item in items) dtos.Add(await MapToDtoAsync(item));
        return new PagedResultDto<AssetShiftAllocationDto>(totalCount, dtos);
    }

    public async Task<PagedResultDto<DepreciationScheduleDto>> GetUnbookedScheduleAsync(Guid assetId, Guid? financeBookId)
    {
        var query = await _scheduleRepository.GetQueryableAsync();
        var entries = query
            .Where(e => e.AssetId == assetId && e.FinanceBookId == financeBookId && !e.IsBooked)
            .OrderBy(e => e.ScheduleDate)
            .ToList();

        var dtos = entries.Select(e => new DepreciationScheduleDto
        {
            Id = e.Id,
            ScheduleDate = e.ScheduleDate,
            DepreciationAmount = e.DepreciationAmount,
            AccumulatedDepreciation = e.AccumulatedDepreciation,
            IsBooked = e.IsBooked,
            ShiftFactorId = e.ShiftFactorId,
        }).ToList();

        return new PagedResultDto<DepreciationScheduleDto>(dtos.Count, dtos);
    }

    [Authorize(MyERPPermissions.Assets.Create)]
    public async Task<AssetShiftAllocationDto> CreateAsync(CreateAssetShiftAllocationDto input)
    {
        var scheduleIds = input.Lines.Select(l => l.ScheduleEntryId).ToList();
        var query = await _scheduleRepository.GetQueryableAsync();
        var entries = query.Where(e => scheduleIds.Contains(e.Id)).ToList();

        if (entries.Count != scheduleIds.Distinct().Count())
            throw new BusinessException(MyERPDomainErrorCodes.EntityNotFound).WithData("reason", "One or more schedule periods were not found.");
        if (entries.Any(e => e.AssetId != input.AssetId || e.FinanceBookId != input.FinanceBookId))
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition).WithData("reason", "All periods must belong to the same asset and finance book.");
        if (entries.Any(e => e.IsBooked))
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition).WithData("reason", "A booked (journaled) period's shift cannot be changed.");

        var allocationNumber = $"AST-SHIFT-{DateTime.UtcNow:yyyyMMdd}-{GuidGenerator.Create().ToString()[..6].ToUpper()}";
        var allocation = new AssetShiftAllocation(GuidGenerator.Create(), allocationNumber, input.AssetId, input.FinanceBookId, CurrentTenant.Id);

        foreach (var line in input.Lines)
        {
            var entry = entries.First(e => e.Id == line.ScheduleEntryId);
            allocation.AssignShift(GuidGenerator.Create(), line.ScheduleEntryId, line.ShiftFactorId, entry.ScheduleDate);
        }

        await _repository.InsertAsync(allocation);
        return await MapToDtoAsync(allocation);
    }

    [Authorize(MyERPPermissions.Assets.Edit)]
    public async Task<AssetShiftAllocationDto> SubmitAsync(Guid id)
    {
        var query = await _repository.WithDetailsAsync(a => a.Lines);
        var allocation = await AsyncExecuter.FirstOrDefaultAsync(query, a => a.Id == id);
        if (allocation == null) throw new BusinessException(MyERPDomainErrorCodes.EntityNotFound);

        var scheduleQuery = await _scheduleRepository.GetQueryableAsync();
        var unbookedEntries = scheduleQuery
            .Where(e => e.AssetId == allocation.AssetId && e.FinanceBookId == allocation.FinanceBookId && !e.IsBooked)
            .OrderBy(e => e.ScheduleDate)
            .ToList();

        var lastBooked = scheduleQuery
            .Where(e => e.AssetId == allocation.AssetId && e.FinanceBookId == allocation.FinanceBookId && e.IsBooked)
            .OrderByDescending(e => e.ScheduleDate)
            .FirstOrDefault();

        var factorQuery = await _shiftFactorRepository.GetQueryableAsync();
        var factorById = factorQuery.ToDictionary(f => f.Id, f => f.Factor);

        var requestedByEntryId = allocation.Lines.ToDictionary(l => l.ScheduleEntryId, l => l.ShiftFactorId);

        _reallocationService.Reallocate(
            unbookedEntries,
            factorById,
            requestedByEntryId,
            lastBooked?.AccumulatedDepreciation ?? 0);

        foreach (var entry in unbookedEntries)
        {
            await _scheduleRepository.UpdateAsync(entry);
            if (requestedByEntryId.ContainsKey(entry.Id))
                allocation.SetLineResult(entry.Id, entry.DepreciationAmount, entry.AccumulatedDepreciation);
        }

        allocation.Submit();
        await _repository.UpdateAsync(allocation);

        return await MapToDtoAsync(allocation);
    }

    [Authorize(MyERPPermissions.Assets.Edit)]
    public async Task<AssetShiftAllocationDto> CancelAsync(Guid id)
    {
        var query = await _repository.WithDetailsAsync(a => a.Lines);
        var allocation = await AsyncExecuter.FirstOrDefaultAsync(query, a => a.Id == id);
        if (allocation == null) throw new BusinessException(MyERPDomainErrorCodes.EntityNotFound);

        allocation.Cancel();
        await _repository.UpdateAsync(allocation);
        return await MapToDtoAsync(allocation);
    }

    private async Task<AssetShiftAllocationDto> MapToDtoAsync(AssetShiftAllocation a)
    {
        var factorQuery = await _shiftFactorRepository.GetQueryableAsync();
        var factorNames = factorQuery.ToDictionary(f => f.Id, f => f.ShiftName);

        return new AssetShiftAllocationDto
        {
            Id = a.Id,
            AllocationNumber = a.AllocationNumber,
            AssetId = a.AssetId,
            FinanceBookId = a.FinanceBookId,
            Status = a.Status,
            CreationTime = a.CreationTime,
            LastModificationTime = a.LastModificationTime,
            Lines = a.Lines.Select(l => new AssetShiftAllocationLineDto
            {
                Id = l.Id,
                ScheduleEntryId = l.ScheduleEntryId,
                ShiftFactorId = l.ShiftFactorId,
                ShiftFactorName = factorNames.GetValueOrDefault(l.ShiftFactorId),
                ScheduleDate = l.ScheduleDate,
                DepreciationAmount = l.DepreciationAmount,
                AccumulatedDepreciation = l.AccumulatedDepreciation,
            }).ToList(),
        };
    }
}
