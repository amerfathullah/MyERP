using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace MyERP.Accounting;

/// <summary>
/// Bank Clearance — marks Payment Entries and Journal Entries (Bank/Contra/Credit Card type)
/// as cleared against a bank statement by setting their ClearanceDate.
/// Per ERPNext accounts/doctype/bank_clearance/bank_clearance.py.
/// </summary>
public interface IBankClearanceAppService : IApplicationService
{
    Task<List<BankClearanceEntryDto>> GetEntriesAsync(GetBankClearanceEntriesInput input);

    Task<BulkClearanceResultDto> SetClearanceDateAsync(SetClearanceDateDto input);
}

public class GetBankClearanceEntriesInput
{
    /// <summary>GL Account (Bank/Cash type) to list entries for.</summary>
    [Required]
    public Guid BankAccountId { get; set; }

    [Required]
    public Guid CompanyId { get; set; }

    [Required]
    public DateTime FromDate { get; set; }

    [Required]
    public DateTime ToDate { get; set; }

    /// <summary>When false (default), only uncleared entries (ClearanceDate == null) are returned.</summary>
    public bool IncludeCleared { get; set; }
}

public class BankClearanceEntryDto
{
    /// <summary>"PaymentEntry" or "JournalEntry".</summary>
    public string DocumentType { get; set; } = null!;
    public Guid DocumentId { get; set; }
    public string DocumentNumber { get; set; } = null!;
    public DateTime PostingDate { get; set; }
    public decimal Debit { get; set; }
    public decimal Credit { get; set; }
    public string? ReferenceNumber { get; set; }
    public DateTime? ClearanceDate { get; set; }
}

public class SetClearanceDateDto
{
    [Required]
    [MinLength(1)]
    public List<BankClearanceDocRefDto> Entries { get; set; } = new();

    /// <summary>The clearance date to apply. Null clears (un-reconciles) the selected entries.</summary>
    public DateTime? ClearanceDate { get; set; }
}

public class BankClearanceDocRefDto
{
    /// <summary>"PaymentEntry" or "JournalEntry".</summary>
    [Required]
    public string DocumentType { get; set; } = null!;

    [Required]
    public Guid DocumentId { get; set; }
}

public class BulkClearanceResultDto
{
    public int UpdatedCount { get; set; }
}
