using System;
using System.ComponentModel.DataAnnotations;

namespace MyERP.Accounting;

public class CreateUpdateBankGuaranteeDto
{
    [Required]
    public Guid CompanyId { get; set; }

    [Required]
    public BankGuaranteeType BgType { get; set; } = BankGuaranteeType.Receiving;

    [StringLength(BankGuaranteeConsts.MaxReferenceDocTypeLength)]
    public string? ReferenceDocType { get; set; }

    public Guid? ReferenceDocId { get; set; }

    [StringLength(BankGuaranteeConsts.MaxReferenceDocNameLength)]
    public string? ReferenceDocName { get; set; }

    public Guid? CustomerId { get; set; }

    [StringLength(BankGuaranteeConsts.MaxBeneficiaryNameLength)]
    public string? CustomerName { get; set; }

    public Guid? SupplierId { get; set; }

    [StringLength(BankGuaranteeConsts.MaxBeneficiaryNameLength)]
    public string? SupplierName { get; set; }

    public Guid? ProjectId { get; set; }

    [StringLength(BankGuaranteeConsts.MaxBeneficiaryNameLength)]
    public string? ProjectName { get; set; }

    [Range(0.01, double.MaxValue)]
    public decimal Amount { get; set; }

    [Required]
    public DateTime StartDate { get; set; } = DateTime.UtcNow.Date;

    [Range(0, 3650)]
    public int ValidityDays { get; set; }

    [StringLength(BankGuaranteeConsts.MaxBankNameLength)]
    public string? Bank { get; set; }

    public Guid? BankAccountId { get; set; }

    [StringLength(BankGuaranteeConsts.MaxAccountNumberLength)]
    public string? BankAccountNumber { get; set; }

    [StringLength(BankGuaranteeConsts.MaxAccountNumberLength)]
    public string? Account { get; set; }

    [StringLength(BankGuaranteeConsts.MaxIbanLength)]
    public string? Iban { get; set; }

    [StringLength(BankGuaranteeConsts.MaxBranchCodeLength)]
    public string? BranchCode { get; set; }

    [StringLength(BankGuaranteeConsts.MaxSwiftNumberLength)]
    public string? SwiftNumber { get; set; }

    [StringLength(BankGuaranteeConsts.MaxGuaranteeNumberLength)]
    public string? BankGuaranteeNumber { get; set; }

    [StringLength(BankGuaranteeConsts.MaxBeneficiaryNameLength)]
    public string? NameOfBeneficiary { get; set; }

    [Range(0, double.MaxValue)]
    public decimal MarginMoney { get; set; }

    [Range(0, double.MaxValue)]
    public decimal Charges { get; set; }

    [StringLength(BankGuaranteeConsts.MaxFixedDepositNumberLength)]
    public string? FixedDepositNumber { get; set; }

    [StringLength(BankGuaranteeConsts.MaxClausesLength)]
    public string? ClausesAndConditions { get; set; }
}
