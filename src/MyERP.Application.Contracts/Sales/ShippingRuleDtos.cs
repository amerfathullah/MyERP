using System;
using System.Collections.Generic;
using Volo.Abp.Application.Dtos;

namespace MyERP.Sales;

public class ShippingRuleDto : EntityDto<Guid>
{
    public string Label { get; set; } = null!;
    public Guid CompanyId { get; set; }
    public string CalculationMode { get; set; } = null!;
    public string RuleType { get; set; } = null!;
    public decimal ShippingAmount { get; set; }
    public bool IsEnabled { get; set; }
    public Guid? CostCenterId { get; set; }
    public Guid? ProjectId { get; set; }
    public List<ShippingConditionDto> Conditions { get; set; } = new();
    public List<string> Countries { get; set; } = new();
}

public class ShippingConditionDto
{
    public decimal FromValue { get; set; }
    public decimal ToValue { get; set; }
    public decimal ShippingAmount { get; set; }
}

public class CreateShippingRuleDto
{
    public string Label { get; set; } = null!;
    public Guid CompanyId { get; set; }
    public Guid AccountId { get; set; }
    public Guid? CostCenterId { get; set; }
    public Guid? ProjectId { get; set; }
    public ShippingCalculationMode CalculationMode { get; set; }
    public ShippingRuleType RuleType { get; set; }
    public decimal FixedAmount { get; set; }
    public bool IsEnabled { get; set; } = true;
    public List<ShippingConditionDto> Conditions { get; set; } = new();
    public List<string> Countries { get; set; } = new();
}
