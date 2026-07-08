using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Volo.Abp.Application.Dtos;

namespace MyERP.Tax;

public class TaxCategoryDto : FullAuditedEntityDto<Guid>
{
    public string Code { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public string TaxType { get; set; } = null!;
    public bool IsActive { get; set; }
}

public class CreateUpdateTaxCategoryDto
{
    [Required]
    [StringLength(20)]
    public string Code { get; set; } = null!;

    [Required]
    [StringLength(128)]
    public string Name { get; set; } = null!;

    [StringLength(500)]
    public string? Description { get; set; }

    [Required]
    public TaxType TaxType { get; set; }

    public bool IsActive { get; set; } = true;
}

public class TaxRuleDto : EntityDto<Guid>
{
    public Guid TaxCategoryId { get; set; }
    public decimal Rate { get; set; }
    public DateTime EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }
    public string? ItemGroupFilter { get; set; }
    public string? RegionFilter { get; set; }
    public int Priority { get; set; }
    public string? Description { get; set; }
    public bool IsActive { get; set; }
}

public class CreateUpdateTaxRuleDto
{
    [Required]
    public Guid TaxCategoryId { get; set; }

    [Required]
    [Range(0, 100)]
    public decimal Rate { get; set; }

    [Required]
    public DateTime EffectiveFrom { get; set; }

    public DateTime? EffectiveTo { get; set; }

    [StringLength(100)]
    public string? ItemGroupFilter { get; set; }

    [StringLength(100)]
    public string? RegionFilter { get; set; }

    public int Priority { get; set; }

    [StringLength(500)]
    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;
}
