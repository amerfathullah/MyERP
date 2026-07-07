using System;
using Volo.Abp.Application.Dtos;

namespace MyERP.Core;

public class CompanyDto : FullAuditedEntityDto<Guid>
{
    public string Name { get; set; } = null!;
    public string? ShortName { get; set; }
    public string? TaxId { get; set; }
    public string? RegistrationNumber { get; set; }
    public string? SstRegistrationNumber { get; set; }
    public string? MsicCode { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Website { get; set; }
    public string? Address { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? PostalCode { get; set; }
    public string? Country { get; set; }
    public string CurrencyCode { get; set; } = "MYR";
    public int FiscalYearStartMonth { get; set; }
    public bool IsActive { get; set; }
}
