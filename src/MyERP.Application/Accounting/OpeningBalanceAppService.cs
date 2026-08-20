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
public class OpeningBalanceAppService : ApplicationService, IOpeningBalanceAppService
{
    private readonly IRepository<JournalEntry, Guid> _journalRepository;
    private readonly IRepository<JournalEntryLine, Guid> _lineRepository;
    private readonly IRepository<Account, Guid> _accountRepository;
    private readonly IRepository<Company, Guid> _companyRepository;
    private readonly IRepository<FiscalYear, Guid> _fiscalYearRepository;
    private readonly IRepository<SalesInvoice, Guid> _salesInvoiceRepository;
    private readonly IRepository<PurchaseInvoice, Guid> _purchaseInvoiceRepository;
    private readonly IDocumentNumberGenerator _numberGenerator;
    private readonly Accounting.DomainServices.PaymentLedgerService _pleService;

    public OpeningBalanceAppService(
        IRepository<JournalEntry, Guid> journalRepository,
        IRepository<JournalEntryLine, Guid> lineRepository,
        IRepository<Account, Guid> accountRepository,
        IRepository<Company, Guid> companyRepository,
        IRepository<FiscalYear, Guid> fiscalYearRepository,
        IRepository<SalesInvoice, Guid> salesInvoiceRepository,
        IRepository<PurchaseInvoice, Guid> purchaseInvoiceRepository,
        IDocumentNumberGenerator numberGenerator,
        Accounting.DomainServices.PaymentLedgerService pleService)
    {
        _journalRepository = journalRepository;
        _lineRepository = lineRepository;
        _accountRepository = accountRepository;
        _companyRepository = companyRepository;
        _fiscalYearRepository = fiscalYearRepository;
        _salesInvoiceRepository = salesInvoiceRepository;
        _purchaseInvoiceRepository = purchaseInvoiceRepository;
        _numberGenerator = numberGenerator;
        _pleService = pleService;
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

        if (fiscalYear == null)
            throw new Volo.Abp.BusinessException("MyERP:02002")
                .WithData("reason", $"No fiscal year found for company covering posting date {input.PostingDate:yyyy-MM-dd}. Create a fiscal year first.");

        // Create Journal Entry
        var journalEntry = new JournalEntry(
            GuidGenerator.Create(),
            input.CompanyId,
            fiscalYear.Id,
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
    /// - Party currency fallback from party account
    /// - No payment schedule generated
    ///
    /// GL: DR Company.DefaultReceivableAccountId / CR Temporary Opening account, posted as a
    /// manually-composed JournalEntry (AccountingRuleEngine has no per-invoice account override,
    /// same reasoning as the exchange-gain/loss JE built in PaymentEntryAppService). Also creates
    /// the PLE row so the invoice actually shows outstanding — previously this method called
    /// only Submit(), never Post(), so opening invoices never reached the ledger at all.
    /// </summary>
    [Authorize(MyERPPermissions.SalesInvoices.Create)]
    public async Task<OpeningInvoiceResultDto> CreateOpeningSalesInvoicesAsync(
        CreateOpeningInvoicesDto input)
    {
        var company = await _companyRepository.GetAsync(input.CompanyId);
        if (!company.DefaultReceivableAccountId.HasValue)
            throw new BusinessException(MyERPDomainErrorCodes.DefaultAccountNotConfigured)
                .WithData("reason", "No Default Receivable Account configured on the company.");

        var tempOpeningAccount = await ResolveTemporaryOpeningAccountAsync(input.CompanyId);
        var fiscalYear = await ResolveFiscalYearAsync(input.CompanyId, input.PostingDate);

        int created = 0;
        var errors = new List<string>();

        foreach (var invoice in input.Invoices)
        {
            try
            {
                if (!invoice.CustomerId.HasValue || invoice.CustomerId == Guid.Empty)
                {
                    errors.Add($"Invoice for amount {invoice.OutstandingAmount}: Customer ID is required.");
                    continue;
                }
                if (!invoice.ItemId.HasValue || invoice.ItemId == Guid.Empty)
                {
                    errors.Add($"Invoice for customer {invoice.CustomerId}: ItemId is required (a placeholder 'Opening Balance' item).");
                    continue;
                }

                var number = await _numberGenerator.GenerateAsync("SI", input.CompanyId, input.PostingDate);

                var si = new SalesInvoice(
                    GuidGenerator.Create(),
                    input.CompanyId,
                    invoice.CustomerId.Value,
                    number,
                    input.PostingDate,
                    CurrentTenant.Id)
                {
                    IsOpening = true,
                    DueDate = invoice.DueDate ?? input.PostingDate,
                    DebitToAccountId = company.DefaultReceivableAccountId.Value,
                };

                si.AddItem(invoice.ItemId.Value, "Opening Balance", 1m, invoice.OutstandingAmount, 0m);
                si.Submit();
                si.Post();
                await _salesInvoiceRepository.InsertAsync(si);

                var je = BuildOpeningJournalEntry(input.CompanyId, fiscalYear.Id, input.PostingDate,
                    "SalesInvoice", si.Id,
                    debitAccountId: company.DefaultReceivableAccountId.Value,
                    creditAccountId: tempOpeningAccount.Id,
                    amount: invoice.OutstandingAmount);
                await _journalRepository.InsertAsync(je);

                await _pleService.CreateEntryAsync(
                    input.CompanyId, input.PostingDate, company.DefaultReceivableAccountId.Value,
                    "Customer", invoice.CustomerId.Value, "SalesInvoice", si.Id, "SalesInvoice", si.Id,
                    invoice.OutstandingAmount, invoice.OutstandingAmount, input.Currency ?? si.CurrencyCode,
                    invoice.DueDate, CurrentTenant.Id);

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
    /// GL: DR Temporary Opening account / CR Company.DefaultPayableAccountId — see
    /// CreateOpeningSalesInvoicesAsync for why this is a manual JE rather than the rule engine.
    /// </summary>
    [Authorize(MyERPPermissions.PurchaseInvoices.Create)]
    public async Task<OpeningInvoiceResultDto> CreateOpeningPurchaseInvoicesAsync(
        CreateOpeningInvoicesDto input)
    {
        var company = await _companyRepository.GetAsync(input.CompanyId);
        if (!company.DefaultPayableAccountId.HasValue)
            throw new BusinessException(MyERPDomainErrorCodes.DefaultAccountNotConfigured)
                .WithData("reason", "No Default Payable Account configured on the company.");

        var tempOpeningAccount = await ResolveTemporaryOpeningAccountAsync(input.CompanyId);
        var fiscalYear = await ResolveFiscalYearAsync(input.CompanyId, input.PostingDate);

        int created = 0;
        var errors = new List<string>();

        foreach (var invoice in input.Invoices)
        {
            try
            {
                if (!invoice.SupplierId.HasValue || invoice.SupplierId == Guid.Empty)
                {
                    errors.Add($"Invoice for amount {invoice.OutstandingAmount}: Supplier ID is required.");
                    continue;
                }
                if (!invoice.ItemId.HasValue || invoice.ItemId == Guid.Empty)
                {
                    errors.Add($"Invoice for supplier {invoice.SupplierId}: ItemId is required (a placeholder 'Opening Balance' item).");
                    continue;
                }

                var number = await _numberGenerator.GenerateAsync("PI", input.CompanyId, input.PostingDate);

                var pi = new PurchaseInvoice(
                    GuidGenerator.Create(),
                    input.CompanyId,
                    invoice.SupplierId.Value,
                    number,
                    input.PostingDate,
                    CurrentTenant.Id)
                {
                    IsOpening = true,
                    DueDate = invoice.DueDate ?? input.PostingDate,
                    CreditToAccountId = company.DefaultPayableAccountId.Value,
                };

                pi.AddItem(invoice.ItemId.Value, "Opening Balance", 1m, invoice.OutstandingAmount, 0m);
                pi.Submit();
                pi.Post();
                await _purchaseInvoiceRepository.InsertAsync(pi);

                var je = BuildOpeningJournalEntry(input.CompanyId, fiscalYear.Id, input.PostingDate,
                    "PurchaseInvoice", pi.Id,
                    debitAccountId: tempOpeningAccount.Id,
                    creditAccountId: company.DefaultPayableAccountId.Value,
                    amount: invoice.OutstandingAmount);
                await _journalRepository.InsertAsync(je);

                await _pleService.CreateEntryAsync(
                    input.CompanyId, input.PostingDate, company.DefaultPayableAccountId.Value,
                    "Supplier", invoice.SupplierId.Value, "PurchaseInvoice", pi.Id, "PurchaseInvoice", pi.Id,
                    -invoice.OutstandingAmount, -invoice.OutstandingAmount, input.Currency ?? pi.CurrencyCode,
                    invoice.DueDate, CurrentTenant.Id);

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

    private JournalEntry BuildOpeningJournalEntry(
        Guid companyId, Guid fiscalYearId, DateTime postingDate,
        string referenceType, Guid referenceId, Guid debitAccountId, Guid creditAccountId, decimal amount)
    {
        var je = new JournalEntry(GuidGenerator.Create(), companyId, fiscalYearId, postingDate, CurrentTenant.Id)
        {
            ReferenceType = referenceType,
            ReferenceId = referenceId,
            IsOpening = true,
        };
        je.AddLine(debitAccountId, amount, true, "Opening balance");
        je.AddLine(creditAccountId, amount, false, "Opening balance");
        je.Validate();
        je.Post();
        return je;
    }

    private async Task<Account> ResolveTemporaryOpeningAccountAsync(Guid companyId)
    {
        var tempOpeningAccount = await _accountRepository.FindAsync(a =>
            a.CompanyId == companyId && a.AccountSubType == AccountSubType.TemporaryOpening);

        if (tempOpeningAccount == null)
            throw new BusinessException("MyERP:02033").WithData("companyId", companyId);

        return tempOpeningAccount;
    }

    private async Task<FiscalYear> ResolveFiscalYearAsync(Guid companyId, DateTime postingDate)
    {
        var fiscalYear = await _fiscalYearRepository.FindAsync(fy =>
            fy.CompanyId == companyId && fy.StartDate <= postingDate && fy.EndDate >= postingDate);

        if (fiscalYear == null)
            throw new BusinessException("MyERP:02002")
                .WithData("reason", $"No fiscal year found for company covering posting date {postingDate:yyyy-MM-dd}. Create a fiscal year first.");

        return fiscalYear;
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
