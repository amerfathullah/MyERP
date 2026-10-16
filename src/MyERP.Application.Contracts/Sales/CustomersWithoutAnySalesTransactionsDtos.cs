using System;
using System.Collections.Generic;
using Volo.Abp.Application.Dtos;

namespace MyERP.Sales;

public class CustomersWithoutAnySalesTransactionsFilterDto
{
    public Guid? CompanyId { get; set; }
    public Guid? CustomerGroupId { get; set; }
    public Guid? TerritoryId { get; set; }
}

public class CustomerWithoutSalesItemDto
{
    public Guid CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string? CustomerCode { get; set; }
    public string? Territory { get; set; }
    public string? CustomerGroup { get; set; }
    public DateTime CreationTime { get; set; }
}

public class CustomersWithoutAnySalesTransactionsReportDto
{
    public List<CustomerWithoutSalesItemDto> Items { get; set; } = new();
    public int TotalCustomers { get; set; }
}
