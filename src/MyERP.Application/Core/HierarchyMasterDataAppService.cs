using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using MyERP.Core.Entities;
using MyERP.Permissions;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Core;

/// <summary>
/// Manages Territory, CustomerGroup, and SupplierGroup hierarchies.
/// These are tree-structured master data used for sales territory assignment,
/// customer categorization, and supplier grouping.
/// </summary>
[Authorize(MyERPPermissions.Customers.Default)]
public class HierarchyMasterDataAppService : ApplicationService, IHierarchyMasterDataAppService
{
    private readonly IRepository<Territory, Guid> _territoryRepository;
    private readonly IRepository<CustomerGroup, Guid> _customerGroupRepository;
    private readonly IRepository<SupplierGroup, Guid> _supplierGroupRepository;

    public HierarchyMasterDataAppService(
        IRepository<Territory, Guid> territoryRepository,
        IRepository<CustomerGroup, Guid> customerGroupRepository,
        IRepository<SupplierGroup, Guid> supplierGroupRepository)
    {
        _territoryRepository = territoryRepository;
        _customerGroupRepository = customerGroupRepository;
        _supplierGroupRepository = supplierGroupRepository;
    }

    // === Territory ===

    public async Task<List<HierarchyNodeDto>> GetTerritoriesAsync()
    {
        var query = await _territoryRepository.GetQueryableAsync();
        return query.OrderBy(t => t.Name).ToList().Select(t => new HierarchyNodeDto
        {
            Id = t.Id, Name = t.Name, ParentId = t.ParentId, IsGroup = t.IsGroup
        }).ToList();
    }

    [Authorize(MyERPPermissions.Customers.Create)]
    public async Task<HierarchyNodeDto> CreateTerritoryAsync(CreateHierarchyNodeDto input)
    {
        var territory = new Territory(GuidGenerator.Create(), input.Name, input.ParentId, input.IsGroup, CurrentTenant.Id);
        territory.TerritoryManagerId = input.ManagerId;
        await _territoryRepository.InsertAsync(territory);

        var activityLogRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Entities.DocumentActivityLog, Guid>>();
        await activityLogRepo.InsertAsync(new Entities.DocumentActivityLog(
            GuidGenerator.Create(), "Territory", territory.Id,
            "Created", Guid.Empty,
            territory.Name, "Draft", "Active", CurrentUser.Id,
            $"Territory '{territory.Name}' created", CurrentTenant.Id));

        return new HierarchyNodeDto { Id = territory.Id, Name = territory.Name, ParentId = territory.ParentId, IsGroup = territory.IsGroup };
    }

    [Authorize(MyERPPermissions.Customers.Delete)]
    public async Task DeleteTerritoryAsync(Guid id) => await _territoryRepository.DeleteAsync(id);

    // === Customer Group ===

    public async Task<List<HierarchyNodeDto>> GetCustomerGroupsAsync()
    {
        var query = await _customerGroupRepository.GetQueryableAsync();
        return query.OrderBy(g => g.Name).ToList().Select(g => new HierarchyNodeDto
        {
            Id = g.Id, Name = g.Name, ParentId = g.ParentId, IsGroup = g.IsGroup
        }).ToList();
    }

    [Authorize(MyERPPermissions.Customers.Create)]
    public async Task<HierarchyNodeDto> CreateCustomerGroupAsync(CreateHierarchyNodeDto input)
    {
        var group = new CustomerGroup(GuidGenerator.Create(), input.Name, input.ParentId, input.IsGroup, CurrentTenant.Id);
        await _customerGroupRepository.InsertAsync(group);

        var activityLogRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Entities.DocumentActivityLog, Guid>>();
        await activityLogRepo.InsertAsync(new Entities.DocumentActivityLog(
            GuidGenerator.Create(), "CustomerGroup", group.Id,
            "Created", Guid.Empty,
            group.Name, "Draft", "Active", CurrentUser.Id,
            $"Customer group '{group.Name}' created", CurrentTenant.Id));

        return new HierarchyNodeDto { Id = group.Id, Name = group.Name, ParentId = group.ParentId, IsGroup = group.IsGroup };
    }

    [Authorize(MyERPPermissions.Customers.Delete)]
    public async Task DeleteCustomerGroupAsync(Guid id) => await _customerGroupRepository.DeleteAsync(id);

    // === Supplier Group ===

    public async Task<List<HierarchyNodeDto>> GetSupplierGroupsAsync()
    {
        var query = await _supplierGroupRepository.GetQueryableAsync();
        return query.OrderBy(g => g.Name).ToList().Select(g => new HierarchyNodeDto
        {
            Id = g.Id, Name = g.Name, ParentId = g.ParentId, IsGroup = g.IsGroup
        }).ToList();
    }

    [Authorize(MyERPPermissions.Suppliers.Create)]
    public async Task<HierarchyNodeDto> CreateSupplierGroupAsync(CreateHierarchyNodeDto input)
    {
        var group = new SupplierGroup(GuidGenerator.Create(), input.Name, input.ParentId, input.IsGroup, CurrentTenant.Id);
        await _supplierGroupRepository.InsertAsync(group);

        var activityLogRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Entities.DocumentActivityLog, Guid>>();
        await activityLogRepo.InsertAsync(new Entities.DocumentActivityLog(
            GuidGenerator.Create(), "SupplierGroup", group.Id,
            "Created", Guid.Empty,
            group.Name, "Draft", "Active", CurrentUser.Id,
            $"Supplier group '{group.Name}' created", CurrentTenant.Id));

        return new HierarchyNodeDto { Id = group.Id, Name = group.Name, ParentId = group.ParentId, IsGroup = group.IsGroup };
    }

    [Authorize(MyERPPermissions.Suppliers.Delete)]
    public async Task DeleteSupplierGroupAsync(Guid id) => await _supplierGroupRepository.DeleteAsync(id);
}
