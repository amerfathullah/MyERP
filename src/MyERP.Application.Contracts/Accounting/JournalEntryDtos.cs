using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using MyERP.Core;
using Volo.Abp.Application.Dtos;

namespace MyERP.Accounting;

public class JournalEntryDto : EntityDto<Guid>
{
    public Guid CompanyId { get; set; }
    public Guid FiscalYearId { get; set; }
    public string? EntryNumber { get; set; }
    public DateTime PostingDate { get; set; }
    public JournalEntryVoucherType VoucherType { get; set; }
    public string? ReferenceType { get; set; }
    public Guid? ReferenceId { get; set; }
    public string? ReferenceNumber { get; set; }
    public string? Narration { get; set; }
    public string Status { get; set; } = null!;
    public decimal TotalDebit { get; set; }
    public decimal TotalCredit { get; set; }
    public decimal Difference { get; set; }

    /// <summary>Entry has lines on accounts in another currency (ERPNext multi_currency).</summary>
    public bool IsMultiCurrency { get; set; }
    public List<JournalEntryLineDto> Lines { get; set; } = new();
}

public class JournalEntryLineDto
{
    public Guid Id { get; set; }
    public Guid AccountId { get; set; }
    public string? AccountName { get; set; }
    public string? AccountCode { get; set; }
    public decimal Amount { get; set; }
    public bool IsDebit { get; set; }
    public string? Description { get; set; }
    public Guid? PartyId { get; set; }
    public string? PartyType { get; set; }
    public Guid? CostCenterId { get; set; }
    public Guid? ProjectId { get; set; }

    /// <summary>Currency of the line's account.</summary>
    public string? AccountCurrency { get; set; }

    /// <summary>Amount in the account's currency; equals Amount on a company currency account.</summary>
    public decimal AmountInAccountCurrency { get; set; }

    /// <summary>Account currency to company currency rate; 1 on a company currency account.</summary>
    public decimal ExchangeRate { get; set; } = 1m;
}

public class CreateJournalEntryDto
{
    [Required] public Guid CompanyId { get; set; }
    [Required] public Guid FiscalYearId { get; set; }
    [Required] public DateTime PostingDate { get; set; }
    public JournalEntryVoucherType VoucherType { get; set; } = JournalEntryVoucherType.JournalEntry;
    public string? ReferenceType { get; set; }
    public Guid? ReferenceId { get; set; }
    [StringLength(100)] public string? ReferenceNumber { get; set; }
    [StringLength(500)] public string? Narration { get; set; }

    /// <summary>
    /// Must be set to use accounts in a currency other than the company's
    /// (ERPNext Journal Entry multi_currency).
    /// </summary>
    public bool IsMultiCurrency { get; set; }
    [Required][MinLength(2)] public List<CreateJournalEntryLineDto> Lines { get; set; } = new();
}

public class CreateJournalEntryLineDto
{
    [Required] public Guid AccountId { get; set; }

    /// <summary>
    /// Amount in company currency. On a foreign currency account it is derived from
    /// AmountInAccountCurrency x ExchangeRate when that amount is given.
    /// </summary>
    [Range(0, double.MaxValue)] public decimal Amount { get; set; }

    /// <summary>
    /// Amount in the account's currency (ERPNext debit/credit_in_account_currency). Only meaningful
    /// on a foreign currency account, where it is the amount actually entered.
    /// </summary>
    [Range(0, double.MaxValue)] public decimal? AmountInAccountCurrency { get; set; }

    /// <summary>
    /// Account currency to company currency rate. Left empty (or 1) on a foreign currency account,
    /// the rate for the posting date is looked up.
    /// </summary>
    [Range(0, double.MaxValue)] public decimal? ExchangeRate { get; set; }
    [Required] public bool IsDebit { get; set; }
    [StringLength(500)] public string? Description { get; set; }
    public Guid? PartyId { get; set; }
    [StringLength(PartyTypeConsts.MaxPartyTypeNameLength)] public string? PartyType { get; set; }
    public Guid? CostCenterId { get; set; }
    public Guid? ProjectId { get; set; }
}

/// <summary>
/// What the Journal Entry form needs when an account is picked on a row
/// (ERPNext get_account_details_and_party_type).
/// </summary>
public class JournalEntryAccountDetailsDto
{
    public string AccountCurrency { get; set; } = null!;
    public string CompanyCurrency { get; set; } = null!;

    /// <summary>Account currency to company currency rate on the posting date; 1 for a company currency account.</summary>
    public decimal ExchangeRate { get; set; } = 1m;

    /// <summary>"Customer" for a receivable account, "Supplier" for a payable one, otherwise null.</summary>
    public string? PartyType { get; set; }
}
