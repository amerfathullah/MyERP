using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace MyERP.Sales;

public interface IPosAppService : IApplicationService
{
    Task<PosInvoiceDto> CompleteSaleAsync(CreatePosInvoiceDto input);
    Task<PagedResultDto<PosItemDto>> SearchItemsAsync(PosItemSearchDto input);
}

public class CreatePosInvoiceDto
{
    [Required]
    public Guid CompanyId { get; set; }

    public Guid? CustomerId { get; set; }

    /// <summary>Warehouse from which stock is deducted (required for stock items).</summary>
    public Guid? WarehouseId { get; set; }

    [Required]
    public List<PosLineItemDto> Items { get; set; } = new();

    public string PaymentMethod { get; set; } = "Cash";

    public decimal AmountReceived { get; set; }
}

public class PosLineItemDto
{
    [Required]
    public Guid ItemId { get; set; }

    public string Description { get; set; } = null!;
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TaxAmount { get; set; }
}

public class PosInvoiceDto : EntityDto<Guid>
{
    public string InvoiceNumber { get; set; } = null!;
    public DateTime IssueDate { get; set; }
    public decimal NetTotal { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal GrandTotal { get; set; }
    public decimal AmountReceived { get; set; }
    public decimal Change { get; set; }
    public string Status { get; set; } = null!;
}

public class PosItemDto
{
    public Guid Id { get; set; }
    public string ItemCode { get; set; } = null!;
    public string ItemName { get; set; } = null!;
    public decimal SellingPrice { get; set; }
    public string Uom { get; set; } = null!;
    public string? Barcode { get; set; }
}

public class PosItemSearchDto
{
    public string? Search { get; set; }
    public int MaxResultCount { get; set; } = 20;
}
