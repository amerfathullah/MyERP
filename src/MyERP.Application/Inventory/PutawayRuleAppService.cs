using System;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Inventory.Entities;
using MyERP.Permissions;
using Microsoft.AspNetCore.Authorization;
using MyERP.Shared;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Inventory;

public class PutawayRuleDto : EntityDto<Guid>
{
    public Guid CompanyId { get; set; }
    public Guid? ItemId { get; set; }
    public Guid? ItemGroupId { get; set; }
    public Guid WarehouseId { get; set; }
    public decimal StockCapacity { get; set; }
    public int Priority { get; set; }
    public string? Uom { get; set; }
    public bool IsEnabled { get; set; }
}

public class CreateUpdatePutawayRuleDto
{
    public Guid CompanyId { get; set; }
    public Guid? ItemId { get; set; }
    public Guid? ItemGroupId { get; set; }
    public Guid WarehouseId { get; set; }
    public decimal StockCapacity { get; set; }
    public int Priority { get; set; } = 1;
    public string? Uom { get; set; }
}

[Authorize(MyERPPermissions.Warehouses.Default)]
public class PutawayRuleAppService : ApplicationService
{
    private readonly IRepository<PutawayRule, Guid> _repository;

    public PutawayRuleAppService(IRepository<PutawayRule, Guid> repository)
        => _repository = repository;

    public async Task<PagedResultDto<PutawayRuleDto>> GetListAsync(CompanyFilteredPagedRequestDto input)
    {
        var query = await _repository.GetQueryableAsync();
        if (input.CompanyId.HasValue)
            query = query.Where(r => r.CompanyId == input.CompanyId.Value);

        if (!string.IsNullOrWhiteSpace(input.Filter))
        {
            var filter = input.Filter;
            query = query.Where(r => r.Uom != null && r.Uom.Contains(filter));
        }

        var totalCount = query.Count();
        var items = query.OrderBy(r => r.Priority).ThenBy(r => r.WarehouseId)
            .Skip(input.SkipCount).Take(input.MaxResultCount).ToList();
        return new PagedResultDto<PutawayRuleDto>(totalCount,
            items.Select(ObjectMapper.Map<PutawayRule, PutawayRuleDto>).ToList());
    }

    public async Task<PutawayRuleDto> GetAsync(Guid id)
        => ObjectMapper.Map<PutawayRule, PutawayRuleDto>(await _repository.GetAsync(id));

    [Authorize(MyERPPermissions.Warehouses.Create)]
    public async Task<PutawayRuleDto> CreateAsync(CreateUpdatePutawayRuleDto input)
    {
        var rule = new PutawayRule(GuidGenerator.Create(), input.CompanyId, input.WarehouseId, CurrentTenant.Id)
        {
            ItemId = input.ItemId,
            ItemGroupId = input.ItemGroupId,
            StockCapacity = input.StockCapacity,
            Priority = input.Priority,
            Uom = input.Uom,
        };
        await _repository.InsertAsync(rule);
        return ObjectMapper.Map<PutawayRule, PutawayRuleDto>(rule);
    }

    [Authorize(MyERPPermissions.Warehouses.Edit)]
    public async Task<PutawayRuleDto> UpdateAsync(Guid id, CreateUpdatePutawayRuleDto input)
    {
        var rule = await _repository.GetAsync(id);
        rule.ItemId = input.ItemId;
        rule.ItemGroupId = input.ItemGroupId;
        rule.WarehouseId = input.WarehouseId;
        rule.StockCapacity = input.StockCapacity;
        rule.Priority = input.Priority;
        rule.Uom = input.Uom;
        await _repository.UpdateAsync(rule);
        return ObjectMapper.Map<PutawayRule, PutawayRuleDto>(rule);
    }

    [Authorize(MyERPPermissions.Warehouses.Edit)]
    public async Task ToggleAsync(Guid id)
    {
        var rule = await _repository.GetAsync(id);
        rule.IsEnabled = !rule.IsEnabled;
        await _repository.UpdateAsync(rule);
    }

    [Authorize(MyERPPermissions.Warehouses.Delete)]
    public async Task DeleteAsync(Guid id) => await _repository.DeleteAsync(id);
}

