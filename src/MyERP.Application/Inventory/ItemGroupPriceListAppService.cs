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

    [Authorize(MyERPPermissions.Items.Create)]
    public async Task<ItemGroupDto> CreateAsync(CreateItemGroupDto input)
    {
        var ig = new ItemGroup(GuidGenerator.Create(), input.Name, input.IsGroup, CurrentTenant.Id)
        {
            ParentId = input.ParentId,
            DefaultWarehouseId = input.DefaultWarehouseId,
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
}
