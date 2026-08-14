using System;
using System.Collections.Generic;
using Volo.Abp.Application.Dtos;

namespace MyERP.Sales;

public class LoyaltyProgramDto : EntityDto<Guid>
{
    public Guid CompanyId { get; set; }
    public string Name { get; set; } = null!;
    public decimal ConversionFactor { get; set; }
    public int ExpiryDurationDays { get; set; }
    public bool IsEnabled { get; set; }
    public Guid? ExpenseAccountId { get; set; }
    public Guid? CostCenterId { get; set; }
    public List<LoyaltyProgramTierDto> Tiers { get; set; } = new();
}

public class LoyaltyProgramTierDto
{
    public string TierName { get; set; } = null!;
    public decimal MinSpent { get; set; }
    public decimal CollectionFactor { get; set; }
    public decimal RedemptionFactor { get; set; }
}

public class CreateLoyaltyProgramDto
{
    public Guid CompanyId { get; set; }
    public string Name { get; set; } = null!;
    public decimal ConversionFactor { get; set; }
    public int ExpiryDurationDays { get; set; } = 365;
    public Guid? ExpenseAccountId { get; set; }
    public Guid? CostCenterId { get; set; }
    public List<LoyaltyProgramTierDto> Tiers { get; set; } = new();
}

public class UpdateLoyaltyProgramDto
{
    public string Name { get; set; } = null!;
    public decimal ConversionFactor { get; set; }
    public int ExpiryDurationDays { get; set; }
    public bool IsEnabled { get; set; }
    public Guid? ExpenseAccountId { get; set; }
    public Guid? CostCenterId { get; set; }
}

public class LoyaltyBalanceDto
{
    public Guid CustomerId { get; set; }
    public Guid ProgramId { get; set; }
    public string ProgramName { get; set; } = null!;
    public int AvailablePoints { get; set; }
    public string? CurrentTier { get; set; }
    public decimal RedemptionValue { get; set; }
}

public class LoyaltyPointEntryDto
{
    public Guid Id { get; set; }
    public int Points { get; set; }
    public DateTime PostingDate { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public string? InvoiceType { get; set; }
    public Guid? InvoiceId { get; set; }
    public string? TierName { get; set; }
    public bool IsExpired { get; set; }
    public bool IsEarning { get; set; }
}
