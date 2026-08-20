using System;
using System.ComponentModel.DataAnnotations;
using Volo.Abp.Application.Dtos;

namespace MyERP.Tax;

public class LowerDeductionCertificateDto : FullAuditedEntityDto<Guid>
{
    public Guid CompanyId { get; set; }
    public Guid SupplierId { get; set; }
    public string? SupplierName { get; set; }
    public Guid TaxWithholdingCategoryId { get; set; }
    public string? TaxWithholdingCategoryName { get; set; }
    public string CertificateNumber { get; set; } = null!;
    public decimal Rate { get; set; }
    public decimal CertificateLimit { get; set; }
    public DateTime ValidFrom { get; set; }
    public DateTime ValidUpto { get; set; }
}

public class CreateUpdateLowerDeductionCertificateDto
{
    [Required]
    public Guid CompanyId { get; set; }

    [Required]
    public Guid SupplierId { get; set; }

    [Required]
    public Guid TaxWithholdingCategoryId { get; set; }

    [Required]
    [StringLength(100)]
    public string CertificateNumber { get; set; } = null!;

    [Range(0, 100)]
    public decimal Rate { get; set; }

    [Range(0, double.MaxValue)]
    public decimal CertificateLimit { get; set; }

    [Required]
    public DateTime ValidFrom { get; set; }

    [Required]
    public DateTime ValidUpto { get; set; }
}

public class GetLowerDeductionCertificateListDto : PagedAndSortedResultRequestDto
{
    public Guid? CompanyId { get; set; }
    public Guid? SupplierId { get; set; }
}
