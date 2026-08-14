using System;
using System.Collections.Generic;
using MyERP.Shared;
using Volo.Abp.Application.Dtos;

namespace MyERP.Inventory;

public class GetBundleListDto : CompanyFilteredPagedRequestDto
{
    public Guid? ItemId { get; set; }
    public Guid? WarehouseId { get; set; }
    public string? VoucherType { get; set; }
}

public class SerialAndBatchBundleDto : EntityDto<Guid>
{
    public Guid ItemId { get; set; }
    public string ItemName { get; set; } = null!;
    public Guid WarehouseId { get; set; }
    public string TypeOfTransaction { get; set; } = null!;
    public string BundleType { get; set; } = null!;
    public string? VoucherType { get; set; }
    public Guid? VoucherId { get; set; }
    public DateTime PostingDate { get; set; }
    public decimal TotalQty { get; set; }
    public decimal TotalAmount { get; set; }
    public int EntryCount { get; set; }
    public bool IsCancelled { get; set; }
    public List<BundleEntryDto>? Entries { get; set; }
}

public class BundleEntryDto
{
    public string? SerialNo { get; set; }
    public string? BatchNo { get; set; }
    public decimal Qty { get; set; }
    public decimal Rate { get; set; }
}
