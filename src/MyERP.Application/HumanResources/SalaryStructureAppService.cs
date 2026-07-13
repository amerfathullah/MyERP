using System;
using System.Linq;
using System.Threading.Tasks;
using MyERP.HumanResources.Entities;
using MyERP.Permissions;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.HumanResources;

public class SalaryStructureDto : EntityDto<Guid>
{
    public Guid CompanyId { get; set; }
    public string Name { get; set; } = null!;
    public bool IsHourlyBased { get; set; }
    public string PayrollFrequency { get; set; } = null!;
    public bool IsActive { get; set; }
    public string? Description { get; set; }
    public SalaryStructureDetailDto[] Details { get; set; } = [];
}

public class SalaryStructureDetailDto
{
    public Guid Id { get; set; }
    public Guid SalaryComponentId { get; set; }
    public string ComponentName { get; set; } = null!;
    public decimal Amount { get; set; }
    public string? Formula { get; set; }
}

public class CreateSalaryStructureDto
{
    public Guid CompanyId { get; set; }
    public string Name { get; set; } = null!;
    public bool IsHourlyBased { get; set; }
    public string PayrollFrequency { get; set; } = "Monthly";
    public string? Description { get; set; }
    public CreateSalaryStructureDetailDto[] Details { get; set; } = [];
}

public class CreateSalaryStructureDetailDto
{
    public Guid SalaryComponentId { get; set; }
    public string ComponentName { get; set; } = null!;
    public decimal Amount { get; set; }
    public string? Formula { get; set; }
}

[Authorize(MyERPPermissions.Payroll.Default)]
public class SalaryStructureAppService : ApplicationService
{
    private readonly IRepository<SalaryStructure, Guid> _repository;

    public SalaryStructureAppService(IRepository<SalaryStructure, Guid> repository) => _repository = repository;

    public async Task<PagedResultDto<SalaryStructureDto>> GetListAsync(PagedAndSortedResultRequestDto input)
    {
        var query = (await _repository.WithDetailsAsync()).AsQueryable();
        var totalCount = query.Count();
        var items = query.OrderBy(s => s.Name)
            .Skip(input.SkipCount).Take(input.MaxResultCount).ToList();
        return new PagedResultDto<SalaryStructureDto>(totalCount, items.Select(MapToDto).ToList());
    }

    public async Task<SalaryStructureDto> GetAsync(Guid id)
    {
        var ss = (await _repository.WithDetailsAsync()).First(s => s.Id == id);
        return MapToDto(ss);
    }

    [Authorize(MyERPPermissions.Payroll.Create)]
    public async Task<SalaryStructureDto> CreateAsync(CreateSalaryStructureDto input)
    {
        var ss = new SalaryStructure(GuidGenerator.Create(), input.CompanyId, input.Name, CurrentTenant.Id)
        {
            IsHourlyBased = input.IsHourlyBased,
            PayrollFrequency = input.PayrollFrequency,
            Description = input.Description,
        };
        foreach (var d in input.Details)
            ss.AddDetail(new SalaryStructureDetail(Guid.NewGuid(), ss.Id,
                d.SalaryComponentId, d.ComponentName, d.Amount,
                HumanResources.Entities.SalaryComponentType.Earning)
            {
                Formula = d.Formula,
            });
        await _repository.InsertAsync(ss);
        return MapToDto(ss);
    }

    [Authorize(MyERPPermissions.Payroll.Default)]
    public async Task<SalaryStructureDto> UpdateAsync(Guid id, CreateSalaryStructureDto input)
    {
        var ss = await _repository.GetAsync(id);
        ss.Name = input.Name;
        ss.IsHourlyBased = input.IsHourlyBased;
        ss.PayrollFrequency = input.PayrollFrequency;
        ss.Description = input.Description;
        ss.IsActive = true;
        await _repository.UpdateAsync(ss);
        return MapToDto(ss);
    }

    private static SalaryStructureDto MapToDto(SalaryStructure s) => new()
    {
        Id = s.Id, CompanyId = s.CompanyId, Name = s.Name,
        IsHourlyBased = s.IsHourlyBased, PayrollFrequency = s.PayrollFrequency,
        IsActive = s.IsActive, Description = s.Description,
        Details = s.Details.Select(d => new SalaryStructureDetailDto
        {
            Id = d.Id, SalaryComponentId = d.SalaryComponentId,
            ComponentName = d.ComponentName, Amount = d.Amount, Formula = d.Formula,
        }).ToArray(),
    };
}
