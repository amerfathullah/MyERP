using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using MyERP.Inventory.Entities;
using MyERP.Permissions;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Inventory;

[Authorize(MyERPPermissions.Items.Default)]
public class ItemGroupAppService : ApplicationService, IItemGroupAppService
{
    private readonly IRepository<ItemGroup, Guid> _repository;
    public ItemGroupAppService(IRepository<ItemGroup, Guid> repository) => _repository = repository;

    public async Task<PagedResultDto<ItemGroupDto>> GetListAsync(PagedAndSortedResultRequestDto input)
    {
        var query = await _repository.GetQueryableAsync();
        var totalCount = query.Count();
        var items = query.OrderBy(g => g.Name)
            .Skip(input.SkipCount).Take(input.MaxResultCount).ToList();
        return new PagedResultDto<ItemGroupDto>(totalCount, items.Select(ObjectMapper.Map<ItemGroup, ItemGroupDto>).ToList());
    }

    [Authorize(MyERPPermissions.Items.Default)]
    public async Task<ItemGroupDto> GetAsync(Guid id)
    {
        var ig = await _repository.GetAsync(id);
        return ObjectMapper.Map<ItemGroup, ItemGroupDto>(ig);
    }

    [Authorize(MyERPPermissions.Items.Create)]
    public async Task<ItemGroupDto> CreateAsync(CreateItemGroupDto input)
    {
        await ValidateDefaultsAsync(input.DefaultWarehouseId, input.DefaultInventoryAccountId);

        var ig = new ItemGroup(GuidGenerator.Create(), input.Name, input.IsGroup, CurrentTenant.Id)
        {
            ParentId = input.ParentId,
            DefaultWarehouseId = input.DefaultWarehouseId,
            DefaultInventoryAccountId = input.DefaultInventoryAccountId,
            DefaultSupplierId = input.DefaultSupplierId,
        };
        await _repository.InsertAsync(ig);

        var activityLogRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Core.Entities.DocumentActivityLog, Guid>>();
        await activityLogRepo.InsertAsync(new Core.Entities.DocumentActivityLog(
            GuidGenerator.Create(), "ItemGroup", ig.Id,
            "Created", Guid.Empty,
            ig.Name, "Draft", "Active", CurrentUser.Id,
            $"Item group '{ig.Name}' created", CurrentTenant.Id));

        return ObjectMapper.Map<ItemGroup, ItemGroupDto>(ig);
    }

    [Authorize(MyERPPermissions.Items.Edit)]
    public async Task<ItemGroupDto> UpdateAsync(Guid id, CreateItemGroupDto input)
    {
        await ValidateDefaultsAsync(input.DefaultWarehouseId, input.DefaultInventoryAccountId);

        var ig = await _repository.GetAsync(id);
        ig.Name = input.Name;
        ig.IsGroup = input.IsGroup;
        ig.ParentId = input.ParentId;
        ig.DefaultWarehouseId = input.DefaultWarehouseId;
        ig.DefaultInventoryAccountId = input.DefaultInventoryAccountId;
        ig.DefaultSupplierId = input.DefaultSupplierId;

        await _repository.UpdateAsync(ig);

        var activityLogRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Core.Entities.DocumentActivityLog, Guid>>();
        await activityLogRepo.InsertAsync(new Core.Entities.DocumentActivityLog(
            GuidGenerator.Create(), "ItemGroup", ig.Id,
            "Updated", Guid.Empty,
            ig.Name, "Active", "Active", CurrentUser.Id,
            $"Item group '{ig.Name}' updated", CurrentTenant.Id));

        return ObjectMapper.Map<ItemGroup, ItemGroupDto>(ig);
    }

    [Authorize(MyERPPermissions.Items.Delete)]
    public async Task DeleteAsync(Guid id)
    {
        await _repository.DeleteAsync(id);
    }

    private async Task ValidateDefaultsAsync(Guid? defaultWarehouseId, Guid? defaultInventoryAccountId)
    {
        if (defaultWarehouseId.HasValue)
        {
            var whRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Warehouse, Guid>>();
            var wh = await whRepo.FindAsync(defaultWarehouseId.Value);
            if (wh != null && wh.IsGroup)
            {
                throw new Volo.Abp.BusinessException(MyERPDomainErrorCodes.GroupWarehouseCannotReceiveStock)
                    .WithData("detail", $"Warehouse '{wh.Name}' is a group warehouse. Default warehouse must be a leaf warehouse.");
            }
        }

        if (defaultInventoryAccountId.HasValue)
        {
            var accRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Accounting.Entities.Account, Guid>>();
            var acc = await accRepo.FindAsync(defaultInventoryAccountId.Value);
            if (acc != null)
            {
                if (acc.IsGroup)
                {
                    throw new Volo.Abp.BusinessException(MyERPDomainErrorCodes.AccountIsGroup)
                        .WithData("detail", $"Account '{acc.AccountName}' is a group account. Default inventory account must be a leaf account.");
                }

                // Per ERPNext PR #58923: default inventory account must be an Asset Stock account
                if (acc.AccountType != Accounting.AccountType.Asset || (acc.AccountSubType.HasValue && acc.AccountSubType != Accounting.AccountSubType.Stock))
                {
                    throw new Volo.Abp.BusinessException(MyERPDomainErrorCodes.InvalidAccountType)
                        .WithData("detail", $"Account '{acc.AccountName}' is not a stock asset account. Default inventory account must be an Asset Stock account.");
                }
            }
        }
    }
}
