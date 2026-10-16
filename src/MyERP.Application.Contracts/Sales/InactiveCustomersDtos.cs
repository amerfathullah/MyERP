using System;
using System.Collections.Generic;
using Volo.Abp.Application.Dtos;

namespace MyERP.Sales;

public class InactiveCustomersFilterDto
{
    public Guid? CompanyId { get; set; }
    public string DocType { get; set; } = "Sales Order";
    public int DaysSinceLastOrder { get; set; } = 60;
    public Guid? CustomerGroupId { get; set; }
    public Guid? TerritoryId { get; set; }
    public Guid? CustomerId { get; set; }
    public DateTime? AsOfDate { get; set; }
}

public class InactiveCustomerItemDto
{
    public Guid CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string? CustomerCode { get; set; }
    public string? Territory { get; set; }
    public string? CustomerGroup { get; set; }
    public int NumOfOrder { get; set; }
    public decimal TotalOrderValue { get; set; }
    public decimal TotalOrderConsidered { get; set; }
    public decimal LastOrderAmount { get; set; }
    public DateTime? LastOrderDate { get; set; }
    public int DaysSinceLastOrder { get; set; }
}

public class InactiveCustomersReportDto
{
    public List<InactiveCustomerItemDto> Items { get; set; } = new();
    public int TotalCustomers { get; set; }
    public int TotalOrders { get; set; }
    public decimal TotalOrderValue { get; set; }
    public decimal TotalOrderConsidered { get; set; }
    public string DocType { get; set; } = "Sales Order";
    public int DaysSinceLastOrderThreshold { get; set; }
}
