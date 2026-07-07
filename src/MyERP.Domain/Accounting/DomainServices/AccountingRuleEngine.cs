using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Accounting.Entities;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;

namespace MyERP.Accounting.DomainServices;

/// <summary>
/// Interface for documents that can be posted to the general ledger.
/// </summary>
public interface IAccountableDocument
{
    Guid Id { get; }
    Guid CompanyId { get; }
    string DocumentType { get; }
    decimal NetTotal { get; }
    decimal GrandTotal { get; }
    decimal TaxAmount { get; }
    Guid? CustomerId { get; }
    Guid? SupplierId { get; }
    DateTime PostingDate { get; }
}

/// <summary>
/// Accounting Rules Engine — generates journal entries from documents using configurable rules.
/// This is the core of the ERP's accounting system. NEVER hardcode GL postings.
/// </summary>
public class AccountingRuleEngine : DomainService
{
    private readonly IRepository<AccountingRule, Guid> _ruleRepository;
    private readonly IRepository<FiscalYear, Guid> _fiscalYearRepository;

    public AccountingRuleEngine(
        IRepository<AccountingRule, Guid> ruleRepository,
        IRepository<FiscalYear, Guid> fiscalYearRepository)
    {
        _ruleRepository = ruleRepository;
        _fiscalYearRepository = fiscalYearRepository;
    }

    /// <summary>
    /// Generate a balanced journal entry from a source document using configured accounting rules.
    /// </summary>
    public async Task<JournalEntry> PostDocumentAsync(IAccountableDocument document)
    {
        var rules = await _ruleRepository.GetListAsync(r =>
            r.CompanyId == document.CompanyId &&
            r.DocumentType == document.DocumentType &&
            r.IsActive);

        if (!rules.Any())
        {
            throw new BusinessException(MyERPDomainErrorCodes.UnbalancedJournalEntry)
                .WithData("documentType", document.DocumentType);
        }

        var fiscalYear = await GetFiscalYearAsync(document.CompanyId, document.PostingDate);

        var journal = new JournalEntry(
            GuidGenerator.Create(),
            document.CompanyId,
            fiscalYear.Id,
            document.PostingDate);

        journal.ReferenceType = document.DocumentType;
        journal.ReferenceId = document.Id;

        foreach (var rule in rules.OrderBy(r => r.SortOrder))
        {
            var amount = ResolveAmount(rule.AmountSource, document);
            if (amount <= 0) continue;

            var accountId = ResolveAccountId(rule, document);
            journal.AddLine(accountId, amount, rule.IsDebit);
        }

        // Double-entry validation — will throw if unbalanced
        journal.Validate();
        journal.Post();

        return journal;
    }

    private decimal ResolveAmount(AmountSource source, IAccountableDocument document)
    {
        return source switch
        {
            AmountSource.NetTotal => document.NetTotal,
            AmountSource.GrandTotal => document.GrandTotal,
            AmountSource.TaxAmount => document.TaxAmount,
            _ => 0m
        };
    }

    private Guid ResolveAccountId(AccountingRule rule, IAccountableDocument document)
    {
        // For now, use FixedAccount. Future: resolve from customer/supplier/item defaults.
        if (rule.FixedAccountId == null)
        {
            throw new BusinessException(MyERPDomainErrorCodes.AccountIsGroup)
                .WithData("ruleName", rule.Name);
        }

        return rule.FixedAccountId.Value;
    }

    private async Task<FiscalYear> GetFiscalYearAsync(Guid companyId, DateTime date)
    {
        var fiscalYear = await _fiscalYearRepository.FindAsync(fy =>
            fy.CompanyId == companyId &&
            !fy.IsClosed &&
            fy.StartDate <= date &&
            fy.EndDate >= date);

        if (fiscalYear == null)
        {
            throw new BusinessException(MyERPDomainErrorCodes.FiscalYearClosed)
                .WithData("date", date);
        }

        return fiscalYear;
    }
}
