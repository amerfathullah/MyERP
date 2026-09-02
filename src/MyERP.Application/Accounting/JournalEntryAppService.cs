using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Accounting.DomainServices;
using MyERP.Accounting.Entities;
using MyERP.Core.DomainServices;
using MyERP.Core.Entities;
using MyERP.Permissions;
using MyERP.Shared;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Accounting;

[Authorize(MyERPPermissions.JournalEntries.Default)]
public class JournalEntryAppService : ApplicationService, IJournalEntryAppService
{
    private readonly IRepository<JournalEntry, Guid> _repository;
    private readonly IDocumentNumberGenerator _numberGenerator;
    private readonly IRepository<Company, Guid> _companyRepository;
    private readonly IRepository<AccountingPeriod, Guid> _periodRepository;
    private readonly IRepository<FiscalYear, Guid> _fiscalYearRepository;

    public JournalEntryAppService(
        IRepository<JournalEntry, Guid> repository,
        IDocumentNumberGenerator numberGenerator,
        IRepository<Company, Guid> companyRepository,
        IRepository<AccountingPeriod, Guid> periodRepository,
        IRepository<FiscalYear, Guid> fiscalYearRepository)
    {
        _repository = repository;
        _numberGenerator = numberGenerator;
        _companyRepository = companyRepository;
        _periodRepository = periodRepository;
        _fiscalYearRepository = fiscalYearRepository;
    }

    public async Task<JournalEntryDto> GetAsync(Guid id)
    {
        var entry = await _repository.GetAsync(id);
        var dto = ObjectMapper.Map<JournalEntry, JournalEntryDto>(entry);

        // Resolve account names
        var accountRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Account, Guid>>();
        var accountIds = dto.Lines.Select(l => l.AccountId).Distinct().ToList();
        var accountQuery = await accountRepo.GetQueryableAsync();
        var accounts = accountQuery.Where(a => accountIds.Contains(a.Id))
            .Select(a => new { a.Id, a.AccountCode, a.AccountName }).ToList()
            .ToDictionary(a => a.Id);

        foreach (var line in dto.Lines)
        {
            if (accounts.TryGetValue(line.AccountId, out var acct))
            {
                line.AccountCode = acct.AccountCode;
                line.AccountName = acct.AccountName;
            }
        }

        return dto;
    }

    public async Task<PagedResultDto<JournalEntryDto>> GetListAsync(CompanyFilteredPagedRequestDto input)
    {
        var query = await _repository.GetQueryableAsync();

        if (input.CompanyId.HasValue)
            query = query.Where(x => x.CompanyId == input.CompanyId.Value);

        if (!string.IsNullOrWhiteSpace(input.Filter))
        {
            var filter = input.Filter;
            query = query.Where(x => x.EntryNumber != null && x.EntryNumber.Contains(filter));
        }

        if (!string.IsNullOrWhiteSpace(input.Status) && Enum.TryParse<Core.DocumentStatus>(input.Status, true, out var status))
            query = query.Where(x => x.Status == status);

        if (input.FromDate.HasValue)
            query = query.Where(x => x.PostingDate >= input.FromDate.Value);

        if (input.ToDate.HasValue)
            query = query.Where(x => x.PostingDate <= input.ToDate.Value);

        var totalCount = query.Count();
        var sorted = SortingHelper.ApplySorting(query, input.Sorting,
            q => q.OrderByDescending(x => x.PostingDate),
            ("entryNumber", x => (object)(x.EntryNumber ?? string.Empty)),
            ("postingDate", x => x.PostingDate),
            ("status", x => x.Status));
        var entries = sorted
            .Skip(input.SkipCount)
            .Take(input.MaxResultCount)
            .ToList();

        return new PagedResultDto<JournalEntryDto>(
            totalCount,
            entries.Select(x => ObjectMapper.Map<JournalEntry, JournalEntryDto>(x)).ToList());
    }

    [Authorize(MyERPPermissions.JournalEntries.Create)]
    public async Task<JournalEntryDto> CreateAsync(CreateJournalEntryDto input)
    {
        var entryNumber = await _numberGenerator.GenerateAsync("JournalEntry", input.CompanyId);

        var entry = new JournalEntry(
            GuidGenerator.Create(),
            input.CompanyId,
            input.FiscalYearId,
            input.PostingDate);

        entry.EntryNumber = entryNumber;
        entry.VoucherType = input.VoucherType;
        entry.ReferenceType = input.ReferenceType;
        entry.ReferenceId = input.ReferenceId;
        entry.ReferenceNumber = input.ReferenceNumber;
        entry.Narration = input.Narration;

        foreach (var line in input.Lines)
        {
            entry.AddLine(line.AccountId, line.Amount, line.IsDebit, line.Description);
        }

        var accountIds = input.Lines.Select(l => l.AccountId).ToArray();
        var companyRestriction = LazyServiceProvider.LazyGetRequiredService<Core.DomainServices.CompanyRestrictionValidationService>();
        await companyRestriction.ValidateTransactionCompanyAsync("JournalEntry", input.CompanyId, accountIds: accountIds);

        // Validate double-entry balance before saving
        entry.Validate();

        await _repository.InsertAsync(entry, autoSave: true);
        return ObjectMapper.Map<JournalEntry, JournalEntryDto>(entry);
    }

    [Authorize(MyERPPermissions.JournalEntries.Post)]
    public async Task<JournalEntryDto> PostAsync(Guid id)
    {
        var entry = await _repository.GetAsync(id);

        // Validate accounting period is not closed/frozen
        var company = await _companyRepository.GetAsync(entry.CompanyId);
        if (company.AccountsFrozenTillDate.HasValue && entry.PostingDate <= company.AccountsFrozenTillDate.Value)
        {
            throw new BusinessException(MyERPDomainErrorCodes.AccountingPeriodClosed)
                .WithData("frozenTill", company.AccountsFrozenTillDate.Value.ToString("yyyy-MM-dd"))
                .WithData("postingDate", entry.PostingDate.ToString("yyyy-MM-dd"));
        }

        // Check fiscal year exists and is open
        var fyQuery = await _fiscalYearRepository.GetQueryableAsync();
        var fy = fyQuery.FirstOrDefault(f =>
            f.CompanyId == entry.CompanyId && f.StartDate <= entry.PostingDate && f.EndDate >= entry.PostingDate);
        if (fy != null && fy.IsClosed)
        {
            throw new BusinessException(MyERPDomainErrorCodes.FiscalYearClosed)
                .WithData("postingDate", entry.PostingDate.ToString("yyyy-MM-dd"))
                .WithData("fiscalYear", fy.Name);
        }

        // Check closed accounting period
        var periodQuery = await _periodRepository.GetQueryableAsync();
        var closedPeriod = periodQuery.FirstOrDefault(p =>
            !p.IsDisabled && p.IsClosed && p.CompanyId == entry.CompanyId
            && p.StartDate <= entry.PostingDate && p.EndDate >= entry.PostingDate);
        if (closedPeriod != null)
        {
            throw new BusinessException(MyERPDomainErrorCodes.AccountingPeriodClosed)
                .WithData("period", closedPeriod.PeriodName)
                .WithData("postingDate", entry.PostingDate.ToString("yyyy-MM-dd"));
        }

        // Budget Level 3 validation: a manually-posted JE can debit an expense account
        // just as directly as an SI/PI/PO can — must be checked the same way (per
        // DocumentPostingOrchestrator.ValidateBudgetOnPostingAsync's own doc comment,
        // which already named JE as an expected caller before this was wired).
        var budgetItems = entry.Lines
            .Where(l => l.IsDebit && l.Amount > 0)
            .Select(l => new BudgetCheckItem(l.AccountId, l.Amount))
            .ToList();
        if (budgetItems.Count > 0)
        {
            var postingOrchestrator = LazyServiceProvider.LazyGetRequiredService<DocumentPostingOrchestrator>();
            await postingOrchestrator.ValidateBudgetOnPostingAsync(
                entry.CompanyId, entry.PostingDate, budgetItems, entry.TenantId);
        }

        // Validate blocked purchase invoice references (PR #57825 / commit cbafa16fbc)
        if (string.Equals(entry.ReferenceType, "PurchaseInvoice", StringComparison.OrdinalIgnoreCase) && entry.ReferenceId.HasValue)
        {
            var piRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Purchasing.Entities.PurchaseInvoice, Guid>>();
            var pi = await piRepo.FindAsync(entry.ReferenceId.Value);
            if (pi != null && pi.IsBlockedOnDate(entry.PostingDate))
            {
                throw new BusinessException(MyERPDomainErrorCodes.InvoiceOnHold)
                    .WithData("invoiceNumber", pi.InvoiceNumber)
                    .WithData("holdUntil", pi.ReleaseDate?.ToString("yyyy-MM-dd") ?? "indefinite");
            }
        }

        entry.Post();
        await _repository.UpdateAsync(entry, autoSave: true);

        // Audit trail
        var activityRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Core.Entities.DocumentActivityLog, Guid>>();
        await activityRepo.InsertAsync(new Core.Entities.DocumentActivityLog(
            GuidGenerator.Create(), "JournalEntry", entry.Id, "Posted",
            entry.CompanyId, entry.EntryNumber, "Draft", "Posted",
            CurrentUser.Id, tenantId: entry.TenantId));

        return ObjectMapper.Map<JournalEntry, JournalEntryDto>(entry);
    }

    [Authorize(MyERPPermissions.JournalEntries.Post)]
    public async Task<JournalEntryDto> CancelAsync(Guid id)
    {
        var entry = await _repository.GetAsync(id);

        // Validate posting period is not frozen/closed (reversals can't post to locked periods)
        var company = await _companyRepository.GetAsync(entry.CompanyId);
        if (company.AccountsFrozenTillDate.HasValue && entry.PostingDate <= company.AccountsFrozenTillDate.Value)
        {
            throw new BusinessException(MyERPDomainErrorCodes.AccountingPeriodClosed)
                .WithData("frozenTill", company.AccountsFrozenTillDate.Value.ToString("yyyy-MM-dd"))
                .WithData("postingDate", entry.PostingDate.ToString("yyyy-MM-dd"));
        }

        // Check if linked to an active submitted Asset Value Adjustment (PR #51666 / commit 73b038084b)
        if (entry.ReferenceType == "AssetValueAdjustment" && entry.ReferenceId.HasValue)
        {
            var adjRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Assets.Entities.AssetValueAdjustment, Guid>>();
            var adj = await adjRepo.FindAsync(entry.ReferenceId.Value);
            if (adj != null && adj.Status == Core.DocumentStatus.Submitted)
            {
                throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                    .WithData("detail", $"Cannot cancel this document as it is linked with the submitted Asset Value Adjustment '{adj.AdjustmentNumber}'. Please cancel the Asset Value Adjustment to continue.");
            }
        }

        entry.Cancel();
        await _repository.UpdateAsync(entry, autoSave: true);

        // Audit trail
        var activityRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Core.Entities.DocumentActivityLog, Guid>>();
        await activityRepo.InsertAsync(new Core.Entities.DocumentActivityLog(
            GuidGenerator.Create(), "JournalEntry", entry.Id, "Cancelled",
            entry.CompanyId, entry.EntryNumber, "Posted", "Cancelled",
            CurrentUser.Id, tenantId: entry.TenantId));

        return ObjectMapper.Map<JournalEntry, JournalEntryDto>(entry);
    }

    /// <summary>
    /// Creates a reversal Journal Entry from a posted JE.
    /// Per ERPNext JE→JE reversal: swaps debit↔credit on all lines, links via ReversalOfId.
    /// </summary>
    [Authorize(MyERPPermissions.JournalEntries.Post)]
    public async Task<JournalEntryDto> CreateReversalAsync(Guid sourceId)
    {
        var source = await _repository.GetAsync(sourceId);
        if (source.Status != Core.DocumentStatus.Posted)
        {
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition)
                .WithData("documentType", "JournalEntry")
                .WithData("status", source.Status.ToString());
        }

        // Per ERPNext PR #58092 / gotcha: cannot reverse a reversal entry (cancel it instead)
        if (source.ReversalOfId.HasValue || source.VoucherType == JournalEntryVoucherType.Reversal)
        {
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition)
                .WithData("documentType", "JournalEntry")
                .WithData("detail", "Cannot reverse a Journal Entry that is already a reversal. Cancel it instead.");
        }

        // Check if an active reversal already exists for this entry
        var query = await _repository.GetQueryableAsync();
        var existingReversal = query.Any(x => x.ReversalOfId == sourceId && x.Status != Core.DocumentStatus.Cancelled);
        if (existingReversal)
        {
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition)
                .WithData("documentType", "JournalEntry")
                .WithData("detail", "A reversal Journal Entry already exists for this entry.");
        }

        var number = await _numberGenerator.GenerateAsync("JE", source.CompanyId);
        var reversal = new JournalEntry(
            GuidGenerator.Create(), source.CompanyId, source.FiscalYearId,
            DateTime.UtcNow, source.TenantId);

        reversal.EntryNumber = number;
        reversal.VoucherType = JournalEntryVoucherType.Reversal;
        reversal.ReversalOfId = source.Id;
        reversal.IsMultiCurrency = source.IsMultiCurrency;

        // Swap debit↔credit for each line (per ERPNext reversal pattern)
        foreach (var line in source.Lines)
        {
            // Original debit → new credit; original credit → new debit
            reversal.AddLine(
                line.AccountId,
                line.Amount,
                !line.IsDebit, // flip direction
                line.Description != null ? $"Reversal: {line.Description}" : "Reversal entry");
        }

        reversal.Post();
        await _repository.InsertAsync(reversal, autoSave: true);

        // Audit trail
        var activityRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Core.Entities.DocumentActivityLog, Guid>>();
        await activityRepo.InsertAsync(new Core.Entities.DocumentActivityLog(
            GuidGenerator.Create(), "JournalEntry", reversal.Id, "Converted",
            reversal.CompanyId, reversal.EntryNumber, "Draft", "Posted",
            CurrentUser.Id, $"Reversal of {source.EntryNumber}", tenantId: reversal.TenantId));

        return ObjectMapper.Map<JournalEntry, JournalEntryDto>(reversal);
    }

    /// <summary>
    /// Amend a cancelled Journal Entry — creates a new draft copy with amendment link.
    /// Per gotcha #11 / #265: clearance_date is cleared and NOT carried over into the amendment.
    /// </summary>
    [Authorize(MyERPPermissions.JournalEntries.Create)]
    public async Task<JournalEntryDto> AmendAsync(Guid id)
    {
        var original = await _repository.GetAsync(id);
        var amendService = LazyServiceProvider.LazyGetRequiredService<Core.DomainServices.DocumentAmendmentService>();

        amendService.ValidateCanAmend(original.Status);
        var newNumber = amendService.GenerateAmendedNumber(original.EntryNumber ?? original.Id.ToString()[..8], original.AmendmentIndex + 1);

        var amended = new JournalEntry(
            GuidGenerator.Create(),
            original.CompanyId,
            original.FiscalYearId,
            DateTime.UtcNow.Date,
            original.TenantId)
        {
            EntryNumber = newNumber,
            AmendedFromId = original.Id,
            AmendmentIndex = original.AmendmentIndex + 1,
            VoucherType = original.VoucherType,
            ReferenceType = original.ReferenceType,
            ReferenceId = original.ReferenceId,
            ReferenceNumber = original.ReferenceNumber,
            Narration = original.Narration,
            IsOpening = original.IsOpening,
            IsMultiCurrency = original.IsMultiCurrency,
            InterCompanyJournalEntryId = original.InterCompanyJournalEntryId
        };

        // Explicitly ensure ClearanceDate is null (stale clearance date prevention per gotcha #11/#265)
        amended.SetClearanceDate(null);

        foreach (var line in original.Lines)
        {
            amended.AddLine(line.AccountId, line.Amount, line.IsDebit, line.Description);
        }

        amended.Validate();
        await _repository.InsertAsync(amended, autoSave: true);
        return ObjectMapper.Map<JournalEntry, JournalEntryDto>(amended);
    }

    [Authorize(MyERPPermissions.JournalEntries.Create)]
    public async Task<CreateJournalEntryDto> GetJournalEntryTemplateAsync(string documentType, Guid documentId)
    {
        Check.NotNullOrWhiteSpace(documentType, nameof(documentType));
        Check.NotDefaultOrNull<Guid>(documentId, nameof(documentId));

        switch (documentType)
        {
            case "SalesOrder":
            {
                await AuthorizationService.CheckAsync(MyERPPermissions.SalesOrders.Default);
                var soRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Sales.Entities.SalesOrder, Guid>>();
                var so = await soRepo.GetAsync(documentId);

                if (so.Status is Core.DocumentStatus.Cancelled or Core.DocumentStatus.Closed)
                    throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);

                if (so.PerBilled >= 100)
                    throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                        .WithData("detail", "Can only make payment against unbilled Sales Order.");

                var company = await _companyRepository.GetAsync(so.CompanyId);
                var customerRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Sales.Entities.Customer, Guid>>();
                var customer = await customerRepo.GetAsync(so.CustomerId);

                var partyAccountId = customer.DefaultReceivableAccountId
                    ?? company.DefaultReceivableAccountId
                    ?? Guid.Empty;
                var bankAccountId = company.DefaultBankAccountId
                    ?? Guid.Empty;

                var outstanding = Math.Max(0, so.GrandTotal - so.AdvancePaid);
                var fy = (await _fiscalYearRepository.GetQueryableAsync())
                    .FirstOrDefault(f => f.CompanyId == so.CompanyId && f.StartDate <= DateTime.UtcNow.Date && f.EndDate >= DateTime.UtcNow.Date)
                    ?? (await _fiscalYearRepository.GetQueryableAsync()).FirstOrDefault(f => f.CompanyId == so.CompanyId);

                return new CreateJournalEntryDto
                {
                    CompanyId = so.CompanyId,
                    FiscalYearId = fy?.Id ?? Guid.Empty,
                    PostingDate = DateTime.UtcNow.Date,
                    VoucherType = JournalEntryVoucherType.BankEntry,
                    ReferenceType = "SalesOrder",
                    ReferenceId = so.Id,
                    ReferenceNumber = so.OrderNumber,
                    Narration = $"Payment against Sales Order {so.OrderNumber}",
                    Lines = new List<CreateJournalEntryLineDto>
                    {
                        new()
                        {
                            AccountId = bankAccountId,
                            Amount = outstanding > 0 ? outstanding : 0.01m,
                            IsDebit = true,
                            Description = $"Payment against Sales Order {so.OrderNumber}"
                        },
                        new()
                        {
                            AccountId = partyAccountId,
                            Amount = outstanding > 0 ? outstanding : 0.01m,
                            IsDebit = false,
                            Description = $"Payment against Sales Order {so.OrderNumber}"
                        }
                    }
                };
            }
            case "PurchaseOrder":
            {
                await AuthorizationService.CheckAsync(MyERPPermissions.PurchaseOrders.Default);
                var poRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Purchasing.Entities.PurchaseOrder, Guid>>();
                var po = await poRepo.GetAsync(documentId);

                if (po.Status is Core.DocumentStatus.Cancelled or Core.DocumentStatus.Closed)
                    throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);

                if (po.PerBilled >= 100)
                    throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                        .WithData("detail", "Can only make payment against unbilled Purchase Order.");

                var company = await _companyRepository.GetAsync(po.CompanyId);
                var supplierRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Purchasing.Entities.Supplier, Guid>>();
                var supplier = await supplierRepo.GetAsync(po.SupplierId);

                var partyAccountId = supplier.DefaultPayableAccountId
                    ?? company.DefaultPayableAccountId
                    ?? Guid.Empty;
                var bankAccountId = company.DefaultBankAccountId
                    ?? Guid.Empty;

                var outstanding = Math.Max(0, po.GrandTotal - po.AdvancePaid);
                var fy = (await _fiscalYearRepository.GetQueryableAsync())
                    .FirstOrDefault(f => f.CompanyId == po.CompanyId && f.StartDate <= DateTime.UtcNow.Date && f.EndDate >= DateTime.UtcNow.Date)
                    ?? (await _fiscalYearRepository.GetQueryableAsync()).FirstOrDefault(f => f.CompanyId == po.CompanyId);

                return new CreateJournalEntryDto
                {
                    CompanyId = po.CompanyId,
                    FiscalYearId = fy?.Id ?? Guid.Empty,
                    PostingDate = DateTime.UtcNow.Date,
                    VoucherType = JournalEntryVoucherType.BankEntry,
                    ReferenceType = "PurchaseOrder",
                    ReferenceId = po.Id,
                    ReferenceNumber = po.OrderNumber,
                    Narration = $"Payment against Purchase Order {po.OrderNumber}",
                    Lines = new List<CreateJournalEntryLineDto>
                    {
                        new()
                        {
                            AccountId = partyAccountId,
                            Amount = outstanding > 0 ? outstanding : 0.01m,
                            IsDebit = true,
                            Description = $"Payment against Purchase Order {po.OrderNumber}"
                        },
                        new()
                        {
                            AccountId = bankAccountId,
                            Amount = outstanding > 0 ? outstanding : 0.01m,
                            IsDebit = false,
                            Description = $"Payment against Purchase Order {po.OrderNumber}"
                        }
                    }
                };
            }
            case "SalesInvoice":
            {
                await AuthorizationService.CheckAsync(MyERPPermissions.SalesInvoices.Default);
                var siRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Sales.Entities.SalesInvoice, Guid>>();
                var si = await siRepo.GetAsync(documentId);

                if (si.Status != Core.DocumentStatus.Submitted)
                    throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);

                var company = await _companyRepository.GetAsync(si.CompanyId);
                var customerRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Sales.Entities.Customer, Guid>>();
                var customer = await customerRepo.GetAsync(si.CustomerId);

                var partyAccountId = customer.DefaultReceivableAccountId
                    ?? company.DefaultReceivableAccountId
                    ?? Guid.Empty;
                var bankAccountId = company.DefaultBankAccountId
                    ?? Guid.Empty;

                var outstanding = Math.Max(0, si.GrandTotal - si.AmountPaid);
                var fy = (await _fiscalYearRepository.GetQueryableAsync())
                    .FirstOrDefault(f => f.CompanyId == si.CompanyId && f.StartDate <= DateTime.UtcNow.Date && f.EndDate >= DateTime.UtcNow.Date)
                    ?? (await _fiscalYearRepository.GetQueryableAsync()).FirstOrDefault(f => f.CompanyId == si.CompanyId);

                return new CreateJournalEntryDto
                {
                    CompanyId = si.CompanyId,
                    FiscalYearId = fy?.Id ?? Guid.Empty,
                    PostingDate = DateTime.UtcNow.Date,
                    VoucherType = JournalEntryVoucherType.BankEntry,
                    ReferenceType = "SalesInvoice",
                    ReferenceId = si.Id,
                    ReferenceNumber = si.InvoiceNumber,
                    Narration = $"Payment against Sales Invoice {si.InvoiceNumber}",
                    Lines = new List<CreateJournalEntryLineDto>
                    {
                        new()
                        {
                            AccountId = bankAccountId,
                            Amount = outstanding > 0 ? outstanding : 0.01m,
                            IsDebit = true,
                            Description = $"Payment against Sales Invoice {si.InvoiceNumber}"
                        },
                        new()
                        {
                            AccountId = partyAccountId,
                            Amount = outstanding > 0 ? outstanding : 0.01m,
                            IsDebit = false,
                            Description = $"Payment against Sales Invoice {si.InvoiceNumber}"
                        }
                    }
                };
            }
            case "PurchaseInvoice":
            {
                await AuthorizationService.CheckAsync(MyERPPermissions.PurchaseInvoices.Default);
                var piRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Purchasing.Entities.PurchaseInvoice, Guid>>();
                var pi = await piRepo.GetAsync(documentId);

                if (pi.Status != Core.DocumentStatus.Submitted)
                    throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);

                var company = await _companyRepository.GetAsync(pi.CompanyId);
                var supplierRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Purchasing.Entities.Supplier, Guid>>();
                var supplier = await supplierRepo.GetAsync(pi.SupplierId);

                var partyAccountId = supplier.DefaultPayableAccountId
                    ?? company.DefaultPayableAccountId
                    ?? Guid.Empty;
                var bankAccountId = company.DefaultBankAccountId
                    ?? Guid.Empty;

                var outstanding = Math.Max(0, pi.GrandTotal - pi.AmountPaid);
                var fy = (await _fiscalYearRepository.GetQueryableAsync())
                    .FirstOrDefault(f => f.CompanyId == pi.CompanyId && f.StartDate <= DateTime.UtcNow.Date && f.EndDate >= DateTime.UtcNow.Date)
                    ?? (await _fiscalYearRepository.GetQueryableAsync()).FirstOrDefault(f => f.CompanyId == pi.CompanyId);

                return new CreateJournalEntryDto
                {
                    CompanyId = pi.CompanyId,
                    FiscalYearId = fy?.Id ?? Guid.Empty,
                    PostingDate = DateTime.UtcNow.Date,
                    VoucherType = JournalEntryVoucherType.BankEntry,
                    ReferenceType = "PurchaseInvoice",
                    ReferenceId = pi.Id,
                    ReferenceNumber = pi.InvoiceNumber,
                    Narration = $"Payment against Purchase Invoice {pi.InvoiceNumber}",
                    Lines = new List<CreateJournalEntryLineDto>
                    {
                        new()
                        {
                            AccountId = partyAccountId,
                            Amount = outstanding > 0 ? outstanding : 0.01m,
                            IsDebit = true,
                            Description = $"Payment against Purchase Invoice {pi.InvoiceNumber}"
                        },
                        new()
                        {
                            AccountId = bankAccountId,
                            Amount = outstanding > 0 ? outstanding : 0.01m,
                            IsDebit = false,
                            Description = $"Payment against Purchase Invoice {pi.InvoiceNumber}"
                        }
                    }
                };
            }
            default:
                throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition)
                    .WithData("documentType", documentType);
        }
    }

    /// <summary>
    /// Creates a reverse Journal Entry with debits and credits swapped.
    /// Per ERPNext PR #58092 (commit 9dd37d5f32):
    /// - Disallows reversing an entry that is already a reverse entry (must cancel it instead).
    /// - Disallows creating duplicate active reverse entries.
    /// </summary>
    [Authorize(MyERPPermissions.JournalEntries.Create)]
    public async Task<JournalEntryDto> CreateReverseJournalEntryAsync(Guid sourceId)
    {
        var source = await _repository.GetAsync(sourceId, includeDetails: true);

        // Disallow reversing an entry that is already a reverse journal entry
        if (source.ReversalOfId.HasValue)
        {
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("detail", $"Journal Entry {source.EntryNumber} is already a Reverse Journal Entry. Cancel it instead of reversing it.");
        }

        // Must not already have an active reverse JE
        var query = await _repository.GetQueryableAsync();
        var existingReverse = query.FirstOrDefault(j =>
            j.ReversalOfId == source.Id && j.Status != Core.DocumentStatus.Cancelled);

        if (existingReverse != null)
        {
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("detail", $"A Reverse Journal Entry ({existingReverse.EntryNumber}) already exists for this entry.");
        }

        // Source must be posted/submitted
        if (source.Status != Core.DocumentStatus.Posted && source.Status != Core.DocumentStatus.Submitted)
        {
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);
        }

        var entryNumber = await _numberGenerator.GenerateAsync("JournalEntry", source.CompanyId);
        var reverseEntry = new JournalEntry(
            GuidGenerator.Create(),
            source.CompanyId,
            source.FiscalYearId,
            DateTime.UtcNow.Date,
            CurrentTenant.Id)
        {
            EntryNumber = entryNumber,
            VoucherType = source.VoucherType,
            ReferenceType = source.ReferenceType,
            ReferenceId = source.ReferenceId,
            ReferenceNumber = source.ReferenceNumber,
            ReversalOfId = source.Id,
            Narration = $"Reversal of Journal Entry {source.EntryNumber}"
        };

        // Swap Debits and Credits
        foreach (var line in source.Lines)
        {
            reverseEntry.AddLine(
                line.AccountId,
                line.Amount,
                !line.IsDebit,
                $"Reversal of line: {line.Description}");
        }

        await _repository.InsertAsync(reverseEntry, autoSave: true);
        return ObjectMapper.Map<JournalEntry, JournalEntryDto>(reverseEntry);
    }
}

