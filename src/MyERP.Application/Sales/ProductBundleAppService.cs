using System;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Sales.Entities;
using MyERP.Permissions;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Sales;

public class ProductBundleDto : EntityDto<Guid>
{
    public Guid ItemId { get; set; }
    public string? ItemName { get; set; }
    public string? Description { get; set; }
    public bool IsActive { get; set; }
    public ProductBundleItemDto[] Items { get; set; } = [];
}

public class ProductBundleItemDto
{
    public Guid Id { get; set; }
    public Guid ComponentItemId { get; set; }
    public string? ItemName { get; set; }
    public decimal Qty { get; set; }
}

public class CreateProductBundleDto
{
    public Guid ItemId { get; set; }
    public string? ItemName { get; set; }
    public string? Description { get; set; }
    public CreateProductBundleItemDto[] Items { get; set; } = [];
}

public class CreateProductBundleItemDto
{
    public Guid ComponentItemId { get; set; }
    public string? ItemName { get; set; }
    public decimal Qty { get; set; }
}

[Authorize(MyERPPermissions.Items.Default)]
public class ProductBundleAppService : ApplicationService
{
    private readonly IRepository<ProductBundle, Guid> _repository;
    public ProductBundleAppService(IRepository<ProductBundle, Guid> repository) => _repository = repository;

    public async Task<PagedResultDto<ProductBundleDto>> GetListAsync(PagedAndSortedResultRequestDto input)
    {
        var query = (await _repository.WithDetailsAsync()).AsQueryable();
        var totalCount = query.Count();
        var items = query.OrderBy(b => b.ItemName)
            .Skip(input.SkipCount).Take(input.MaxResultCount).ToList();
        return new PagedResultDto<ProductBundleDto>(totalCount, items.Select(x => ObjectMapper.Map<ProductBundle, ProductBundleDto>(x)).ToList());
    }

    [Authorize(MyERPPermissions.Items.Create)]
    public async Task<ProductBundleDto> CreateAsync(CreateProductBundleDto input)
    {
        var bundle = new ProductBundle(GuidGenerator.Create(), input.ItemId, CurrentTenant.Id)
        { ItemName = input.ItemName, Description = input.Description };
        foreach (var item in input.Items)
            bundle.AddItem(item.ComponentItemId, item.Qty, item.ItemName);
        await _repository.InsertAsync(bundle);
        return ObjectMapper.Map<ProductBundle, ProductBundleDto>(bundle);
    }

    [Authorize(MyERPPermissions.Items.Edit)]
    public async Task<ProductBundleDto> DeactivateAsync(Guid id)
    {
        var bundle = await _repository.GetAsync(id);
        bundle.Deactivate();
        await _repository.UpdateAsync(bundle);
        return ObjectMapper.Map<ProductBundle, ProductBundleDto>(bundle);
    }
}
