using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Inventory.Entities;
using MyERP.Permissions;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Inventory;

[Authorize(MyERPPermissions.QualityInspections.Default)]
public class QualityInspectionTemplateAppService : ApplicationService
{
    private readonly IRepository<QualityInspectionTemplate, Guid> _repository;

    public QualityInspectionTemplateAppService(
        IRepository<QualityInspectionTemplate, Guid> repository)
    {
        _repository = repository;
    }

    public async Task<QiTemplateDto> GetAsync(Guid id)
    {
        var entity = await _repository.GetAsync(id);
        return MapToDto(entity);
    }

    public async Task<PagedResultDto<QiTemplateDto>> GetListAsync(PagedAndSortedResultRequestDto input)
    {
        var queryable = await _repository.GetQueryableAsync();
        var totalCount = queryable.Count();
        var items = queryable
            .OrderBy(x => x.Name)
            .Skip(input.SkipCount)
            .Take(input.MaxResultCount)
            .ToList();
        return new PagedResultDto<QiTemplateDto>(totalCount, items.Select(MapToDto).ToList());
    }

    [Authorize(MyERPPermissions.QualityInspections.Create)]
    public async Task<QiTemplateDto> CreateAsync(CreateQiTemplateDto input)
    {
        var entity = new QualityInspectionTemplate(GuidGenerator.Create(), input.Name, CurrentTenant.Id);
        entity.Description = input.Description;
        entity.ItemId = input.ItemId;
        entity.BomId = input.BomId;

        foreach (var p in input.Parameters ?? [])
        {
            entity.AddParameter(GuidGenerator.Create(), p.Specification,
                p.ExpectedValue, p.MinValue, p.MaxValue, p.IsNumeric,
                p.FormulaBased, p.Formula, p.AcceptanceCriteria);
        }

        await _repository.InsertAsync(entity);
        return MapToDto(entity);
    }

    public async Task ToggleAsync(Guid id)
    {
        var entity = await _repository.GetAsync(id);
        if (entity.IsEnabled) entity.Disable(); else entity.Enable();
        await _repository.UpdateAsync(entity);
    }

    public async Task DeleteAsync(Guid id)
    {
        await _repository.DeleteAsync(id);
    }

    private static QiTemplateDto MapToDto(QualityInspectionTemplate e) => new()
    {
        Id = e.Id,
        Name = e.Name,
        Description = e.Description,
        ItemId = e.ItemId,
        BomId = e.BomId,
        IsEnabled = e.IsEnabled,
        ParameterCount = e.Parameters.Count,
        Parameters = e.Parameters.Select(p => new QiTemplateParameterDto
        {
            Id = p.Id,
            Specification = p.Specification,
            ExpectedValue = p.ExpectedValue,
            MinValue = p.MinValue,
            MaxValue = p.MaxValue,
            IsNumeric = p.IsNumeric,
            FormulaBased = p.FormulaBased,
            Formula = p.Formula,
            AcceptanceCriteria = p.AcceptanceCriteria
        }).ToList()
    };
}

#region DTOs

public class QiTemplateDto : EntityDto<Guid>
{
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public Guid? ItemId { get; set; }
    public Guid? BomId { get; set; }
    public bool IsEnabled { get; set; }
    public int ParameterCount { get; set; }
    public List<QiTemplateParameterDto> Parameters { get; set; } = new();
}

public class QiTemplateParameterDto : EntityDto<Guid>
{
    public string Specification { get; set; } = null!;
    public string? ExpectedValue { get; set; }
    public decimal? MinValue { get; set; }
    public decimal? MaxValue { get; set; }
    public bool IsNumeric { get; set; }
    public bool FormulaBased { get; set; }
    public string? Formula { get; set; }
    public string? AcceptanceCriteria { get; set; }
}

public class CreateQiTemplateDto
{
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public Guid? ItemId { get; set; }
    public Guid? BomId { get; set; }
    public List<CreateQiTemplateParameterDto>? Parameters { get; set; }
}

public class CreateQiTemplateParameterDto
{
    public string Specification { get; set; } = null!;
    public string? ExpectedValue { get; set; }
    public decimal? MinValue { get; set; }
    public decimal? MaxValue { get; set; }
    public bool IsNumeric { get; set; }
    public bool FormulaBased { get; set; }
    public string? Formula { get; set; }
    public string? AcceptanceCriteria { get; set; }
}

#endregion
