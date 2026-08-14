using System;
using Volo.Abp.Application.Dtos;

namespace MyERP.Inventory;

public class RepostItemValuationDto : EntityDto<Guid>
{
    public Guid CompanyId { get; set; }
    public int BasedOn { get; set; }
    public Guid? ItemId { get; set; }
    public Guid? WarehouseId { get; set; }
    public DateTime PostingDate { get; set; }
    public int Status { get; set; }
    public bool RepostGlEntries { get; set; }
    public int TotalAffectedEntries { get; set; }
    public int CurrentIndex { get; set; }
    public string? ErrorLog { get; set; }
    public string? VoucherType { get; set; }
    public Guid? VoucherId { get; set; }
    public bool IsDeduplicated { get; set; }
    public DateTime CreationTime { get; set; }
}

public class CreateRepostItemValuationDto
{
    public Guid CompanyId { get; set; }
    public int BasedOn { get; set; }
    public Guid? ItemId { get; set; }
    public Guid? WarehouseId { get; set; }
    public DateTime PostingDate { get; set; }
    public bool RepostGlEntries { get; set; } = true;
    public string? VoucherType { get; set; }
    public Guid? VoucherId { get; set; }
}
