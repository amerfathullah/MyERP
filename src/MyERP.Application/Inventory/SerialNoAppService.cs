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
public class SerialNoAppService : ApplicationService, ISerialNoAppService
{
    private readonly IRepository<SerialNo, Guid> _repository;

    public SerialNoAppService(IRepository<SerialNo, Guid> repository) => _repository = repository;

    public async Task<PagedResultDto<SerialNoDto>> GetListAsync(GetSerialNoListDto input)
    {
        var query = await _repository.GetQueryableAsync();
        if (input.ItemId.HasValue)
            query = query.Where(s => s.ItemId == input.ItemId.Value);
        if (input.WarehouseId.HasValue)
        {
            var whRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Warehouse, Guid>>();
            var wh = await whRepo.FindAsync(input.WarehouseId.Value);
            if (wh?.IsGroup == true)
            {
                var whQuery = await whRepo.GetQueryableAsync();
                var childWarehouseIds = whQuery
                    .Where(w => w.ParentWarehouseId == input.WarehouseId.Value || w.Id == input.WarehouseId.Value)
                    .Select(w => w.Id)
                    .ToList();
                query = query.Where(s => s.WarehouseId.HasValue && childWarehouseIds.Contains(s.WarehouseId.Value));
            }
            else
            {
                query = query.Where(s => s.WarehouseId == input.WarehouseId.Value);
            }
        }
        if (!string.IsNullOrWhiteSpace(input.Filter))
        {
            var f = input.Filter;
            query = query.Where(s => s.SerialNumber.Contains(f));
        }
        var totalCount = query.Count();
        var items = query.OrderByDescending(s => s.CreationTime)
            .Skip(input.SkipCount).Take(input.MaxResultCount).ToList();
        return new PagedResultDto<SerialNoDto>(totalCount, items.Select(x => ObjectMapper.Map<SerialNo, SerialNoDto>(x)).ToList());
    }

    public async Task<SerialNoDto> GetAsync(Guid id)
    {
        var sn = await _repository.GetAsync(id);
        return ObjectMapper.Map<SerialNo, SerialNoDto>(sn);
    }
}
