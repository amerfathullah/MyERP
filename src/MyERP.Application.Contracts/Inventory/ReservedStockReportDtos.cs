using System;
using System.Collections.Generic;

namespace MyERP.Inventory;

public class ReservedStockFilterDto
{
    public Guid? CompanyId { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public Guid? ItemId { get; set; }
    public Guid? WarehouseId { get; set; }
    public string? VoucherType { get; set; }
    public string? Status { get; set; }
    public string? SearchTerm { get; set; }
}

public class ReservedStockRowDto
{
    public Guid StockReservationEntryId { get; set; }
    public DateTime Date { get; set; }
    public Guid WarehouseId { get; set; }
    public string WarehouseName { get; set; } = string.Empty;
    public Guid ItemId { get; set; }
    public string ItemCode { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public string? StockUom { get; set; }
    public decimal VoucherQty { get; set; }
    public decimal ReservedQty { get; set; }
    public decimal DeliveredQty { get; set; }
    public decimal TransferredQty { get; set; }
    public decimal ConsumedQty { get; set; }
    public decimal AvailableQtyToReserve { get; set; }
    public string VoucherType { get; set; } = string.Empty;
    public Guid VoucherId { get; set; }
    public string? FromVoucherType { get; set; }
    public Guid? FromVoucherId { get; set; }
    public string Status { get; set; } = string.Empty;
    public Guid? BatchId { get; set; }
    public string? BatchNo { get; set; }
}

public class ReservedStockReportDto
{
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public List<ReservedStockRowDto> Rows { get; set; } = new();
    public int TotalEntries { get; set; }
    public decimal TotalReservedQty { get; set; }
    public decimal TotalDeliveredQty { get; set; }
    public decimal TotalTransferredQty { get; set; }
    public decimal TotalConsumedQty { get; set; }
    public decimal TotalAvailableQty { get; set; }
}
