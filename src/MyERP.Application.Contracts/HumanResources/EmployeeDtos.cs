using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Volo.Abp.Application.Dtos;

namespace MyERP.HumanResources;

public class EmployeeDto : FullAuditedEntityDto<Guid>
{
    public Guid CompanyId { get; set; }
    public string EmployeeId { get; set; } = null!;
    public string FirstName { get; set; } = null!;
    public string? LastName { get; set; }
    public string? FullName { get; set; }
    public DateTime? DateOfBirth { get; set; }
    public DateTime? DateOfJoining { get; set; }
    public DateTime? DateOfResignation { get; set; }
    public string? Citizenship { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Designation { get; set; }
    public string? Department { get; set; }
    public string? Status { get; set; }
}

public class CreateUpdateEmployeeDto
{
    [Required]
    public Guid CompanyId { get; set; }

    [Required]
    [StringLength(128)]
    public string FirstName { get; set; } = null!;

    [StringLength(128)]
    public string? LastName { get; set; }

    public DateTime? DateOfBirth { get; set; }
    public DateTime? DateOfJoining { get; set; }

    [StringLength(20)]
    public string? Phone { get; set; }

    [StringLength(200)]
    [EmailAddress]
    public string? Email { get; set; }

    [StringLength(128)]
    public string? Designation { get; set; }

    [StringLength(128)]
    public string? Department { get; set; }

    [StringLength(100)]
    public string? EpfNumber { get; set; }

    [StringLength(100)]
    public string? SocsoNumber { get; set; }

    [StringLength(100)]
    public string? TaxNumber { get; set; }
}
