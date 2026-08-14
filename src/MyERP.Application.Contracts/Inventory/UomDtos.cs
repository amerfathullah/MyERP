using System;
using Volo.Abp.Application.Dtos;

namespace MyERP.Inventory;

public class UomDto : EntityDto<Guid>
{
    public string UomName { get; set; } = null!;
    public bool MustBeWholeNumber { get; set; }
    public string? Category { get; set; }
    public bool IsEnabled { get; set; }
}

public class CreateUomDto
{
    public string UomName { get; set; } = null!;
    public bool MustBeWholeNumber { get; set; }
    public string? Category { get; set; }
}
