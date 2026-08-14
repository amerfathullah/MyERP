using System;
using Volo.Abp.Application.Dtos;

namespace MyERP.Inventory;

public class UomConversionDto : EntityDto<Guid>
{
    public string FromUom { get; set; } = null!;
    public string ToUom { get; set; } = null!;
    public decimal ConversionFactor { get; set; }
    public Guid? ItemId { get; set; }
}
