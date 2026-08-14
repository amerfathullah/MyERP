using System;
using System.ComponentModel.DataAnnotations;
using Volo.Abp.Application.Dtos;

namespace MyERP.Inventory;

public class ManufacturerDto : FullAuditedEntityDto<Guid>
{
    public Guid CompanyId { get; set; }
    public string ShortName { get; set; } = null!;
    public string? FullName { get; set; }
    public string? Website { get; set; }
    public string? Country { get; set; }
    public string? LogoUrl { get; set; }
    public string? Notes { get; set; }
}

public class CreateUpdateManufacturerDto
{
    [Required]
    public Guid CompanyId { get; set; }

    [Required]
    [StringLength(ManufacturerConsts.MaxShortNameLength)]
    public string ShortName { get; set; } = null!;

    [StringLength(ManufacturerConsts.MaxFullNameLength)]
    public string? FullName { get; set; }

    [StringLength(ManufacturerConsts.MaxWebsiteLength)]
    public string? Website { get; set; }

    [StringLength(ManufacturerConsts.MaxCountryLength)]
    public string? Country { get; set; }

    [StringLength(ManufacturerConsts.MaxLogoUrlLength)]
    public string? LogoUrl { get; set; }

    [StringLength(ManufacturerConsts.MaxNotesLength)]
    public string? Notes { get; set; }
}
