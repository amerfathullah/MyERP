using System;
using System.Collections.Generic;

namespace MyERP.Sales;

public class GetCustomerOverviewInputDto
{
    public Guid CustomerId { get; set; }
    public Guid CompanyId { get; set; }
    public string Period { get; set; } = "This fiscal year";
}

public class CustomerOverviewDto
{
    public Guid CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public Guid CompanyId { get; set; }
    public string Currency { get; set; } = "MYR";
    public string Period { get; set; } = "This fiscal year";
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public DateTime AsOfDate { get; set; }

    public CustomerPositionDto Position { get; set; } = new();
    public CustomerTrendDto Trend { get; set; } = new();
    public CustomerAgeingDto Ageing { get; set; } = new();
    public CustomerPipelineDto Pipeline { get; set; } = new();
    public decimal UnallocatedAdvances { get; set; }
}

public class CustomerPositionDto
{
    public CustomerMetricDto NetSales { get; set; } = new();
    public CustomerOutstandingMetricDto Outstanding { get; set; } = new();
    public CustomerMetricDto Overdue { get; set; } = new();
    public CustomerCreditMetricDto Credit { get; set; } = new();
}

public class CustomerMetricDto
{
    public decimal Value { get; set; }
    public int Count { get; set; }
    public decimal? Delta { get; set; }
    public bool DeltaPositiveIsGood { get; set; } = true;
}

public class CustomerOutstandingMetricDto
{
    public decimal Value { get; set; }
    public int UnpaidCount { get; set; }
    public int? DaysToPay { get; set; }
}

public class CustomerCreditMetricDto
{
    public decimal Limit { get; set; }
    public decimal? UsedPct { get; set; }
}

public class CustomerTrendDto
{
    public List<CustomerTrendPointDto> Points { get; set; } = new();
    public decimal Average { get; set; }
    public bool HasMtd { get; set; }
}

public class CustomerTrendPointDto
{
    public string Label { get; set; } = string.Empty;
    public decimal Value { get; set; }
    public bool IsMtd { get; set; }
}

public class CustomerAgeingDto
{
    public List<CustomerAgeingBucketDto> Buckets { get; set; } = new();
    public decimal Total { get; set; }
    public decimal Overdue { get; set; }
    public decimal OverduePct { get; set; }
}

public class CustomerAgeingBucketDto
{
    public string Key { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public decimal Value { get; set; }
    public bool IsOverdue { get; set; }
}

public class CustomerPipelineDto
{
    public CustomerPipelineTileDto Quotations { get; set; } = new();
    public CustomerDeliveryTileDto Delivery { get; set; } = new();
    public CustomerPipelineTileDto Billing { get; set; } = new();
    public CustomerInvoiceTileDto Invoices { get; set; } = new();
}

public class CustomerPipelineTileDto
{
    public decimal Value { get; set; }
    public int Count { get; set; }
}

public class CustomerDeliveryTileDto
{
    public decimal Value { get; set; }
    public int Count { get; set; }
    public int PastDue { get; set; }
}

public class CustomerInvoiceTileDto
{
    public decimal Value { get; set; }
    public int Count { get; set; }
    public int Overdue { get; set; }
}

public class GetCustomerTransactionsInputDto
{
    public Guid CustomerId { get; set; }
    public Guid CompanyId { get; set; }
    public string DocType { get; set; } = "All";
    public int MaxResultCount { get; set; } = 20;
}

public class CustomerTransactionDto
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
