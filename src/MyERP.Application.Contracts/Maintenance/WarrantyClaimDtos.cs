using System;
using System.ComponentModel.DataAnnotations;
using Volo.Abp.Application.Dtos;

namespace MyERP.Maintenance;

public class WarrantyClaimDto : EntityDto<Guid>
{
    public Guid CompanyId { get; set; }
    public string ClaimNumber { get; set; } = null!;
    public Guid CustomerId { get; set; }
    public string? CustomerName { get; set; }
    public Guid ItemId { get; set; }
    public string? ItemName { get; set; }
    public Guid? SerialNoId { get; set; }
    public Guid? SalesInvoiceId { get; set; }
    public DateTime? WarrantyExpiryDate { get; set; }
    public DateTime? AmcExpiryDate { get; set; }
    public DateTime ComplaintDate { get; set; }
    public string? Complaint { get; set; }
    public string? Resolution { get; set; }
    public DateTime? ResolutionDate { get; set; }
    public int Status { get; set; }
    public bool IsUnderWarranty { get; set; }
}

public class CreateWarrantyClaimDto
{
    [Required]
    public Guid CompanyId { get; set; }

    [Required]
    public Guid CustomerId { get; set; }

    [Required]
    public Guid ItemId { get; set; }

    public Guid? SerialNoId { get; set; }
    public Guid? SalesInvoiceId { get; set; }
    public DateTime? WarrantyExpiryDate { get; set; }
    public DateTime? AmcExpiryDate { get; set; }
    public DateTime ComplaintDate { get; set; }

    [StringLength(2000)]
    public string? Complaint { get; set; }
}

public class GetWarrantyClaimListDto : PagedAndSortedResultRequestDto
{
    public string? Filter { get; set; }
    public Guid? CompanyId { get; set; }
    public int? Status { get; set; }
}
