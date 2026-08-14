using System;
using Volo.Abp.Application.Dtos;

namespace MyERP.Purchasing;

public class SupplierQuotationDto : EntityDto<Guid>
{
    public Guid CompanyId { get; set; }
    public Guid SupplierId { get; set; }
    public string? SupplierName { get; set; }
    public string? QuotationNumber { get; set; }
    public DateTime TransactionDate { get; set; }
    public DateTime? ValidTill { get; set; }
    public string Currency { get; set; } = null!;
    public decimal NetTotal { get; set; }
    public decimal GrandTotal { get; set; }
    public int Status { get; set; }
    public SupplierQuotationItemDto[] Items { get; set; } = [];
}

public class SupplierQuotationItemDto : EntityDto<Guid>
{
    public Guid ItemId { get; set; }
    public string? ItemName { get; set; }
    public decimal Qty { get; set; }
    public decimal Rate { get; set; }
    public decimal Amount { get; set; }
}

public class CreateSupplierQuotationDto
{
    public Guid CompanyId { get; set; }
    public Guid SupplierId { get; set; }
    public string? SupplierName { get; set; }
    public DateTime TransactionDate { get; set; }
    public DateTime? ValidTill { get; set; }
    public string Currency { get; set; } = "MYR";
    public Guid? RequestForQuotationId { get; set; }
    public CreateSQItemDto[] Items { get; set; } = [];
}

public class CreateSQItemDto
{
    public Guid ItemId { get; set; }
    public string? ItemName { get; set; }
    public decimal Qty { get; set; }
    public decimal Rate { get; set; }
}
