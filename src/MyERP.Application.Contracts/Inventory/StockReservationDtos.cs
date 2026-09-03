using System;
using MyERP.Shared;
using Volo.Abp.Application.Dtos;

namespace MyERP.Inventory;

public class StockReservationEntryDto : EntityDto<Guid>
{
    public Guid CompanyId { get; set; }
    public Guid ItemId { get; set; }
    public Guid WarehouseId { get; set; }
    public string VoucherType { get; set; } = null!;
    public Guid VoucherId { get; set; }
    public Guid? VoucherDetailId { get; set; }
    public string? FromVoucherType { get; set; }
    public Guid? FromVoucherId { get; set; }
    public Guid? FromVoucherDetailId { get; set; }
    public decimal ReservedQty { get; set; }
    public decimal DeliveredQty { get; set; }
    public decimal AvailableQty { get; set; }
    public int Status { get; set; }
    public DateTime CreationTime { get; set; }
}

public class CreateStockReservationDto
{
    public Guid CompanyId { get; set; }
    public Guid ItemId { get; set; }
    public Guid WarehouseId { get; set; }
    public string VoucherType { get; set; } = "SalesOrder";
    public Guid VoucherId { get; set; }
    public Guid? VoucherDetailId { get; set; }
    public decimal ReservedQty { get; set; }
    public Guid? BatchId { get; set; }
}

public class GetStockReservationListDto : CompanyFilteredPagedRequestDto
{
    public Guid? ItemId { get; set; }
    public Guid? WarehouseId { get; set; }
    public Guid? VoucherId { get; set; }
    public new string? Status { get; set; }
}
