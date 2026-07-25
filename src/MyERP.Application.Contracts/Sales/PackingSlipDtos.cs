using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Volo.Abp.Application.Dtos;

namespace MyERP.Sales;

public class PackingSlipDto : EntityDto<Guid>
{
    public Guid CompanyId { get; set; }
    public Guid DeliveryNoteId { get; set; }
    public string? DeliveryNoteNumber { get; set; }
    public int FromCaseNo { get; set; }
    public int ToCaseNo { get; set; }
    public int NumberOfCases { get; set; }
    public decimal NetWeightKg { get; set; }
    public decimal GrossWeightKg { get; set; }
    public string? WeightUom { get; set; }
    public int Status { get; set; }
    public DateTime CreationTime { get; set; }
    public List<PackingSlipItemDto> Items { get; set; } = new();
}

public class PackingSlipItemDto : EntityDto<Guid>
{
    public Guid ItemId { get; set; }
    public string? ItemCode { get; set; }
    public string? ItemName { get; set; }
    public decimal Qty { get; set; }
    public decimal NetWeight { get; set; }
    public string? Description { get; set; }
    public Guid? DeliveryNoteItemId { get; set; }
}

public class CreatePackingSlipDto
{
    [Required]
    public Guid CompanyId { get; set; }

    [Required]
    public Guid DeliveryNoteId { get; set; }

    [Range(1, int.MaxValue)]
    public int FromCaseNo { get; set; } = 1;

    [Range(1, int.MaxValue)]
    public int ToCaseNo { get; set; } = 1;

    public decimal GrossWeightKg { get; set; }
    public string? WeightUom { get; set; }

    public List<CreatePackingSlipItemDto> Items { get; set; } = new();
}

public class CreatePackingSlipItemDto
{
    [Required]
    public Guid ItemId { get; set; }

    [Range(0.0001, double.MaxValue)]
    public decimal Qty { get; set; }

    public decimal NetWeight { get; set; }
    public string? Description { get; set; }
    public Guid? DeliveryNoteItemId { get; set; }
}
