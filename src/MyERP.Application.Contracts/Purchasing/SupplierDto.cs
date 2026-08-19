using System;
using Volo.Abp.Application.Dtos;

namespace MyERP.Purchasing;

public class SupplierDto : FullAuditedEntityDto<Guid>
{
    public Guid CompanyId { get; set; }
    public string Name { get; set; } = null!;
    public string? SupplierCode { get; set; }
    public string? Tin { get; set; }
    public string? RegistrationNumber { get; set; }
    public string? SstRegistrationNumber { get; set; }
    public string? IdType { get; set; }
    public string? IdValue { get; set; }
    public string? ContactPerson { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Website { get; set; }
    public string? Address { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? PostalCode { get; set; }
    public string? Country { get; set; }
    public Guid? DefaultPayableAccountId { get; set; }
    public bool IsActive { get; set; }
    public SupplierHoldType HoldType { get; set; }
    public bool PreventPurchaseOrders { get; set; }
    public bool PreventRfqs { get; set; }
    public Guid? RepresentsCompanyId { get; set; }
    public Guid? SupplierGroupId { get; set; }
    public string? TaxWithholdingCategory { get; set; }
    public Guid? DefaultPaymentTermsTemplateId { get; set; }
    public bool RestrictToCompanies { get; set; }
}
