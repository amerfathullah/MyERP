using System;
using System.Collections.Generic;

namespace MyERP.Sales;

public class RegisterReportDto<T>
{
    public List<T> Items { get; set; } = new();
    public decimal TotalNet { get; set; }
    public decimal TotalTax { get; set; }
    public decimal TotalGrand { get; set; }
    public int Count { get; set; }
}

public class RegisterFilterDto
{
    public Guid CompanyId { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
}
