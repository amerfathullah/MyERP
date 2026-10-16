using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace MyERP.Sales;

public class GetLostQuotationsReportInput
{
    [Required]
    public Guid CompanyId { get; set; }

    public DateTime? FromDate { get; set; }

    public DateTime? ToDate { get; set; }

    public string? Timespan { get; set; } = "This Year";

    /// <summary>
    /// Group by dimension: "Lost Reason" or "Competitor"
    /// </summary>
    public string GroupBy { get; set; } = "Lost Reason";
}

public class LostQuotationReportRowDto
{
    /// <summary>
    /// Lost reason name or competitor name, or "Not Specified"
    /// </summary>
    public string Dimension { get; set; } = null!;

    public int LostQuotations { get; set; }

    public decimal LostQuotationsPct { get; set; }

    public decimal LostValue { get; set; }

    public decimal LostValuePct { get; set; }

    public string Currency { get; set; } = "MYR";
}

public class LostQuotationsSummaryDto
{
    public int TotalLostQuotations { get; set; }

    public decimal TotalLostValue { get; set; }

    public string Currency { get; set; } = "MYR";
}

public class LostQuotationsChartDto
{
    public List<string> Labels { get; set; } = new();

    public List<decimal> Values { get; set; } = new();

    public List<int> Counts { get; set; } = new();
}

public class LostQuotationsReportDto
{
    public List<LostQuotationReportRowDto> Rows { get; set; } = new();

    public LostQuotationsSummaryDto Summary { get; set; } = new();

    public LostQuotationsChartDto Chart { get; set; } = new();
}
