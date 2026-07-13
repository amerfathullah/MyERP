using System;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Inventory.Entities;
using MyERP.Permissions;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Inventory;

// --- Item Group ---
public class ItemGroupDto : EntityDto<Guid>
{
    public string Name { get; set; } = null!;
    public Guid? ParentId { get; set; }
    public bool IsGroup { get; set; }
    public Guid? DefaultWarehouseId { get; set; }
}

public class CreateItemGroupDto
{
    public string Name { get; set; } = null!;
    public Guid? ParentId { get; set; }
    public bool IsGroup { get; set; }
    public Guid? DefaultWarehouseId { get; set; }
}

[Authorize(MyERPPermissions.Items.Default)]
public class ItemGroupAppService : ApplicationService
{
    private readonly IRepository<ItemGroup, Guid> _repository;
    public ItemGroupAppService(IRepository<ItemGroup, Guid> repository) => _repository = repository;

    public async Task<PagedResultDto<ItemGroupDto>> GetListAsync(PagedAndSortedResultRequestDto input)
    {
        var query = await _repository.GetQueryableAsync();
        var totalCount = query.Count();
        var items = query.OrderBy(g => g.Name)
            .Skip(input.SkipCount).Take(input.MaxResultCount).ToList();
        return new PagedResultDto<ItemGroupDto>(totalCount, items.Select(MapToDto).ToList());
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
        return MapToDto(ig);
    }

    private static ItemGroupDto MapToDto(ItemGroup g) => new()
    {
        Id = g.Id, Name = g.Name, ParentId = g.ParentId,
        IsGroup = g.IsGroup, DefaultWarehouseId = g.DefaultWarehouseId,
    };
}
