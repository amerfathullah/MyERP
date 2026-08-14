using System;
using Volo.Abp.Application.Dtos;

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
