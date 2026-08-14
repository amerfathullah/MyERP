using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace MyERP.Core;

public interface IHierarchyMasterDataAppService : IApplicationService
{
    Task<List<HierarchyNodeDto>> GetTerritoriesAsync();
    Task<HierarchyNodeDto> CreateTerritoryAsync(CreateHierarchyNodeDto input);
    Task DeleteTerritoryAsync(Guid id);
    Task<List<HierarchyNodeDto>> GetCustomerGroupsAsync();
    Task<HierarchyNodeDto> CreateCustomerGroupAsync(CreateHierarchyNodeDto input);
    Task DeleteCustomerGroupAsync(Guid id);
    Task<List<HierarchyNodeDto>> GetSupplierGroupsAsync();
    Task<HierarchyNodeDto> CreateSupplierGroupAsync(CreateHierarchyNodeDto input);
    Task DeleteSupplierGroupAsync(Guid id);
}
