using System;
using System.Collections.Generic;
using Volo.Abp.Application.Dtos;

namespace MyERP.Purchasing;

public class RfqDto : EntityDto<Guid>
{
    public Guid CompanyId { get; set; }
    public string RfqNumber { get; set; } = null!;
    public DateTime TransactionDate { get; set; }
    public string CurrencyCode { get; set; } = null!;
    public string? MessageForSupplier { get; set; }
    public string Status { get; set; } = null!;
    public List<RfqItemDto> Items { get; set; } = new();
    public List<RfqSupplierDto> Suppliers { get; set; } = new();
}

public class RfqItemDto : EntityDto<Guid>
{
    public Guid ItemId { get; set; }
    public string Description { get; set; } = null!;
    public decimal Qty { get; set; }
    public string Uom { get; set; } = null!;
}

public class RfqSupplierDto : EntityDto<Guid>
{
    public Guid SupplierId { get; set; }
    public string SupplierName { get; set; } = null!;
    public string? Email { get; set; }
    public bool EmailSent { get; set; }
    public string QuoteStatus { get; set; } = null!;
}

public class CreateRfqDto
{
    public Guid CompanyId { get; set; }
    public DateTime TransactionDate { get; set; }
    public string? CurrencyCode { get; set; }
    public string? MessageForSupplier { get; set; }
    public List<CreateRfqItemDto> Items { get; set; } = new();
    public List<CreateRfqSupplierDto> Suppliers { get; set; } = new();
}

public class CreateRfqItemDto
{
    public Guid ItemId { get; set; }
    public string Description { get; set; } = null!;
    public decimal Qty { get; set; }
    public string Uom { get; set; } = "Unit";
}

public class CreateRfqSupplierDto
{
    public Guid SupplierId { get; set; }
    public string? Email { get; set; }
}
