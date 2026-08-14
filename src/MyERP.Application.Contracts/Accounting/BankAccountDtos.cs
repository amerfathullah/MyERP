using System;
using Volo.Abp.Application.Dtos;

namespace MyERP.Accounting;

public class BankAccountDto : EntityDto<Guid>
{
    public Guid CompanyId { get; set; }
    public string AccountName { get; set; } = null!;
    public Guid AccountId { get; set; }
    public string BankName { get; set; } = null!;
    public string? BankAccountNo { get; set; }
    public string? Iban { get; set; }
    public string? SwiftCode { get; set; }
    public string? BranchCode { get; set; }
    public bool IsCompanyAccount { get; set; }
    public bool IsDefault { get; set; }
    public string? PartyType { get; set; }
    public Guid? PartyId { get; set; }
    public string CurrencyCode { get; set; } = null!;
    public bool IsDisabled { get; set; }
    public bool IsCreditCard { get; set; }
    public string? IntegrationId { get; set; }
    public DateTime? LastIntegrationDate { get; set; }
}

public class CreateUpdateBankAccountDto
{
    public Guid CompanyId { get; set; }
    public string AccountName { get; set; } = null!;
    public Guid AccountId { get; set; }
    public string BankName { get; set; } = null!;
    public string? BankAccountNo { get; set; }
    public string? Iban { get; set; }
    public string? SwiftCode { get; set; }
    public string? BranchCode { get; set; }
    public bool IsCompanyAccount { get; set; } = true;
    public string? PartyType { get; set; }
    public Guid? PartyId { get; set; }
    public string CurrencyCode { get; set; } = "MYR";
    public bool IsCreditCard { get; set; }
}

public class GetBankAccountListDto : PagedAndSortedResultRequestDto
{
    public Guid? CompanyId { get; set; }
    public string? Filter { get; set; }
    public bool? IsCompanyAccount { get; set; }
}
