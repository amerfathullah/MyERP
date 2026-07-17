using System;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Inventory.Entities;
using MyERP.Permissions;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Inventory;

[Authorize(MyERPPermissions.Items.Default)]
public class BatchAppService : ApplicationService
{
    private readonly IRepository<Batch, Guid> _repository;

    public BatchAppService(IRepository<Batch, Guid> repository)
    {
        _repository = repository;
    }

    public async Task<BatchDto> GetAsync(Guid id)
    {
        var batch = await _repository.GetAsync(id);
        return ObjectMapper.Map<Batch, BatchDto>(batch);
    }

    public async Task<PagedResultDto<BatchDto>> GetListAsync(GetBatchListDto input)
    {
        var query = await _repository.GetQueryableAsync();
        if (input.ItemId.HasValue)
            query = query.Where(b => b.ItemId == input.ItemId.Value);
        if (input.IsDisabled.HasValue)
            query = query.Where(b => b.IsDisabled == input.IsDisabled.Value);
        if (!string.IsNullOrWhiteSpace(input.Filter))
        {
            var f = input.Filter.ToLower();
            query = query.Where(b => b.BatchNo.ToLower().Contains(f));
        }

        var totalCount = query.Count();
        var items = query.OrderByDescending(b => b.CreationTime)
            .Skip(input.SkipCount).Take(input.MaxResultCount).ToList();

        return new PagedResultDto<BatchDto>(totalCount, items.Select(ObjectMapper.Map<Batch, BatchDto>).ToList());
    }

    [Authorize(MyERPPermissions.Items.Create)]
    public async Task<BatchDto> CreateAsync(CreateBatchDto input)
    {
        var batch = new Batch(GuidGenerator.Create(), input.ItemId, input.BatchNo, CurrentTenant.Id)
        {
            ManufacturingDate = input.ManufacturingDate,
            ExpiryDate = input.ExpiryDate,
            ShelfLifeInDays = input.ShelfLifeInDays,
            SupplierBatchNo = input.SupplierBatchNo,
            Description = input.Description,
        };

        if (batch.ManufacturingDate.HasValue && batch.ShelfLifeInDays.HasValue && !batch.ExpiryDate.HasValue)
            batch.SetExpiryFromShelfLife();

        await _repository.InsertAsync(batch);
        return ObjectMapper.Map<Batch, BatchDto>(batch);
    }

    [Authorize(MyERPPermissions.Items.Edit)]
    public async Task DisableAsync(Guid id)
    {
        var batch = await _repository.GetAsync(id);
        batch.IsDisabled = true;
        await _repository.UpdateAsync(batch);
    }
}

public class BatchDto : AuditedEntityDto<Guid>
{
    public string BatchNo { get; set; } = null!;
    public Guid ItemId { get; set; }
    public DateTime? ManufacturingDate { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public int? ShelfLifeInDays { get; set; }
    public string? SupplierBatchNo { get; set; }
    public bool IsDisabled { get; set; }
    public bool IsExpired { get; set; }
    public string? Description { get; set; }
}

public class CreateBatchDto
{
    [Required] public Guid ItemId { get; set; }
    [Required][StringLength(100)] public string BatchNo { get; set; } = null!;
    public DateTime? ManufacturingDate { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public int? ShelfLifeInDays { get; set; }
    [StringLength(100)] public string? SupplierBatchNo { get; set; }
    [StringLength(500)] public string? Description { get; set; }
}

public class GetBatchListDto : PagedAndSortedResultRequestDto
{
    public Guid? ItemId { get; set; }
    public bool? IsDisabled { get; set; }
    public string? Filter { get; set; }
}
