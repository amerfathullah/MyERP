using System;
using MyERP.Core;
using Volo.Abp.Application.Dtos;

namespace MyERP.Accounting;

public class BankGuaranteeDto : FullAuditedEntityDto<Guid>
{
    public Guid CompanyId { get; set; }
    public BankGuaranteeType BgType { get; set; }

    public string? ReferenceDocType { get; set; }
    public Guid? ReferenceDocId { get; set; }
    public string? ReferenceDocName { get; set; }

    public Guid? CustomerId { get; set; }
    public string? CustomerName { get; set; }

    public Guid? SupplierId { get; set; }
    public string? SupplierName { get; set; }

    public Guid? ProjectId { get; set; }
    public string? ProjectName { get; set; }

    public decimal Amount { get; set; }
    public DateTime StartDate { get; set; }
    public int ValidityDays { get; set; }
    public DateTime? EndDate { get; set; }

    public string? Bank { get; set; }
    public Guid? BankAccountId { get; set; }
    public string? BankAccountNumber { get; set; }
    public string? Account { get; set; }
    public string? Iban { get; set; }
    public string? BranchCode { get; set; }
    public string? SwiftNumber { get; set; }

    public string? BankGuaranteeNumber { get; set; }
    public string? NameOfBeneficiary { get; set; }
    public decimal MarginMoney { get; set; }
    public decimal Charges { get; set; }
    public string? FixedDepositNumber { get; set; }
    public string? ClausesAndConditions { get; set; }

    public DocumentStatus Status { get; set; }
}
