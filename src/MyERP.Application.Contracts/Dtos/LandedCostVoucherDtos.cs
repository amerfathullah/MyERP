using System;
using MyERP.Core;
using MyERP.Inventory;
using Volo.Abp.Application.Dtos;

namespace MyERP.Dtos;

public class LandedCostVoucherDto : EntityDto<Guid>
{
    public Guid CompanyId { get; set; }
    public string? VoucherNumber { get; set; }
    public DateTime PostingDate { get; set; }
    public LandedCostDistributionMethod DistributionMethod { get; set; }
    public DocumentStatus Status { get; set; }
    public decimal TotalCharges { get; set; }
    public decimal TotalDistributedAmount { get; set; }
    public string? Notes { get; set; }
    public LandedCostItemDto[] Items { get; set; } = [];
    public LandedCostChargeDto[] Charges { get; set; } = [];
    public DateTime CreationTime { get; set; }
}

public class LandedCostItemDto : EntityDto<Guid>
{
    public Guid ReceiptId { get; set; }
    public string ReceiptType { get; set; } = null!;
    public Guid ItemId { get; set; }
    public string? Description { get; set; }
    public decimal Quantity { get; set; }
    public decimal Amount { get; set; }
    public decimal ApplicableCharges { get; set; }
}

public class LandedCostChargeDto : EntityDto<Guid>
{
    public string Description { get; set; } = null!;
    public Guid ExpenseAccountId { get; set; }
    public decimal Amount { get; set; }
}

public class CreateLandedCostVoucherDto
{
    public Guid CompanyId { get; set; }
    public DateTime PostingDate { get; set; }
    public LandedCostDistributionMethod DistributionMethod { get; set; } = LandedCostDistributionMethod.BasedOnAmount;
    public string? Notes { get; set; }
    public CreateLandedCostItemDto[] Items { get; set; } = [];
    public CreateLandedCostChargeDto[] Charges { get; set; } = [];
}

public class CreateLandedCostItemDto
{
    public Guid ReceiptId { get; set; }
    public string ReceiptType { get; set; } = null!;
    public Guid ItemId { get; set; }
    public string? Description { get; set; }
    public decimal Quantity { get; set; }
    public decimal Amount { get; set; }
}

public class CreateLandedCostChargeDto
{
    public string Description { get; set; } = null!;
    public Guid ExpenseAccountId { get; set; }
    public decimal Amount { get; set; }
}

public class GetLandedCostVoucherListDto : PagedAndSortedResultRequestDto
{
    public Guid? CompanyId { get; set; }
    public string? Filter { get; set; }
}
