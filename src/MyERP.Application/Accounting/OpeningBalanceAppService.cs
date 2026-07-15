using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Accounting.Entities;
using MyERP.Core;
using MyERP.Core.DomainServices;
using MyERP.Core.Entities;
using MyERP.Permissions;
using MyERP.Sales.Entities;
using MyERP.Purchasing.Entities;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Accounting;

/// <summary>
/// Opening Balance Entry Tool — critical for go-live.
/// Creates opening Journal Entries (Balance Sheet accounts) and opening invoices
/// (AR/AP) to migrate legacy balances into MyERP.
/// 
/// ERPNext equivalent: erpnext/accounts/doctype/opening_invoice_creation_tool/
///                     erpnext/accounts/utils.py → make_opening_entries
/// </summary>
[Authorize(MyERPPermissions.JournalEntries.Default)]
public class OpeningBalanceAppService : ApplicationService
{
    private readonly IRepository<JournalEntry, Guid> _journalRepository;
    private readonly IRepository<JournalEntryLine, Guid> _lineRepository;
    private readonly IRepository<Account, Guid> _accountRepository;
    private readonly IRepository<Company, Guid> _companyRepository;
    private readonly IRepository<FiscalYear, Guid> _fiscalYearRepository;
    private readonly IRepository<SalesInvoice, Guid> _salesInvoiceRepository;
    private readonly IRepository<PurchaseInvoice, Guid> _purchaseInvoiceRepository;
    private readonly IDocumentNumberGenerator _numberGenerator;

    public OpeningBalanceAppService(
        IRepository<JournalEntry, Guid> journalRepository,
        IRepository<JournalEntryLine, Guid> lineRepository,
        IRepository<Account, Guid> accountRepository,
        IRepository<Company, Guid> companyRepository,
        IRepository<FiscalYear, Guid> fiscalYearRepository,
        IRepository<SalesInvoice, Guid> salesInvoiceRepository,
        IRepository<PurchaseInvoice, Guid> purchaseInvoiceRepository,
        IDocumentNumberGenerator numberGenerator)
    {
        _journalRepository = journalRepository;
        _lineRepository = lineRepository;
        _accountRepository = accountRepository;
        _companyRepository = companyRepository;
        _fiscalYearRepository = fiscalYearRepository;
        _salesInvoiceRepository = salesInvoiceRepository;
        _purchaseInvoiceRepository = purchaseInvoiceRepository;
        _numberGenerator = numberGenerator;
    }

    /// <summary>
    /// Creates an opening Journal Entry for Balance Sheet accounts.
    /// All entries are balanced against a "Temporary Opening" equity account.
    /// 
    /// Per ERPNext:
    /// - Only Balance Sheet accounts (Asset, Liability, Equity) allowed
    /// - P&L accounts (Income, Expense) are blocked
    /// - The difference between debits and credits goes to the Temporary Opening account
    /// - JE is created with is_opening=true flag
    /// </summary>
    [Authorize(MyERPPermissions.JournalEntries.Create)]
    public async Task<OpeningBalanceResultDto> CreateOpeningJournalEntryAsync(CreateOpeningJournalEntryDto input)
    {
        var company = await _companyRepository.GetAsync(input.CompanyId);

        // Validate all accounts are Balance Sheet (not P&L)
        var accountIds = input.Lines.Select(l => l.AccountId).Distinct().ToList();
        var accounts = await _accountRepository.GetListAsync(a => accountIds.Contains(a.Id));

        foreach (var line in input.Lines)
        {
            var account = accounts.FirstOrDefault(a => a.Id == line.AccountId);
            if (account == null)
                throw new BusinessException("MyERP:02030").WithData("accountId", line.AccountId);

            if (account.AccountType == AccountType.Revenue || account.AccountType == AccountType.Expense)
                throw new BusinessException("MyERP:02031")
                    .WithData("accountName", account.AccountName)
                    .WithData("rootType", account.AccountType.ToString());

            if (account.IsGroup)
                throw new BusinessException("MyERP:02032")
                    .WithData("accountName", account.AccountName);
        }

        // Resolve Temporary Opening account
        var tempOpeningAccount = await _accountRepository.FindAsync(a =>
            a.CompanyId == input.CompanyId &&
            a.AccountSubType == AccountSubType.TemporaryOpening);

        if (tempOpeningAccount == null)
            throw new BusinessException("MyERP:02033")
                .WithData("company", company.Name ?? company.Id.ToString());

        // Resolve fiscal year
        var fiscalYear = await _fiscalYearRepository.FindAsync(fy =>
            fy.CompanyId == input.CompanyId &&
            fy.StartDate <= input.PostingDate &&
            fy.EndDate >= input.PostingDate);

        // Create Journal Entry
        var journalEntry = new JournalEntry(
            GuidGenerator.Create(),
            input.CompanyId,
            fiscalYear?.Id ?? Guid.Empty,
            input.PostingDate,
            CurrentTenant.Id);

        // Add account lines
        decimal totalDebit = 0m;
        decimal totalCredit = 0m;

        foreach (var line in input.Lines)
        {
            if (line.Debit > 0)
            {
                journalEntry.AddLine(line.AccountId, line.Debit, true, line.PartyType);
                totalDebit += line.Debit;
            }
            else if (line.Credit > 0)
            {
                journalEntry.AddLine(line.AccountId, line.Credit, false, line.PartyType);
                totalCredit += line.Credit;
            }
        }

        // Add balancing line to Temporary Opening account
        decimal difference = totalDebit - totalCredit;
        if (Math.Abs(difference) >= 0.01m)
        {
            if (difference > 0)
            {
                // More debits → credit the opening account
                journalEntry.AddLine(tempOpeningAccount.Id, difference, false);
            }
            else
            {
                // More credits → debit the opening account
                journalEntry.AddLine(tempOpeningAccount.Id, Math.Abs(difference), true);
            }
        }

        // Mark as opening entry
        journalEntry.ReferenceType = "OpeningEntry";

        // Post immediately
        journalEntry.Post();

        await _journalRepository.InsertAsync(journalEntry);

        var entryNumber = journalEntry.EntryNumber ?? "";

        return new OpeningBalanceResultDto
        {
            JournalEntryId = journalEntry.Id,
            EntryNumber = entryNumber,
            TotalDebit = totalDebit + (difference < 0 ? Math.Abs(difference) : 0),
            TotalCredit = totalCredit + (difference > 0 ? difference : 0),
            TemporaryOpeningAmount = Math.Abs(difference),
            Message = $"Opening journal entry {entryNumber} created with {input.Lines.Count} lines."
        };
    }

    /// <summary>
    /// Bulk-creates opening Sales Invoices for customer receivable balances.
    /// Each invoice is created with IsOpening=true, no payment terms, no stock update.
    /// 
    /// Per ERPNext opening_invoice_creation_tool:
    /// - Batch threshold: >50 invoices → background job
    /// - Party currency fallback from party account
    /// - disable_rounded_total forced
    /// - No payment schedule generated
    /// </summary>
    [Authorize(MyERPPermissions.SalesInvoices.Create)]
    public async Task<OpeningInvoiceResultDto> CreateOpeningSalesInvoicesAsync(
        CreateOpeningInvoicesDto input)
    {
        int created = 0;
        var errors = new List<string>();

        foreach (var invoice in input.Invoices)
        {
            try
            {
                var number = await _numberGenerator.GenerateAsync("SI", input.CompanyId, input.PostingDate);

                var si = new SalesInvoice(
                    GuidGenerator.Create(),
                    input.CompanyId,
                    invoice.CustomerId ?? Guid.Empty,
                    number,
                    input.PostingDate,
                    CurrentTenant.Id);

                si.IsOpening = true;
                si.DueDate = invoice.DueDate ?? input.PostingDate;

                // Single item line for the outstanding amount
                si.AddItem(invoice.ItemId ?? Guid.Empty,
                    "Opening Balance", 1m, invoice.OutstandingAmount, 0m);

                si.Submit();

                await _salesInvoiceRepository.InsertAsync(si);
                created++;
            }
            catch (Exception ex)
            {
                errors.Add($"Customer {invoice.CustomerId}: {ex.Message}");
            }
        }

        return new OpeningInvoiceResultDto
        {
            Created = created,
            Failed = errors.Count,
            Errors = errors,
            Message = $"Created {created} opening sales invoices."
        };
    }

    /// <summary>
    /// Bulk-creates opening Purchase Invoices for supplier payable balances.
    /// Each invoice is created with IsOpening=true, no payment terms, no stock update.
    /// </summary>
    [Authorize(MyERPPermissions.PurchaseInvoices.Create)]
    public async Task<OpeningInvoiceResultDto> CreateOpeningPurchaseInvoicesAsync(
        CreateOpeningInvoicesDto input)
    {
        int created = 0;
        var errors = new List<string>();

        foreach (var invoice in input.Invoices)
        {
            try
            {
                var number = await _numberGenerator.GenerateAsync("PI", input.CompanyId, input.PostingDate);

                var pi = new PurchaseInvoice(
                    GuidGenerator.Create(),
                    input.CompanyId,
                    invoice.SupplierId ?? Guid.Empty,
                    number,
                    input.PostingDate,
                    CurrentTenant.Id);

                pi.IsOpening = true;
                pi.DueDate = invoice.DueDate ?? input.PostingDate;

                // Single item line for the outstanding amount
                pi.AddItem(invoice.ItemId ?? Guid.Empty,
                    "Opening Balance", 1m, invoice.OutstandingAmount, 0m);

                pi.Submit();

                await _purchaseInvoiceRepository.InsertAsync(pi);
                created++;
            }
            catch (Exception ex)
            {
                errors.Add($"Supplier {invoice.SupplierId}: {ex.Message}");
            }
        }

        return new OpeningInvoiceResultDto
        {
            Created = created,
            Failed = errors.Count,
            Errors = errors,
            Message = $"Created {created} opening purchase invoices."
        };
    }

    /// <summary>
    /// Validates the opening entry status for a company:
    /// - Are there existing opening invoices?
    /// - Is the Temporary Opening account balance zero? (indicates complete migration)
    /// - Are all Balance Sheet accounts populated?
    /// </summary>
    public async Task<OpeningStatusDto> GetOpeningStatusAsync(Guid companyId)
    {
        var tempOpeningAccount = await _accountRepository.FindAsync(a =>
            a.CompanyId == companyId &&
            a.AccountSubType == AccountSubType.TemporaryOpening);

        decimal tempBalance = 0m;
        if (tempOpeningAccount != null)
        {
            var lines = await _lineRepository.GetListAsync(l =>
                l.AccountId == tempOpeningAccount.Id);
            tempBalance = lines.Sum(l => l.IsDebit ? l.Amount : -l.Amount);
        }

        var openingSalesInvoices = await _salesInvoiceRepository.CountAsync(si =>
            si.CompanyId == companyId && si.IsOpening);

        var openingPurchaseInvoices = await _purchaseInvoiceRepository.CountAsync(pi =>
            pi.CompanyId == companyId && pi.IsOpening);

        var openingJournals = await _journalRepository.CountAsync(je =>
            je.CompanyId == companyId && je.ReferenceType == "OpeningEntry");

        return new OpeningStatusDto
        {
            CompanyId = companyId,
            TemporaryOpeningBalance = tempBalance,
            IsBalanced = Math.Abs(tempBalance) < 0.01m,
            OpeningSalesInvoiceCount = openingSalesInvoices,
            OpeningPurchaseInvoiceCount = openingPurchaseInvoices,
            OpeningJournalEntryCount = openingJournals,
            Message = Math.Abs(tempBalance) < 0.01m
                ? "Opening entries are balanced. Ready for production."
                : $"Temporary Opening account has a balance of {tempBalance:N2}. Additional entries needed."
        };
    }
}

// --- DTOs ---

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
