using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace MyERP.Accounting;

public class CreateOpeningJournalEntryDto
{
    [Required]
    public Guid CompanyId { get; set; }

    [Required]
    public DateTime PostingDate { get; set; }

    [Required]
    public List<OpeningJournalLineDto> Lines { get; set; } = new();

    public string? Remarks { get; set; }
}

public class OpeningJournalLineDto
{
    [Required]
    public Guid AccountId { get; set; }

    public decimal Debit { get; set; }
    public decimal Credit { get; set; }

    public string? PartyType { get; set; }
    public Guid? PartyId { get; set; }
}

public class CreateOpeningInvoicesDto
{
    [Required]
    public Guid CompanyId { get; set; }

    [Required]
    public DateTime PostingDate { get; set; }

    public string? Currency { get; set; }

    [Required]
    public List<OpeningInvoiceLineDto> Invoices { get; set; } = new();
}

public class OpeningInvoiceLineDto
{
    public Guid? CustomerId { get; set; }
    public Guid? SupplierId { get; set; }
    public Guid? ItemId { get; set; }

    [Required]
    public decimal OutstandingAmount { get; set; }

    public DateTime? DueDate { get; set; }
}

public class OpeningBalanceResultDto
{
    public Guid JournalEntryId { get; set; }
    public string EntryNumber { get; set; } = "";
    public decimal TotalDebit { get; set; }
    public decimal TotalCredit { get; set; }
    public decimal TemporaryOpeningAmount { get; set; }
    public string Message { get; set; } = "";
}

public class OpeningInvoiceResultDto
{
    public int Created { get; set; }
    public int Failed { get; set; }
    public List<string> Errors { get; set; } = new();
    public string Message { get; set; } = "";
}

public class OpeningStatusDto
{
    public Guid CompanyId { get; set; }
    public decimal TemporaryOpeningBalance { get; set; }
    public bool IsBalanced { get; set; }
    public int OpeningSalesInvoiceCount { get; set; }
    public int OpeningPurchaseInvoiceCount { get; set; }
    public int OpeningJournalEntryCount { get; set; }
    public string Message { get; set; } = "";
}
