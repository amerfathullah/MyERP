using System;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Accounting.Entities;
using MyERP.Permissions;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Accounting;

[Authorize(MyERPPermissions.Accounts.Default)]
public class CostCenterAppService : ApplicationService
{
    private readonly IRepository<CostCenter, Guid> _repository;

    public CostCenterAppService(IRepository<CostCenter, Guid> repository)
    {
        _repository = repository;
    }

    public async Task<PagedResultDto<CostCenterDto>> GetListAsync(GetCostCenterListDto input)
    {
        var query = await _repository.GetQueryableAsync();
        if (input.CompanyId.HasValue)
            query = query.Where(c => c.CompanyId == input.CompanyId.Value);
        if (!string.IsNullOrWhiteSpace(input.Filter))
        {
            var f = input.Filter;
            query = query.Where(c => c.Name.ToLower().Contains(f));
        }

        var totalCount = query.Count();
        var items = query.Where(c => c.IsActive)
            .OrderBy(c => c.Name)
            .Skip(input.SkipCount).Take(input.MaxResultCount).ToList();

        return new PagedResultDto<CostCenterDto>(totalCount, items.Select(ObjectMapper.Map<CostCenter, CostCenterDto>).ToList());
    }

    [Authorize(MyERPPermissions.Accounts.Create)]
    public async Task<CostCenterDto> CreateAsync(CreateCostCenterDto input)
    {
        var cc = new CostCenter(GuidGenerator.Create(), input.CompanyId, input.Name,
            input.IsGroup, input.ParentId, CurrentTenant.Id)
        { CostCenterNumber = input.CostCenterNumber };
        await _repository.InsertAsync(cc);
        return ObjectMapper.Map<CostCenter, CostCenterDto>(cc);
    }

    [Authorize(MyERPPermissions.Accounts.Edit)]
    public async Task<CostCenterDto> UpdateAsync(Guid id, CreateCostCenterDto input)
    {
        var cc = await _repository.GetAsync(id);
        cc.Name = input.Name;
        cc.CostCenterNumber = input.CostCenterNumber;
        cc.IsGroup = input.IsGroup;
        cc.ParentId = input.ParentId;
        await _repository.UpdateAsync(cc);
        return ObjectMapper.Map<CostCenter, CostCenterDto>(cc);
    }


}

public class CostCenterDto : AuditedEntityDto<Guid>
{
    public string Name { get; set; } = null!;
    public string? CostCenterNumber { get; set; }
    public Guid CompanyId { get; set; }
    public bool IsGroup { get; set; }
    public Guid? ParentId { get; set; }
    public bool IsActive { get; set; }
}

public class CreateCostCenterDto
{
    [Required] public Guid CompanyId { get; set; }
    [Required][StringLength(200)] public string Name { get; set; } = null!;
    [StringLength(50)] public string? CostCenterNumber { get; set; }
    public bool IsGroup { get; set; }
    public Guid? ParentId { get; set; }
}

public class GetCostCenterListDto : PagedAndSortedResultRequestDto
{
    public Guid? CompanyId { get; set; }
    public string? Filter { get; set; }
}

