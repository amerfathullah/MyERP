using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Volo.Abp.Application.Dtos;

namespace MyERP.Sales;

public class PosProfileDto : FullAuditedEntityDto<Guid>
{
    public Guid CompanyId { get; set; }
    public string ProfileName { get; set; } = null!;
    public Guid WarehouseId { get; set; }
    public Guid? PriceListId { get; set; }
    public Guid? DefaultCustomerId { get; set; }
    public string CurrencyCode { get; set; } = "MYR";
    public bool ValidateStock { get; set; }
    public string InvoiceType { get; set; } = "POS Invoice";
    public bool IsDisabled { get; set; }
    public bool HideUnavailableItems { get; set; }
    public Guid? TaxTemplateId { get; set; }
    public Guid? WriteOffAccountId { get; set; }
    public Guid? WriteOffCostCenterId { get; set; }
    public decimal WriteOffLimit { get; set; }
    public bool PostChangeGlEntries { get; set; }
    public Guid? IncomeAccountId { get; set; }
    public Guid? ExpenseAccountId { get; set; }
    public Guid? ProjectId { get; set; }
    public List<PosProfilePaymentMethodDto> PaymentMethods { get; set; } = new();
    public List<PosProfileUserDto> Users { get; set; } = new();
}

public class PosProfilePaymentMethodDto : EntityDto<Guid>
{
    public Guid PosProfileId { get; set; }
    public Guid ModeOfPaymentId { get; set; }
    public Guid AccountId { get; set; }
    public bool IsDefault { get; set; }
}

public class PosProfileUserDto : EntityDto<Guid>
{
    public Guid PosProfileId { get; set; }
    public Guid UserId { get; set; }
    public bool IsDefault { get; set; }
}

public class CreateUpdatePosProfileDto
{
    [Required]
    public Guid CompanyId { get; set; }

    [Required]
    [MaxLength(140)]
    public string ProfileName { get; set; } = null!;

    [Required]
    public Guid WarehouseId { get; set; }

    public Guid? PriceListId { get; set; }
    public Guid? DefaultCustomerId { get; set; }

    [MaxLength(10)]
    public string CurrencyCode { get; set; } = "MYR";

    public bool ValidateStock { get; set; } = true;

    [MaxLength(50)]
    public string InvoiceType { get; set; } = "POS Invoice";

    public bool IsDisabled { get; set; }
    public bool HideUnavailableItems { get; set; }
    public Guid? TaxTemplateId { get; set; }
    public Guid? WriteOffAccountId { get; set; }
    public Guid? WriteOffCostCenterId { get; set; }
    public decimal WriteOffLimit { get; set; }
    public bool PostChangeGlEntries { get; set; }
    public Guid? IncomeAccountId { get; set; }
    public Guid? ExpenseAccountId { get; set; }
    public Guid? ProjectId { get; set; }
    public List<CreateUpdatePosProfilePaymentMethodDto> PaymentMethods { get; set; } = new();
    public List<CreateUpdatePosProfileUserDto> Users { get; set; } = new();
}

public class CreateUpdatePosProfilePaymentMethodDto
{
    [Required]
    public Guid ModeOfPaymentId { get; set; }

    [Required]
    public Guid AccountId { get; set; }

    public bool IsDefault { get; set; }
}

public class CreateUpdatePosProfileUserDto
{
    [Required]
    public Guid UserId { get; set; }

    public bool IsDefault { get; set; }
}

public class GetPosProfileListDto : PagedAndSortedResultRequestDto
{
    public Guid? CompanyId { get; set; }
    public string? Filter { get; set; }
    public bool? IsDisabled { get; set; }
}
