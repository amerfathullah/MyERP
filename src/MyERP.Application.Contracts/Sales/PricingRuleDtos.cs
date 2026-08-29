using System;
using System.Collections.Generic;
using Volo.Abp.Application.Dtos;

namespace MyERP.Sales;

public class PricingRuleDto : EntityDto<Guid>
{
    public string Title { get; set; } = null!;
    public string ApplicableFor { get; set; } = null!;
    public int ApplyOn { get; set; }
    public Guid? ApplyOnId { get; set; }
    public string? ApplyOnName { get; set; }
    public int RuleType { get; set; }
    public decimal DiscountPercentage { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal Rate { get; set; }
    public decimal MinQty { get; set; }
    public decimal MaxQty { get; set; }
    public decimal MinAmount { get; set; }
    public decimal MaxAmount { get; set; }
    public int Priority { get; set; }
    public DateTime? ValidFrom { get; set; }
    public DateTime? ValidUpto { get; set; }
    public bool IsDisabled { get; set; }
    public bool ApplyOnOtherItem { get; set; }
    public Guid? OtherItemId { get; set; }
}

public class CreatePricingRuleDto
{
    public string Title { get; set; } = null!;
    public string ApplicableFor { get; set; } = "Selling";
    public PricingRuleApplyOn ApplyOn { get; set; }
    public Guid? ApplyOnId { get; set; }
    public string? ApplyOnName { get; set; }
    public PricingRuleType RuleType { get; set; }
    public decimal DiscountPercentage { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal Rate { get; set; }
    public decimal MinQty { get; set; }
    public decimal MaxQty { get; set; }
    public decimal MinAmount { get; set; }
    public decimal MaxAmount { get; set; }
    public int Priority { get; set; } = 1;
    public DateTime? ValidFrom { get; set; }
    public DateTime? ValidUpto { get; set; }
    public Guid? CompanyId { get; set; }
    public bool ApplyOnOtherItem { get; set; }
    public Guid? OtherItemId { get; set; }
}

/// <summary>
/// Applies pricing rules to a transaction context and returns matching discounts.
/// </summary>
public class ApplyPricingRuleDto
{
    public Guid? ItemId { get; set; }
    public Guid? ItemGroupId { get; set; }
    public decimal Qty { get; set; }
    public decimal Amount { get; set; }
    public DateTime TransactionDate { get; set; }
}

public class PricingRuleResultDto
{
    public Guid RuleId { get; set; }
    public string Title { get; set; } = null!;
    public int RuleType { get; set; }
    public decimal DiscountPercentage { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal Rate { get; set; }
    public Guid? FreeItemId { get; set; }
    public decimal FreeItemQty { get; set; }
}
