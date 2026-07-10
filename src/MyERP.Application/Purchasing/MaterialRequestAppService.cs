using System;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Core.DomainServices;
using MyERP.Permissions;
using MyERP.Purchasing.DTOs;
using MyERP.Purchasing.Entities;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Purchasing;

[Authorize(MyERPPermissions.MaterialRequests.Default)]
public class MaterialRequestAppService : ApplicationService, IMaterialRequestAppService
{
    private readonly IRepository<MaterialRequest, Guid> _repository;
    private readonly IDocumentNumberGenerator _numberGenerator;

    public MaterialRequestAppService(
        IRepository<MaterialRequest, Guid> repository,
        IDocumentNumberGenerator numberGenerator)
    {
        _repository = repository;
        _numberGenerator = numberGenerator;
    }

    public async Task<MaterialRequestDto> GetAsync(Guid id)
    {
        var entity = await _repository.GetAsync(id, includeDetails: true);
        return MapToDto(entity);
    }

    public async Task<PagedResultDto<MaterialRequestDto>> GetListAsync(GetMaterialRequestListDto input)
    {
        var query = await _repository.GetQueryableAsync();

        if (input.RequestType.HasValue)
            query = query.Where(x => x.RequestType == input.RequestType.Value);
        if (input.CompanyId.HasValue)
            query = query.Where(x => x.CompanyId == input.CompanyId.Value);
        if (!string.IsNullOrWhiteSpace(input.Filter))
        {
            var f = input.Filter.ToLower();
            query = query.Where(x => x.RequestNumber.ToLower().Contains(f));
        }

        var totalCount = query.Count();
        var items = query.OrderByDescending(x => x.CreationTime)
            .Skip(input.SkipCount).Take(input.MaxResultCount).ToList();

        return new PagedResultDto<MaterialRequestDto>(totalCount, items.Select(MapToDto).ToList());
    }

    [Authorize(MyERPPermissions.MaterialRequests.Create)]
    public async Task<MaterialRequestDto> CreateAsync(CreateMaterialRequestDto input)
    {
        var number = await _numberGenerator.GenerateAsync("MR", input.CompanyId);
        var entity = new MaterialRequest(
            GuidGenerator.Create(), input.CompanyId, number,
            input.RequestType, input.RequestDate, CurrentTenant.Id)
        {
            RequiredByDate = input.RequiredByDate,
            WorkOrderId = input.WorkOrderId,
            SourceWarehouseId = input.SourceWarehouseId,
            TargetWarehouseId = input.TargetWarehouseId,
            Notes = input.Notes,
        };

        foreach (var item in input.Items)
        {
            entity.AddItem(item.ItemId, item.ItemName, item.Quantity, item.Uom, item.WarehouseId);
        }

        await _repository.InsertAsync(entity);
        return MapToDto(entity);
    }

    [Authorize(MyERPPermissions.MaterialRequests.Delete)]
    public async Task DeleteAsync(Guid id)
    {
        await _repository.DeleteAsync(id);
    }

    [Authorize(MyERPPermissions.MaterialRequests.Submit)]
    public async Task<MaterialRequestDto> SubmitAsync(Guid id)
    {
        var entity = await _repository.GetAsync(id, includeDetails: true);
        entity.Submit();
        await _repository.UpdateAsync(entity);
        return MapToDto(entity);
    }

    [Authorize(MyERPPermissions.MaterialRequests.Cancel)]
    public async Task<MaterialRequestDto> CancelAsync(Guid id)
    {
        var entity = await _repository.GetAsync(id, includeDetails: true);
        entity.Cancel();
        await _repository.UpdateAsync(entity);
        return MapToDto(entity);
    }

    private static MaterialRequestDto MapToDto(MaterialRequest e) => new()
    {
        Id = e.Id,
        RequestNumber = e.RequestNumber,
        RequestType = e.RequestType,
        Status = e.Status,
        RequestDate = e.RequestDate,
        RequiredByDate = e.RequiredByDate,
        CompanyId = e.CompanyId,
        WorkOrderId = e.WorkOrderId,
        SourceWarehouseId = e.SourceWarehouseId,
        TargetWarehouseId = e.TargetWarehouseId,
        Notes = e.Notes,
        CreationTime = e.CreationTime,
        Items = e.Items.Select(i => new MaterialRequestItemDto
        {
            Id = i.Id,
            ItemId = i.ItemId,
            ItemName = i.ItemName,
            Quantity = i.Quantity,
            OrderedQuantity = i.OrderedQuantity,
            ReceivedQuantity = i.ReceivedQuantity,
            Uom = i.Uom,
            WarehouseId = i.WarehouseId,
        }).ToList(),
    };
}
