using System;
using System.Collections.Generic;

namespace MyERP.Sales;

public class GetTerritoryWiseSalesInput
{
    public Guid CompanyId { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public Guid? TerritoryId { get; set; }
}

public class TerritoryWiseSalesRowDto
{
    public Guid? TerritoryId { get; set; }
    public string TerritoryName { get; set; } = string.Empty;
    public decimal OpportunityAmount { get; set; }
    public decimal QuotationAmount { get; set; }
    public decimal OrderAmount { get; set; }
    public decimal BillingAmount { get; set; }
    public int OpportunityCount { get; set; }
    public int QuotationCount { get; set; }
    public int OrderCount { get; set; }
    public int BillingCount { get; set; }
}

public class TerritoryWiseSalesReportDto
{
    public string Currency { get; set; } = "MYR";
    public List<TerritoryWiseSalesRowDto> Rows { get; set; } = new();
    public decimal TotalOpportunityAmount { get; set; }
    public decimal TotalQuotationAmount { get; set; }
    public decimal TotalOrderAmount { get; set; }
    public decimal TotalBillingAmount { get; set; }
}
