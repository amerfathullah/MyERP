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

        var binRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Bin, Guid>>();
        var itemIds = items.Where(r => r.ItemId.HasValue).Select(r => r.ItemId!.Value).Distinct().ToList();
        var whIds = items.Select(r => r.WarehouseId).Distinct().ToList();
        var binQuery = await binRepo.GetQueryableAsync();
        var bins = binQuery
            .Where(b => itemIds.Contains(b.ItemId) && whIds.Contains(b.WarehouseId))
            .ToList();
        var binMap = bins.ToDictionary(b => (b.ItemId, b.WarehouseId), b => b.ActualQty);

        var dtos = items.Select(r =>
        {
            var dto = ObjectMapper.Map<PutawayRule, PutawayRuleDto>(r);
            decimal balance = 0m;
            if (r.ItemId.HasValue && binMap.TryGetValue((r.ItemId.Value, r.WarehouseId), out var bal))
            {
                balance = bal;
            }
            dto.AvailableCapacity = r.GetAvailableCapacity(balance);
            return dto;
        }).ToList();

        return new PagedResultDto<PutawayRuleDto>(totalCount, dtos);
    }

    public async Task<PutawayRuleDto> GetAsync(Guid id)
    {
        var rule = await _repository.GetAsync(id);
        var dto = ObjectMapper.Map<PutawayRule, PutawayRuleDto>(rule);
        dto.AvailableCapacity = await GetAvailableCapacityAsync(id);
        return dto;
    }

    /// <summary>
    /// Gets available capacity for a Putaway Rule.
    /// Per ERPNext PR #60260 (commit acda094a7f): checks read permission on the Putaway Rule
    /// and derives available capacity from rule.StockCapacity - bin.ActualQty.
    /// </summary>
    public async Task<decimal> GetAvailableCapacityAsync(Guid id)
    {
        var rule = await _repository.GetAsync(id);
        decimal balanceQty = 0m;
        if (rule.ItemId.HasValue)
        {
            var binRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Bin, Guid>>();
            var bin = await binRepo.FindAsync(b => b.ItemId == rule.ItemId.Value && b.WarehouseId == rule.WarehouseId);
            balanceQty = bin?.ActualQty ?? 0m;
        }
        else if (rule.ItemGroupId.HasValue)
        {
            var itemRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Item, Guid>>();
            var binRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Bin, Guid>>();
            var itemQuery = await itemRepo.GetQueryableAsync();
            var groupItemIds = itemQuery.Where(i => i.ItemGroupId == rule.ItemGroupId.Value).Select(i => i.Id).ToList();
            var binQuery = await binRepo.GetQueryableAsync();
            balanceQty = binQuery
                .Where(b => b.WarehouseId == rule.WarehouseId && groupItemIds.Contains(b.ItemId))
                .Sum(b => b.ActualQty);
        }

        return rule.GetAvailableCapacity(balanceQty);
    }

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
