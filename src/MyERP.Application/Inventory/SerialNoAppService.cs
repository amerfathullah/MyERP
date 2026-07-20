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

public class SerialNoDto : EntityDto<Guid>
{
    public string SerialNumber { get; set; } = null!;
    public Guid ItemId { get; set; }
    public Guid? WarehouseId { get; set; }
    public Guid CompanyId { get; set; }
    public Guid? BatchId { get; set; }
    public Guid? CustomerId { get; set; }
    public decimal PurchaseRate { get; set; }
    public DateTime? WarrantyExpiryDate { get; set; }
    public DateTime? AmcExpiryDate { get; set; }
    public string MaintenanceStatus { get; set; } = null!;
    public int Status { get; set; }
    public DateTime CreationTime { get; set; }
}

public class GetSerialNoListDto : PagedAndSortedResultRequestDto
{
    public Guid? ItemId { get; set; }
    public Guid? WarehouseId { get; set; }
    public string? Filter { get; set; }
}

[Authorize(MyERPPermissions.Items.Default)]
public class SerialNoAppService : ApplicationService
{
    private readonly IRepository<SerialNo, Guid> _repository;

    public SerialNoAppService(IRepository<SerialNo, Guid> repository) => _repository = repository;

    public async Task<PagedResultDto<SerialNoDto>> GetListAsync(GetSerialNoListDto input)
    {
        var query = await _repository.GetQueryableAsync();
        if (input.ItemId.HasValue)
            query = query.Where(s => s.ItemId == input.ItemId.Value);
        if (input.WarehouseId.HasValue)
            query = query.Where(s => s.WarehouseId == input.WarehouseId.Value);
        if (!string.IsNullOrWhiteSpace(input.Filter))
        {
            var f = input.Filter;
            query = query.Where(s => s.SerialNumber.ToLower().Contains(f));
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

