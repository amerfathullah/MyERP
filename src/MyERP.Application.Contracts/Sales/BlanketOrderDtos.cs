using System;
using MyERP.Shared;
using Volo.Abp.Application.Dtos;

namespace MyERP.Sales;

public class BlanketOrderDto : EntityDto<Guid>
{
    public Guid CompanyId { get; set; }
    public string OrderNumber { get; set; } = null!;
    public string OrderType { get; set; } = null!;
    public Guid PartyId { get; set; }
    public string? PartyName { get; set; }
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public int Status { get; set; }
    public BlanketOrderItemDto[] Items { get; set; } = [];
    public DateTime CreationTime { get; set; }
}

public class BlanketOrderItemDto
{
    public Guid Id { get; set; }
    public Guid ItemId { get; set; }
    public string? ItemName { get; set; }
    public decimal Qty { get; set; }
    public decimal Rate { get; set; }
    public decimal OrderedQty { get; set; }
    public decimal RemainingQty { get; set; }
}

public class CreateBlanketOrderDto
{
    public Guid CompanyId { get; set; }
    public string OrderType { get; set; } = "Selling";
    public Guid PartyId { get; set; }
    public string? PartyName { get; set; }
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public CreateBlanketOrderItemDto[] Items { get; set; } = [];
}

public class CreateBlanketOrderItemDto
{
    public Guid ItemId { get; set; }
    public string? ItemName { get; set; }
    public decimal Qty { get; set; }
    public decimal Rate { get; set; }
}
