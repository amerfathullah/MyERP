using System;
using System.Collections.Generic;
using MyERP.Shared;
using Volo.Abp.Application.Dtos;

namespace MyERP.Sales;

public class SubscriptionDto : EntityDto<Guid>
{
    public Guid CompanyId { get; set; }
    public Guid PartyId { get; set; }
    public string PartyType { get; set; } = null!;
    public string? PartyName { get; set; }
    public string? SubscriptionNumber { get; set; }
    public string BillingInterval { get; set; } = null!;
    public int BillingIntervalCount { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public DateTime? CurrentInvoiceStart { get; set; }
    public DateTime? CurrentInvoiceEnd { get; set; }
    public decimal TotalPerInterval { get; set; }
    public int Status { get; set; }
    public SubscriptionPlanDto[] Plans { get; set; } = [];
}

public class SubscriptionPlanDto
{
    public Guid Id { get; set; }
    public Guid ItemId { get; set; }
    public string? ItemName { get; set; }
    public decimal Qty { get; set; }
    public decimal Rate { get; set; }
}

public class CreateSubscriptionDto
{
    public Guid CompanyId { get; set; }
    public Guid PartyId { get; set; }
    public string PartyType { get; set; } = "Customer";
    public string? PartyName { get; set; }
    public string BillingInterval { get; set; } = "Monthly";
    public int BillingIntervalCount { get; set; } = 1;
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public int TrialPeriodDays { get; set; }
    public CreateSubscriptionPlanDto[] Plans { get; set; } = [];
}

public class CreateSubscriptionPlanDto
{
    public Guid ItemId { get; set; }
    public string? ItemName { get; set; }
    public decimal Qty { get; set; }
    public decimal Rate { get; set; }
}

public class GeneratedInvoiceDto
{
    public Guid InvoiceId { get; set; }
    public string? InvoiceNumber { get; set; }
    public decimal GrandTotal { get; set; }
    public DateTime? PeriodStart { get; set; }
    public DateTime? PeriodEnd { get; set; }
}

public class PlanDimensionsDto
{
    public Guid? CostCenterId { get; set; }
}
