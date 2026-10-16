using System;
using System.Collections.Generic;

namespace MyERP.Purchasing;

public class GetSupplierOverviewInputDto
{
    public Guid SupplierId { get; set; }
    public Guid CompanyId { get; set; }
    public string Period { get; set; } = "This fiscal year";
}

public class SupplierOverviewDto
{
    public Guid SupplierId { get; set; }
    public string SupplierName { get; set; } = string.Empty;
    public Guid CompanyId { get; set; }
    public string Currency { get; set; } = "MYR";
    public string Period { get; set; } = "This fiscal year";
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public DateTime AsOfDate { get; set; }

    public SupplierPositionDto Position { get; set; } = new();
    public SupplierTrendDto Trend { get; set; } = new();
    public SupplierOverviewDeliveryDto Delivery { get; set; } = new();
    public SupplierPipelineDto Pipeline { get; set; } = new();
    public SupplierAgeingDto Ageing { get; set; } = new();
    public decimal UnallocatedAdvances { get; set; }
}

public class SupplierPositionDto
{
    public SupplierMetricDto NetPurchases { get; set; } = new();
    public SupplierOutstandingMetricDto Outstanding { get; set; } = new();
    public SupplierMetricDto Overdue { get; set; } = new();
    public SupplierMetricDto Advances { get; set; } = new();
}

public class SupplierMetricDto
{
    public decimal Value { get; set; }
    public int Count { get; set; }
    public decimal? Delta { get; set; }
    public bool DeltaPositiveIsGood { get; set; } = true;
}

public class SupplierOutstandingMetricDto
{
    public decimal Value { get; set; }
    public int UnpaidCount { get; set; }
    public int? DaysToPay { get; set; }
}

public class SupplierTrendDto
{
    public List<SupplierTrendPointDto> Points { get; set; } = new();
    public decimal TotalPurchased { get; set; }
    public decimal TotalPaid { get; set; }
}

public class SupplierTrendPointDto
{
    public string Label { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public decimal Purchased { get; set; }
    public decimal Paid { get; set; }
}

public class SupplierOverviewDeliveryDto
{
    public int OnTime { get; set; }
    public int Late { get; set; }
    public int Overdue { get; set; }
    public int Lines { get; set; }
    public int Orders { get; set; }
    public decimal? OnTimeRate { get; set; }
    public bool HasScorecard { get; set; }
}

public class SupplierPipelineDto
{
    public SupplierQuotationTileDto Quotations { get; set; } = new();
    public SupplierPipelineTileDto Receipt { get; set; } = new();
    public SupplierPipelineTileDto Billing { get; set; } = new();
    public SupplierDeliveryTileDto Late { get; set; } = new();
}

public class SupplierQuotationTileDto
{
    public decimal Value { get; set; }
    public int Count { get; set; }
    public int Expiring { get; set; }
}

public class SupplierPipelineTileDto
{
    public decimal Value { get; set; }
    public int Count { get; set; }
    public DateTime? NextDue { get; set; }
    public List<string> Orders { get; set; } = new();
}

public class SupplierDeliveryTileDto
{
    public decimal Value { get; set; }
    public int Count { get; set; }
    public int? DaysLate { get; set; }
    public List<string> Orders { get; set; } = new();
}

public class SupplierAgeingDto
{
    public List<SupplierAgeingBucketDto> Buckets { get; set; } = new();
    public decimal Total { get; set; }
    public decimal Overdue { get; set; }
    public decimal OverduePct { get; set; }
}

public class SupplierAgeingBucketDto
{
    public string Key { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public decimal Value { get; set; }
    public bool IsOverdue { get; set; }
}

public class GetSupplierTransactionsInputDto
{
    public Guid SupplierId { get; set; }
    public Guid CompanyId { get; set; }
    public string DocType { get; set; } = "All";
    public int MaxResultCount { get; set; } = 20;
}

public class SupplierTransactionDto
{
    public Guid Id { get; set; }
    public string TransactionNumber { get; set; } = string.Empty;
    public string DocType { get; set; } = string.Empty;
    public string TypeLabel { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public string Status { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public decimal? OutstandingAmount { get; set; }
}
