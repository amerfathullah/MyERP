using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Core.Entities;
using MyERP.Permissions;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Core;

/// <summary>
/// Manages Territory, CustomerGroup, and SupplierGroup hierarchies.
/// These are tree-structured master data used for sales territory assignment,
/// customer categorization, and supplier grouping.
/// </summary>
[Authorize(MyERPPermissions.Customers.Default)]
public class HierarchyMasterDataAppService : ApplicationService
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
        return new HierarchyNodeDto { Id = group.Id, Name = group.Name, ParentId = group.ParentId, IsGroup = group.IsGroup };
    }

    [Authorize(MyERPPermissions.Suppliers.Delete)]
    public async Task DeleteSupplierGroupAsync(Guid id) => await _supplierGroupRepository.DeleteAsync(id);
}

#region DTOs

public class HierarchyNodeDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public Guid? ParentId { get; set; }
    public bool IsGroup { get; set; }
}

public class CreateHierarchyNodeDto
{
    public string Name { get; set; } = null!;
    public Guid? ParentId { get; set; }
    public bool IsGroup { get; set; }
    public Guid? ManagerId { get; set; }
}

#endregion
