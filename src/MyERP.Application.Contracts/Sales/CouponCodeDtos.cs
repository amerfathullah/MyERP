using System;
using Volo.Abp.Application.Dtos;

namespace MyERP.Sales;

public class CouponCodeDto : EntityDto<Guid>
{
    public string Code { get; set; } = null!;
    public string CouponName { get; set; } = null!;
    public CouponType CouponType { get; set; }
    public Guid PricingRuleId { get; set; }
    public int MaximumUse { get; set; }
    public int Used { get; set; }
    public bool IsEnabled { get; set; }
    public DateTime? ValidFrom { get; set; }
    public DateTime? ValidUpto { get; set; }
    public Guid? CustomerId { get; set; }
    public string? Description { get; set; }
}

public class CreateCouponCodeDto
{
    public string? Code { get; set; }
    public string CouponName { get; set; } = null!;
    public CouponType CouponType { get; set; }
    public Guid PricingRuleId { get; set; }
    public Guid? CompanyId { get; set; }
    public int MaximumUse { get; set; }
    public int MaximumUsePerCustomer { get; set; }
    public DateTime? ValidFrom { get; set; }
    public DateTime? ValidUpto { get; set; }
    public Guid? CustomerId { get; set; }
    public string? Description { get; set; }
}
