using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Volo.Abp.Application.Dtos;

namespace MyERP.Accounting;

public class AccountingDimensionDto : EntityDto<Guid>
{
    public string DocumentType { get; set; } = null!;
    public string Label { get; set; } = null!;
    public string FieldName { get; set; } = null!;
    public bool IsEnabled { get; set; }
    public bool IsMandatory { get; set; }
    public Guid? CompanyId { get; set; }
}

public class CreateAccountingDimensionDto
{
    [Required]
    [StringLength(100)]
    public string DocumentType { get; set; } = null!;

    [Required]
    [StringLength(200)]
    public string Label { get; set; } = null!;

    public bool IsMandatory { get; set; }
    public Guid? CompanyId { get; set; }
}

public class UpdateAccountingDimensionDto
{
    [Required]
    [StringLength(200)]
    public string Label { get; set; } = null!;

    public bool IsMandatory { get; set; }
    public bool HideDisabledValues { get; set; } = true;
    public Guid? CompanyId { get; set; }
}

public class AccountingDimensionFilterDto : EntityDto<Guid>
{
    public Guid AccountingDimensionId { get; set; }
    public Guid AccountId { get; set; }
    public Guid CompanyId { get; set; }
    public bool IsAllowList { get; set; }
    public string DimensionValueIds { get; set; } = string.Empty;
}

public class CreateDimensionFilterDto
{
    public Guid AccountingDimensionId { get; set; }
    public Guid AccountId { get; set; }
    public Guid CompanyId { get; set; }
    public bool IsAllowList { get; set; } = true;
    public string? DimensionValueIds { get; set; }
}
