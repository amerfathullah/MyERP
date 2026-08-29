using System;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Inventory.Entities;
using MyERP.Permissions;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

using MyERP;

namespace MyERP.Inventory;

public class WarehouseAppService :
    CrudAppService<
        Warehouse,
        WarehouseDto,
        Guid,
        GetWarehouseListDto,
        CreateUpdateWarehouseDto>,
    IWarehouseAppService
{
    public WarehouseAppService(IRepository<Warehouse, Guid> repository)
        : base(repository)
    {
        GetPolicyName = MyERPPermissions.Warehouses.Default;
        GetListPolicyName = MyERPPermissions.Warehouses.Default;
        CreatePolicyName = MyERPPermissions.Warehouses.Create;
        UpdatePolicyName = MyERPPermissions.Warehouses.Edit;
        DeletePolicyName = MyERPPermissions.Warehouses.Delete;
    }

    public override async Task<PagedResultDto<WarehouseDto>> GetListAsync(GetWarehouseListDto input)
    {
        var filter = input.Filter;

        if (string.IsNullOrWhiteSpace(filter))
        {
            return await base.GetListAsync(input);
        }

        var queryable = await Repository.GetQueryableAsync();

        queryable = queryable.Where(w =>
            w.IsActive
            && (w.Name.Contains(filter)
                || (w.WarehouseCode != null && w.WarehouseCode.Contains(filter))));

        var totalCount = queryable.Count();
        var items = queryable
            .OrderBy(w => w.Name)
            .Skip(input.SkipCount)
            .Take(input.MaxResultCount)
            .ToList();

        return new PagedResultDto<WarehouseDto>(
            totalCount,
            items.Select(ObjectMapper.Map<Warehouse, WarehouseDto>).ToList());
    }

    public override async Task<WarehouseDto> CreateAsync(CreateUpdateWarehouseDto input)
    {
        var result = await base.CreateAsync(input);
        var activityLogRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Core.Entities.DocumentActivityLog, Guid>>();
        await activityLogRepo.InsertAsync(new Core.Entities.DocumentActivityLog(
            GuidGenerator.Create(), "Warehouse", result.Id,
            "Created", result.CompanyId,
            result.Name, "Draft", "Active", CurrentUser.Id,
            $"Warehouse '{result.Name}' created", CurrentTenant.Id));
        return result;
    }

    public override async Task<WarehouseDto> UpdateAsync(Guid id, CreateUpdateWarehouseDto input)
    {
        if (input.ParentWarehouseId == id)
        {
            throw new BusinessException(MyERPDomainErrorCodes.InvalidParentWarehouse)
                .WithData("warehouseId", id);
        }

        var result = await base.UpdateAsync(id, input);
        var activityLogRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Core.Entities.DocumentActivityLog, Guid>>();
        await activityLogRepo.InsertAsync(new Core.Entities.DocumentActivityLog(
            GuidGenerator.Create(), "Warehouse", result.Id,
            "Updated", result.CompanyId,
            result.Name, "Active", "Active", CurrentUser.Id,
            $"Warehouse '{result.Name}' updated", CurrentTenant.Id));
        return result;
    }

    protected override Warehouse MapToEntity(CreateUpdateWarehouseDto input)
    {
        var entity = new Warehouse(
            GuidGenerator.Create(),
            input.CompanyId,
            input.Name,
            CurrentTenant.Id);
        MapUpdateFields(input, entity);
        return entity;
    }

    protected override void MapToEntity(CreateUpdateWarehouseDto input, Warehouse entity)
    {
        entity.SetName(input.Name);
        entity.CompanyId = input.CompanyId;
        MapUpdateFields(input, entity);
    }

    private static void MapUpdateFields(CreateUpdateWarehouseDto input, Warehouse entity)
    {
        entity.BranchId = input.BranchId;
        entity.WarehouseCode = input.WarehouseCode;
        entity.Address = input.Address;
        entity.City = input.City;
        entity.State = input.State;
        entity.PostalCode = input.PostalCode;
        entity.Country = input.Country;
        entity.ParentWarehouseId = input.ParentWarehouseId;
        entity.IsGroup = input.IsGroup;
        entity.IsActive = input.IsActive;
        entity.WarehouseType = input.WarehouseType;
    }

    /// <summary>
    /// Prevent deletion of warehouses with stock (Bin entries exist).
    /// </summary>
    public override async Task DeleteAsync(Guid id)
    {
        var binRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Bin, Guid>>();
        var binQuery = await binRepo.GetQueryableAsync();
        var hasStock = binQuery.Any(b => b.WarehouseId == id && b.ActualQty != 0);

        if (hasStock)
        {
            throw new BusinessException(MyERPDomainErrorCodes.WarehouseCannotBeDeleted)
                .WithData("reason", "Warehouse has non-zero stock. Transfer or reconcile stock first.");
        }

        var sleRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<StockLedgerEntry, Guid>>();
        var sleQuery = await sleRepo.GetQueryableAsync();
        var hasHistory = sleQuery.Any(s => s.WarehouseId == id);

        if (hasHistory)
        {
            throw new BusinessException(MyERPDomainErrorCodes.WarehouseCannotBeDeleted)
                .WithData("reason", "Warehouse has stock ledger history. Deactivate instead of deleting.");
        }

        await base.DeleteAsync(id);
    }

    protected override async Task<IQueryable<Warehouse>> CreateFilteredQueryAsync(GetWarehouseListDto input)
    {
        var query = await base.CreateFilteredQueryAsync(input);
        // Only filters by active — group warehouses are deliberately included here (tree/hierarchy
        // views need them); callers that need leaf-only warehouses (e.g. stock transfer dropdowns)
        // filter IsGroup out client-side. Group warehouses are still blocked from receiving stock at
        // posting time (StockPostingService/StockEntryManager), so this list including them is safe.
        return query.Where(w => w.IsActive);
    }
}
