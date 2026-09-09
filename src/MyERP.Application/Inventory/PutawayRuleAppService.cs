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

[Authorize(MyERPPermissions.Warehouses.Default)]
public class PutawayRuleAppService : ApplicationService, IPutawayRuleAppService
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

    /// <summary>
    /// Mirrors ERPNext PutawayRule.validate: duplicate rule, warehouse/company match,
    /// capacity vs existing stock level, and priority floor.
    /// </summary>
    private async Task ValidateRuleAsync(CreateUpdatePutawayRuleDto input, Guid? existingRuleId)
    {
        if (input.StockCapacity <= 0)
        {
            throw new Volo.Abp.BusinessException(MyERPDomainErrorCodes.AmountMustBePositive)
                .WithData("field", "StockCapacity");
        }

        if (input.Priority < 1)
        {
            throw new Volo.Abp.BusinessException(MyERPDomainErrorCodes.PutawayRulePriorityInvalid);
        }

        if (input.ItemId.HasValue)
        {
            var itemValidation = LazyServiceProvider.LazyGetRequiredService<DomainServices.ItemTransactionValidationService>();
            await itemValidation.ValidateItemAsync(input.ItemId.Value);
        }

        // Warehouse must belong to the rule's company — a cross-company warehouse would make the
        // rule allocate stock into another company's warehouse.
        var warehouseRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Warehouse, Guid>>();
        var warehouse = await warehouseRepo.GetAsync(input.WarehouseId);
        if (warehouse.CompanyId != input.CompanyId)
        {
            throw new Volo.Abp.BusinessException(MyERPDomainErrorCodes.PutawayRuleWarehouseCompanyMismatch)
                .WithData("warehouse", warehouse.Name);
        }

        // One rule per (item-or-item-group, warehouse) within a company. Duplicates otherwise each
        // claim the same warehouse's free space independently during allocation.
        var ruleQuery = await _repository.GetQueryableAsync();
        var duplicate = ruleQuery.Any(r =>
            r.CompanyId == input.CompanyId
            && r.WarehouseId == input.WarehouseId
            && r.ItemId == input.ItemId
            && r.ItemGroupId == input.ItemGroupId
            && (existingRuleId == null || r.Id != existingRuleId.Value));
        if (duplicate)
        {
            throw new Volo.Abp.BusinessException(MyERPDomainErrorCodes.PutawayRuleDuplicate);
        }

        // Capacity below what the warehouse already holds would make the rule permanently
        // unusable (free space negative), so reject it up front like ERPNext does.
        if (input.ItemId.HasValue)
        {
            var binRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Bin, Guid>>();
            var bin = await binRepo.FindAsync(b => b.ItemId == input.ItemId.Value && b.WarehouseId == input.WarehouseId);
            if (bin != null && input.StockCapacity < bin.ActualQty)
            {
                throw new Volo.Abp.BusinessException(MyERPDomainErrorCodes.PutawayRuleCapacityBelowStock)
                    .WithData("capacity", input.StockCapacity)
                    .WithData("balance", bin.ActualQty);
            }
        }
    }

    [Authorize(MyERPPermissions.Warehouses.Create)]
    public async Task<PutawayRuleDto> CreateAsync(CreateUpdatePutawayRuleDto input)
    {
        await ValidateRuleAsync(input, existingRuleId: null);

        var rule = new PutawayRule(GuidGenerator.Create(), input.CompanyId, input.WarehouseId, CurrentTenant.Id)
        {
            ItemId = input.ItemId,
            ItemGroupId = input.ItemGroupId,
            StockCapacity = input.StockCapacity,
            Priority = input.Priority,
            Uom = input.Uom,
        };
        await _repository.InsertAsync(rule);

        var activityLogRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Core.Entities.DocumentActivityLog, Guid>>();
        await activityLogRepo.InsertAsync(new Core.Entities.DocumentActivityLog(
            GuidGenerator.Create(), "PutawayRule", rule.Id,
            "Created", rule.CompanyId,
            rule.WarehouseId.ToString()[..8], "Draft", "Active", CurrentUser.Id,
            $"Putaway rule created for warehouse {rule.WarehouseId.ToString()[..8]} with capacity {rule.StockCapacity}", CurrentTenant.Id));

        return ObjectMapper.Map<PutawayRule, PutawayRuleDto>(rule);
    }

    [Authorize(MyERPPermissions.Warehouses.Edit)]
    public async Task<PutawayRuleDto> UpdateAsync(Guid id, CreateUpdatePutawayRuleDto input)
    {
        await ValidateRuleAsync(input, existingRuleId: id);

        var rule = await _repository.GetAsync(id);
        rule.ItemId = input.ItemId;
        rule.ItemGroupId = input.ItemGroupId;
        rule.WarehouseId = input.WarehouseId;
        rule.StockCapacity = input.StockCapacity;
        rule.Priority = input.Priority;
        rule.Uom = input.Uom;
        await _repository.UpdateAsync(rule);

        var activityLogRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Core.Entities.DocumentActivityLog, Guid>>();
        await activityLogRepo.InsertAsync(new Core.Entities.DocumentActivityLog(
            GuidGenerator.Create(), "PutawayRule", rule.Id,
            "Updated", rule.CompanyId,
            rule.WarehouseId.ToString()[..8], "Active", "Active", CurrentUser.Id,
            $"Putaway rule updated for warehouse {rule.WarehouseId.ToString()[..8]}", CurrentTenant.Id));

        return ObjectMapper.Map<PutawayRule, PutawayRuleDto>(rule);
    }

    [Authorize(MyERPPermissions.Warehouses.Edit)]
    public async Task ToggleAsync(Guid id)
    {
        var rule = await _repository.GetAsync(id);
        rule.IsEnabled = !rule.IsEnabled;
        await _repository.UpdateAsync(rule);

        var activityLogRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Core.Entities.DocumentActivityLog, Guid>>();
        await activityLogRepo.InsertAsync(new Core.Entities.DocumentActivityLog(
            GuidGenerator.Create(), "PutawayRule", rule.Id,
            rule.IsEnabled ? "Enabled" : "Disabled", rule.CompanyId,
            rule.WarehouseId.ToString()[..8], "Active", rule.IsEnabled ? "Active" : "Disabled", CurrentUser.Id,
            $"Putaway rule for warehouse {rule.WarehouseId.ToString()[..8]} {(rule.IsEnabled ? "enabled" : "disabled")}", CurrentTenant.Id));
    }

    [Authorize(MyERPPermissions.Warehouses.Delete)]
    public async Task DeleteAsync(Guid id) => await _repository.DeleteAsync(id);
}
