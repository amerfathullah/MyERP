using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace MyERP.Sales;

public enum AnalyticsGroupBy { Customer = 0, Item = 1, Territory = 2, SalesPerson = 3, ItemGroup = 4, Project = 5 }
public enum AnalyticsPeriodType { Monthly = 0, Quarterly = 1, Yearly = 2 }

public class SalesAnalyticsRequestDto
{
    public Guid CompanyId { get; set; }
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public AnalyticsGroupBy GroupBy { get; set; }
    public AnalyticsPeriodType PeriodType { get; set; }
    public string? ValueField { get; set; }
    public List<string>? EntityIds { get; set; }
    /// <summary>
    /// Date field periods are based on: "PostingDate" (default) or "BillingDate".
    /// Per ERPNext PR #60045: BillingDate falls back to PostingDate (IssueDate) when empty.
    /// </summary>
    public string? PeriodBasedOn { get; set; }
}

public class SalesAnalyticsReportDto
{
    public List<string> PeriodLabels { get; set; } = new();
    public List<SalesAnalyticsRowDto> Rows { get; set; } = new();
    public decimal GrandTotal { get; set; }
    public List<decimal> PeriodTotals { get; set; } = new();
}

public class SalesAnalyticsRowDto
{
    public string EntityId { get; set; } = string.Empty;
    public string EntityName { get; set; } = string.Empty;
    public List<decimal> PeriodValues { get; set; } = new();
    public decimal Total { get; set; }
    public decimal Growth { get; set; }
}

public class CustomerCreditBalanceInputDto
{
    public Guid CompanyId { get; set; }
    public Guid? CustomerId { get; set; }
}

public class CustomerCreditBalanceDto
{
    public Guid CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public decimal CreditLimit { get; set; }
    public decimal OutstandingAmount { get; set; }
    public decimal CreditBalance { get; set; }
    public bool BypassCreditLimitCheckAtSalesOrder { get; set; }
    public bool IsFrozen { get; set; }
    public bool Disabled { get; set; }
}

public class CustomersWithoutSalesInputDto
{
    public Guid? CompanyId { get; set; }
}

public class CustomerWithoutSalesDto
{
    public Guid CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string? CustomerGroup { get; set; }
    public string? Territory { get; set; }
}

public class CustomerWiseItemPriceInputDto
{
    [Required]
    public Guid CustomerId { get; set; }
    public Guid? PriceListId { get; set; }
    public Guid? ItemId { get; set; }
    public Guid? CompanyId { get; set; }
}

public class CustomerWiseItemPriceDto
{
    public Guid ItemId { get; set; }
    public string ItemCode { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public string StockUom { get; set; } = "Unit";
    public decimal SellingRate { get; set; }
    public Guid? PriceListId { get; set; }
    public string? PriceListName { get; set; }
    public decimal AvailableStock { get; set; }
}

public class CustomerAcquisitionAndLoyaltyInputDto
{
    public Guid CompanyId { get; set; }
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public Guid? CustomerId { get; set; }
    public Guid? TerritoryId { get; set; }
    public bool TreeView { get; set; }
}

public class CustomerAcquisitionAndLoyaltyReportDto
{
    public List<CustomerAcquisitionAndLoyaltyRowDto> Rows { get; set; } = new();
    public int TotalNewCustomers { get; set; }
    public int TotalRepeatCustomers { get; set; }
    public decimal TotalNewRevenue { get; set; }
    public decimal TotalRepeatRevenue { get; set; }
    public decimal TotalRevenue { get; set; }
}

public class CustomerAcquisitionAndLoyaltyRowDto
{
    public string Key { get; set; } = string.Empty;
    public int NewCustomerCount { get; set; }
    public decimal NewCustomerRevenue { get; set; }
    public int RepeatCustomerCount { get; set; }
    public decimal RepeatCustomerRevenue { get; set; }
    public decimal TotalRevenue => NewCustomerRevenue + RepeatCustomerRevenue;
}

public class AvailableStockForPackingItemsInputDto
{
    public Guid? CompanyId { get; set; }
    public Guid? WarehouseId { get; set; }
    public Guid? BundleItemId { get; set; }
}

public class AvailableStockForPackingItemDto
{
    public Guid BundleItemId { get; set; }
    public string BundleItemCode { get; set; } = string.Empty;
    public string BundleItemName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? StockUom { get; set; }
    public Guid WarehouseId { get; set; }
    public string WarehouseName { get; set; } = string.Empty;
    public decimal AvailablePackableBundles { get; set; }
}


