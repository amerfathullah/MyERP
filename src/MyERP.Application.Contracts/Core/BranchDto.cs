using System;
using Volo.Abp.Application.Dtos;

namespace MyERP.Core;

public class BranchDto : FullAuditedEntityDto<Guid>
{
    public Guid CompanyId { get; set; }
    public string Name { get; set; } = null!;
    public string? Code { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Address { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? PostalCode { get; set; }
    public string? Country { get; set; }
    public bool IsActive { get; set; }
    public bool IsHeadquarters { get; set; }
}
