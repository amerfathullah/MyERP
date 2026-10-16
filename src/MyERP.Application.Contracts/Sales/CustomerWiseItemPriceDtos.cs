using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace MyERP.Sales;

public class GetCustomerWiseItemPriceInput
{
    [Required]
    public Guid CompanyId { get; set; }

    [Required]
    public Guid CustomerId { get; set; }

    public Guid? ItemId { get; set; }

    public Guid? ItemGroupId { get; set; }
}

public class CustomerWiseItemPriceRowDto
{
    public Guid ItemId { get; set; }

    public string ItemCode { get; set; } = null!;

    public string ItemName { get; set; } = null!;

    public decimal SellingRate { get; set; }

    public string? PriceListName { get; set; }

    public decimal AvailableStock { get; set; }

    public string StockUom { get; set; } = "Unit";
}

public class CustomerWiseItemPriceReportDto
{
    public Guid CustomerId { get; set; }

    public string CustomerName { get; set; } = null!;

    public string? DefaultPriceListName { get; set; }

    public List<CustomerWiseItemPriceRowDto> Rows { get; set; } = new();

    public int TotalItems { get; set; }

    public decimal TotalAvailableStock { get; set; }
}
