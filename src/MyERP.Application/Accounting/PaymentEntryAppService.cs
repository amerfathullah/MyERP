using System;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Accounting;
using MyERP.Accounting.DomainServices;
using MyERP.Accounting.Entities;
using MyERP.Core.DomainServices;
using MyERP.Core.Entities;
using MyERP.Permissions;
using MyERP.Purchasing.Entities;
using MyERP.Sales.Entities;
using MyERP.Shared;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Accounting;

[Authorize(MyERPPermissions.PaymentEntries.Default)]
public class PaymentEntryAppService : ApplicationService, IPaymentEntryAppService
{
    /// <summary>
    /// Resolves which account the exchange-gain-loss JE's offsetting leg must hit, and whether
    /// a given raw gain/loss figure (PaidAmount × (ExchangeRate - SourceExchangeRate), computed
    /// from the Receive side by convention — see PaymentEntryExchangeTests) is really a gain or
    /// a loss once PaymentType direction is accounted for.
    /// </summary>
    /// <remarks>
    /// The main JE (DR PaidToAccountId / CR PaidFromAccountId, both at the payment's own
    /// ExchangeRate) leaves the PARTY account — not the bank leg — with a residual balance
    /// whenever ExchangeRate differs from SourceExchangeRate, since the party account was
    /// originally booked at SourceExchangeRate. This leg must be closed against the party
    /// account (PaidFromAccountId for Receive, PaidToAccountId for Pay), not PaidToAccountId
    /// unconditionally. Worked example (Receive, invoice booked at 4.00, paid at 4.20):
    /// Receivable Dr 400 (invoice) then Cr 420 (main JE) = -20 residual → needs Dr Receivable 20
    /// / Cr Gain 20. Direction of "gain" also flips for Pay: a higher settlement rate means the
    /// company paid MORE home-currency to settle the same foreign debt — a loss, not a gain,
    /// even though the raw formula's sign is identical to the Receive case.
    /// </remarks>
    public static (Guid PartyAccountId, bool IsGain) ResolveExchangeGainLossPosting(
        PaymentType paymentType, Guid paidFromAccountId, Guid paidToAccountId, decimal rawGainLoss)
    {
        var partyAccountId = paymentType == PaymentType.Receive ? paidFromAccountId : paidToAccountId;
        var isGain = paymentType == PaymentType.Receive ? rawGainLoss > 0 : rawGainLoss < 0;
        return (partyAccountId, isGain);
    }

    private readonly IRepository<PaymentEntry, Guid> _repository;
    private readonly IRepository<SalesInvoice, Guid> _salesInvoiceRepository;
    private readonly IRepository<PurchaseInvoice, Guid> _purchaseInvoiceRepository;
    private readonly IRepository<PaymentScheduleEntry, Guid> _scheduleRepository;
    private readonly IRepository<PaymentLedgerEntry, Guid> _pleRepository;
    private readonly IDocumentNumberGenerator _numberGenerator;
    private readonly DocumentPostingOrchestrator _postingOrchestrator;
    private readonly IRepository<DocumentActivityLog, Guid> _activityLogRepository;

    public PaymentEntryAppService(
        IRepository<PaymentEntry, Guid> repository,
        IRepository<SalesInvoice, Guid> salesInvoiceRepository,
        IRepository<PurchaseInvoice, Guid> purchaseInvoiceRepository,
        IRepository<PaymentScheduleEntry, Guid> scheduleRepository,
        IRepository<PaymentLedgerEntry, Guid> pleRepository,
        IDocumentNumberGenerator numberGenerator,
        DocumentPostingOrchestrator postingOrchestrator,
        IRepository<DocumentActivityLog, Guid> activityLogRepository)
    {
        _repository = repository;
        _salesInvoiceRepository = salesInvoiceRepository;
        _purchaseInvoiceRepository = purchaseInvoiceRepository;
        _scheduleRepository = scheduleRepository;
        _pleRepository = pleRepository;
        _numberGenerator = numberGenerator;
        _postingOrchestrator = postingOrchestrator;
        _activityLogRepository = activityLogRepository;
    }

    public async Task<PaymentEntryDto> GetAsync(Guid id)
    {
        var pe = await _repository.GetAsync(id);
        var dto = ObjectMapper.Map<PaymentEntry, PaymentEntryDto>(pe);
        await PopulateAccountDetailsAsync(dto);
        dto.PartyName = await ResolvePartyNameAsync(pe.PartyType, pe.PartyId);
        return dto;
    }

    public async Task<PagedResultDto<PaymentEntryDto>> GetListAsync(CompanyFilteredPagedRequestDto input)
    {
        var query = await _repository.GetQueryableAsync();

        if (input.CompanyId.HasValue)
            query = query.Where(x => x.CompanyId == input.CompanyId.Value);

        if (!string.IsNullOrWhiteSpace(input.Filter))
        {
            var filter = input.Filter;
            query = query.Where(x => x.PaymentNumber != null && x.PaymentNumber.Contains(filter));
        }

        if (!string.IsNullOrWhiteSpace(input.Status) && Enum.TryParse<Core.DocumentStatus>(input.Status, true, out var status))
            query = query.Where(x => x.Status == status);

        if (input.FromDate.HasValue)
            query = query.Where(x => x.PostingDate >= input.FromDate.Value);

        if (input.ToDate.HasValue)
            query = query.Where(x => x.PostingDate <= input.ToDate.Value);

        var count = query.Count();
        var sorted = SortingHelper.ApplySorting(query, input.Sorting,
            q => q.OrderByDescending(x => x.PostingDate),
            ("paymentNumber", x => (object)(x.PaymentNumber ?? string.Empty)),
            ("postingDate", x => x.PostingDate),
            ("paidAmount", x => x.PaidAmount),
            ("status", x => x.Status));
        var list = sorted
            .Skip(input.SkipCount)
            .Take(input.MaxResultCount)
            .ToList();

        var dtos = list.Select(ObjectMapper.Map<PaymentEntry, PaymentEntryDto>).ToList();

        // Resolve party names (per ERPNext PR #60804 / commit e36428a61d)
        var customerIds = list.Where(p => string.Equals(p.PartyType, "Customer", StringComparison.OrdinalIgnoreCase) && p.PartyId.HasValue).Select(p => p.PartyId!.Value).Distinct().ToList();
        var supplierIds = list.Where(p => string.Equals(p.PartyType, "Supplier", StringComparison.OrdinalIgnoreCase) && p.PartyId.HasValue).Select(p => p.PartyId!.Value).Distinct().ToList();
        var shareholderIds = list.Where(p => string.Equals(p.PartyType, "Shareholder", StringComparison.OrdinalIgnoreCase) && p.PartyId.HasValue).Select(p => p.PartyId!.Value).Distinct().ToList();
        var employeeIds = list.Where(p => string.Equals(p.PartyType, "Employee", StringComparison.OrdinalIgnoreCase) && p.PartyId.HasValue).Select(p => p.PartyId!.Value).Distinct().ToList();

        var customerNames = new Dictionary<Guid, string>();
        var supplierNames = new Dictionary<Guid, string>();
        var shareholderNames = new Dictionary<Guid, string>();
        var employeeNames = new Dictionary<Guid, string>();

        if (customerIds.Count > 0)
        {
            var custRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Sales.Entities.Customer, Guid>>();
            var custQuery = await custRepo.GetQueryableAsync();
            customerNames = custQuery.Where(c => customerIds.Contains(c.Id))
                .Select(c => new { c.Id, c.Name }).ToList()
                .ToDictionary(c => c.Id, c => c.Name);
        }
        if (supplierIds.Count > 0)
        {
            var suppRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Purchasing.Entities.Supplier, Guid>>();
            var suppQuery = await suppRepo.GetQueryableAsync();
            supplierNames = suppQuery.Where(s => supplierIds.Contains(s.Id))
                .Select(s => new { s.Id, s.Name }).ToList()
                .ToDictionary(s => s.Id, s => s.Name);
        }
        if (shareholderIds.Count > 0)
        {
            var shRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Shareholder, Guid>>();
            var shQuery = await shRepo.GetQueryableAsync();
            shareholderNames = shQuery.Where(s => shareholderIds.Contains(s.Id))
                .Select(s => new { s.Id, s.Title }).ToList()
                .ToDictionary(s => s.Id, s => s.Title);
        }
        if (employeeIds.Count > 0)
        {
            var empRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<HumanResources.Entities.Employee, Guid>>();
            var empQuery = await empRepo.GetQueryableAsync();
            employeeNames = empQuery.Where(e => employeeIds.Contains(e.Id))
                .Select(e => new { e.Id, Name = string.IsNullOrEmpty(e.LastName) ? e.FirstName : e.FirstName + " " + e.LastName }).ToList()
                .ToDictionary(e => e.Id, e => e.Name);
        }

        var bankAccountIds = list.SelectMany(p => new[] { p.PaidFromBankAccountId, p.PaidToBankAccountId })
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .Distinct()
            .ToList();
        var bankAccountNames = new Dictionary<Guid, string>();
        if (bankAccountIds.Count > 0)
        {
            var bankRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<BankAccount, Guid>>();
            var bankQuery = await bankRepo.GetQueryableAsync();
            bankAccountNames = bankQuery.Where(b => bankAccountIds.Contains(b.Id))
                .Select(b => new { b.Id, b.AccountName }).ToList()
                .ToDictionary(b => b.Id, b => b.AccountName);
        }

        for (int i = 0; i < dtos.Count; i++)
        {
            var pe = list[i];
            dtos[i].PartyType = pe.PartyType;
            dtos[i].PartyId = pe.PartyId;
            if (pe.PaidFromBankAccountId.HasValue)
                dtos[i].PaidFromBankAccountName = bankAccountNames.GetValueOrDefault(pe.PaidFromBankAccountId.Value);
            if (pe.PaidToBankAccountId.HasValue)
                dtos[i].PaidToBankAccountName = bankAccountNames.GetValueOrDefault(pe.PaidToBankAccountId.Value);
            if (pe.PartyId.HasValue && !string.IsNullOrWhiteSpace(pe.PartyType))
            {
                if (string.Equals(pe.PartyType, "Customer", StringComparison.OrdinalIgnoreCase))
                    dtos[i].PartyName = customerNames.GetValueOrDefault(pe.PartyId.Value);
                else if (string.Equals(pe.PartyType, "Supplier", StringComparison.OrdinalIgnoreCase))
                    dtos[i].PartyName = supplierNames.GetValueOrDefault(pe.PartyId.Value);
                else if (string.Equals(pe.PartyType, "Shareholder", StringComparison.OrdinalIgnoreCase))
                    dtos[i].PartyName = shareholderNames.GetValueOrDefault(pe.PartyId.Value);
                else if (string.Equals(pe.PartyType, "Employee", StringComparison.OrdinalIgnoreCase))
                    dtos[i].PartyName = employeeNames.GetValueOrDefault(pe.PartyId.Value);
            }
        }

        return new PagedResultDto<PaymentEntryDto>(count, dtos);
    }

    [Authorize(MyERPPermissions.PaymentEntries.Create)]
    public async Task<PaymentEntryDto> CreateAsync(CreatePaymentEntryDto input)
    {
        // Input validation
        Check.NotDefaultOrNull<Guid>(input.CompanyId, nameof(input.CompanyId));
        Check.NotDefaultOrNull<Guid>(input.PaidFromAccountId, nameof(input.PaidFromAccountId));
        Check.NotDefaultOrNull<Guid>(input.PaidToAccountId, nameof(input.PaidToAccountId));

        if (input.PaidAmount < 0)
        {
            throw new BusinessException(MyERPDomainErrorCodes.AmountMustBePositive)
                .WithData("field", nameof(input.PaidAmount));
        }

        if (input.ReceivedAmount.HasValue && input.ReceivedAmount.Value < 0)
        {
            throw new BusinessException(MyERPDomainErrorCodes.AmountMustBePositive)
                .WithData("field", nameof(input.ReceivedAmount));
        }

        // Per ERPNext PR #47069 / commit a854beeb40: set account type and currency if missing
        await PopulateAccountDetailsAsync(input);

        // Per ERPNext PR #59582 / commit 655186714c:
        // Re-derive bank side amount when payment account currency changes / differs:
        // In Pay: PaidFrom is bank side, PaidTo is party side. If bank PaidAmount is 0 and party ReceivedAmount > 0,
        // re-derive PaidAmount from party side ReceivedAmount using exchange rate.
        if (input.PaymentType == PaymentType.Pay)
        {
            if (input.PaidAmount <= 0 && input.ReceivedAmount.HasValue && input.ReceivedAmount.Value > 0)
            {
                var rate = input.ExchangeRate > 0 ? input.ExchangeRate : 1m;
                input.PaidAmount = Math.Round(input.ReceivedAmount.Value * rate, 2);
            }
            else if ((!input.ReceivedAmount.HasValue || input.ReceivedAmount.Value <= 0) && input.PaidAmount > 0)
            {
                if (string.Equals(input.PaidFromAccountCurrency, input.PaidToAccountCurrency, StringComparison.OrdinalIgnoreCase))
                {
                    input.ReceivedAmount = input.PaidAmount;
                }
                else
                {
                    var rate = input.ExchangeRate > 0 ? input.ExchangeRate : 1m;
                    input.ReceivedAmount = Math.Round(input.PaidAmount / rate, 2);
                }
            }
        }
        else if (input.PaymentType == PaymentType.Receive)
        {
            // In Receive: PaidFrom is party side, PaidTo is bank side.
            if ((!input.ReceivedAmount.HasValue || input.ReceivedAmount.Value <= 0) && input.PaidAmount > 0)
            {
                if (string.Equals(input.PaidFromAccountCurrency, input.PaidToAccountCurrency, StringComparison.OrdinalIgnoreCase))
                {
                    input.ReceivedAmount = input.PaidAmount;
                }
                else
                {
                    var rate = input.ExchangeRate > 0 ? input.ExchangeRate : 1m;
                    input.ReceivedAmount = Math.Round(input.PaidAmount * rate, 2);
                }
            }
        }
        else if (input.PaymentType == PaymentType.InternalTransfer)
        {
            if ((!input.ReceivedAmount.HasValue || input.ReceivedAmount.Value <= 0) && input.PaidAmount > 0)
            {
                var rate = input.ExchangeRate > 0 ? input.ExchangeRate : 1m;
                input.ReceivedAmount = string.Equals(input.PaidFromAccountCurrency, input.PaidToAccountCurrency, StringComparison.OrdinalIgnoreCase)
                    ? input.PaidAmount
                    : Math.Round(input.PaidAmount * rate, 2);
            }
        }

        if (input.PaidAmount <= 0)
            throw new Volo.Abp.BusinessException("MyERP:01008")
                .WithData("field", "PaidAmount");

        if (input.ReceivedAmount.HasValue && input.ReceivedAmount.Value <= 0)
            throw new BusinessException(MyERPDomainErrorCodes.AmountMustBePositive)
                .WithData("field", nameof(input.ReceivedAmount));

        if (input.ExchangeRate <= 0)
            throw new BusinessException(MyERPDomainErrorCodes.AmountMustBePositive)
                .WithData("field", nameof(input.ExchangeRate));

        if (input.PostingDate == default)
            input.PostingDate = DateTime.UtcNow.Date;

        var companyRestriction = LazyServiceProvider.LazyGetRequiredService<Core.DomainServices.CompanyRestrictionValidationService>();
        await companyRestriction.ValidateTransactionCompanyAsync(
            "PaymentEntry", input.CompanyId,
            accountIds: new[] { input.PaidFromAccountId, input.PaidToAccountId });

        await ValidatePartyDetailsAsync(
            input.PartyType,
            input.PartyId,
            input.PaymentType,
            input.PaidFromAccountId,
            input.PaidToAccountId,
            input.CompanyId);

        // Per gotcha #197: Receive type cannot have net negative allocation for Customer
        if (input.PaymentType == PaymentType.Receive && string.Equals(input.PartyType, "Customer", StringComparison.OrdinalIgnoreCase))
        {
            var totalAllocated = input.References?.Sum(r => r.AllocatedAmount) ?? 0;
            if (input.References != null && input.References.Count > 0 && totalAllocated < 0)
            {
                throw new Volo.Abp.BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                    .WithData("detail", "Cannot create Receive Payment Entry with net negative allocated amount for Customer. Use Pay payment type for customer refunds.");
            }
        }

        // Per gotcha #458: same-currency received amount cannot exceed paid amount
        if ((input.ExchangeRate == 1m || input.ExchangeRate == 0) && input.ReceivedAmount.HasValue && input.ReceivedAmount.Value > input.PaidAmount)
        {
            throw new Volo.Abp.BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("detail", "Received Amount cannot be greater than Paid Amount for same-currency payments.");
        }

        // Per ERPNext PR #58529 / commit 36a4dfe797: reject same-account internal transfers
        if (input.PaymentType == PaymentType.InternalTransfer &&
            input.PaidFromAccountId != Guid.Empty &&
            input.PaidFromAccountId == input.PaidToAccountId)
        {
            throw new BusinessException(MyERPDomainErrorCodes.SameAccountInternalTransfer);
        }

        await ValidateTransactionReferenceAsync(
            input.PaymentType, input.PaidFromAccountId, input.PaidToAccountId, input.ReferenceNumber, input.ReferenceDate);

        var paymentNumber = await _numberGenerator.GenerateAsync("PaymentEntry", input.CompanyId);
        var pe = new PaymentEntry(
            GuidGenerator.Create(), input.CompanyId, input.PaymentType, input.PostingDate,
            input.PaidAmount, input.PaidFromAccountId, input.PaidToAccountId);

        pe.PaymentNumber = paymentNumber;
        pe.ModeOfPayment = input.ModeOfPayment;
        await ValidateEnabledModeOfPaymentAsync(pe.ModeOfPaymentId, pe.ModeOfPayment);
        pe.PartyType = input.PartyType;
        pe.PartyId = input.PartyId;
        pe.CostCenterId = input.CostCenterId;
        pe.ProjectId = input.ProjectId;
        pe.ReferenceNumber = input.ReferenceNumber;
        pe.ReferenceDate = input.ReferenceDate;
        pe.Notes = input.Notes;
        pe.AgainstOrderId = input.AgainstOrderId;
        pe.AgainstOrderType = input.AgainstOrderType;
        pe.ReceivedAmount = input.ReceivedAmount.HasValue && input.ReceivedAmount.Value > 0
            ? input.ReceivedAmount.Value
            : input.PaidAmount;
        await SetInternalTransferBankAccountsAsync(pe);

        // Multi-currency: auto-resolve exchange rate if not explicitly provided
        // Per ERPNext: when payment currency ≠ company currency, fetch rate from CurrencyExchange
        if (input.ExchangeRate > 0 && input.ExchangeRate != 1m)
        {
            pe.ExchangeRate = input.ExchangeRate;
        }
        else if (!string.IsNullOrWhiteSpace(input.PaymentCurrency))
        {
            var companyRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Core.Entities.Company, Guid>>();
            var company = await companyRepo.GetAsync(input.CompanyId);
            if (!string.IsNullOrWhiteSpace(company.CurrencyCode)
                && !input.PaymentCurrency.Equals(company.CurrencyCode, StringComparison.OrdinalIgnoreCase))
            {
                var exchangeService = LazyServiceProvider.LazyGetRequiredService<CurrencyExchangeService>();
                var rate = await exchangeService.GetExchangeRateAsync(
                    input.PaymentCurrency, company.CurrencyCode, input.PostingDate);
                pe.ExchangeRate = rate;

                var settingsRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<AccountsSettings, Guid>>();
                var settings = (await settingsRepo.GetQueryableAsync()).FirstOrDefault();
                if (settings != null && !settings.AllowStaleExchangeRates)
                {
                    var (isStale, rateDate, daysSinceRate) = await exchangeService.CheckStaleRateAsync(
                        input.PaymentCurrency, company.CurrencyCode, settings.StaleDays);
                    if (isStale)
                    {
                        throw new BusinessException(MyERPDomainErrorCodes.StaleExchangeRate)
                            .WithData("fromCurrency", input.PaymentCurrency)
                            .WithData("toCurrency", company.CurrencyCode)
                            .WithData("daysSinceRate", daysSinceRate == int.MaxValue ? "no rate on record" : daysSinceRate.ToString())
                            .WithData("rateDate", rateDate?.ToString("yyyy-MM-dd") ?? "never");
                    }
                }
            }
        }
        else
        {
            pe.ExchangeRate = input.ExchangeRate;
        }

        // Multi-reference allocation takes precedence over legacy single-invoice field
        if (input.References != null && input.References.Count > 0)
        {
            var duplicateRef = input.References
                .GroupBy(r => (r.ReferenceType, r.ReferenceId, r.PaymentTermId))
                .FirstOrDefault(g => g.Count() > 1);
            if (duplicateRef != null)
            {
                throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                    .WithData("detail", $"Duplicate reference entry: {duplicateRef.Key.ReferenceType} {duplicateRef.Key.ReferenceId} appears more than once.");
            }

            foreach (var refDto in input.References)
            {
                // Per ERPNext PR #47334 / commit b9a02b466b:
                // Do not allocate amount when reference doctype or reference id are not set
                if (string.IsNullOrWhiteSpace(refDto.ReferenceType) || refDto.ReferenceId == Guid.Empty)
                {
                    continue;
                }

                var reference = new PaymentEntryReference(
                    GuidGenerator.Create(),
                    pe.Id,
                    refDto.ReferenceType,
                    refDto.ReferenceId,
                    totalAmount: refDto.AllocatedAmount, // will be corrected at PostAsync
                    outstandingAmount: refDto.AllocatedAmount,
                    allocatedAmount: refDto.AllocatedAmount);
                reference.ExchangeRate = refDto.ExchangeRate;
                reference.PaymentTermId = refDto.PaymentTermId;
                pe.References.Add(reference);
            }
        }
        else if (input.AgainstInvoiceId.HasValue)
        {
            // Legacy single-invoice backwards compatibility
            pe.AgainstInvoiceId = input.AgainstInvoiceId;
            pe.AgainstInvoiceType = input.AgainstInvoiceType;
        }

        pe.Remarks = input.Remarks;
        ApplyTaxRows(pe, input.Taxes);
        await ValidateDunningDeductionsAsync(pe);

        await _repository.InsertAsync(pe, autoSave: true);
        var resultDto = ObjectMapper.Map<PaymentEntry, PaymentEntryDto>(pe);
        await PopulateAccountDetailsAsync(resultDto);
        resultDto.PartyName = await ResolvePartyNameAsync(pe.PartyType, pe.PartyId);
        return resultDto;
    }

    [Authorize(MyERPPermissions.PaymentEntries.Submit)]
    public async Task<PaymentEntryDto> SubmitAsync(Guid id)
    {
        var pe = (await _repository.WithDetailsAsync(p => p.References, p => p.Taxes))
            .FirstOrDefault(x => x.Id == id) ?? await _repository.GetAsync(id);
        await ValidateEnabledModeOfPaymentAsync(pe.ModeOfPaymentId, pe.ModeOfPayment);
        await ValidateDunningDeductionsAsync(pe);
        await ValidateCumulativeReferencesAsync(pe);
        pe.Submit();
        await _repository.UpdateAsync(pe, autoSave: true);
        return ObjectMapper.Map<PaymentEntry, PaymentEntryDto>(pe);
    }

    [Authorize(MyERPPermissions.PaymentEntries.Submit)]
    public async Task<PaymentEntryDto> PostAsync(Guid id)
    {
        var pe = (await _repository.WithDetailsAsync(p => p.References, p => p.Taxes))
            .FirstOrDefault(x => x.Id == id) ?? await _repository.GetAsync(id);
        await ValidateEnabledModeOfPaymentAsync(pe.ModeOfPaymentId, pe.ModeOfPayment);
        await ValidateDunningDeductionsAsync(pe);
        await ValidateCumulativeReferencesAsync(pe);
        await ValidatePartyDetailsAsync(
            pe.PartyType,
            pe.PartyId,
            pe.PaymentType,
            pe.PaidFromAccountId,
            pe.PaidToAccountId,
            pe.CompanyId);

        // Supplier hold enforcement: HoldType.All or HoldType.Payments blocks PE
        if (pe.PartyType == "Supplier" && pe.PartyId.HasValue)
        {
            var supplierRepo = LazyServiceProvider
                .LazyGetRequiredService<IRepository<MyERP.Purchasing.Entities.Supplier, Guid>>();
            var supplier = await supplierRepo.GetAsync(pe.PartyId.Value);
            if (supplier.HoldType == MyERP.Purchasing.SupplierHoldType.All
                || supplier.HoldType == MyERP.Purchasing.SupplierHoldType.Payments)
            {
                throw new BusinessException(MyERPDomainErrorCodes.SupplierOnHold)
                    .WithData("supplierName", supplier.Name)
                    .WithData("holdType", supplier.HoldType.ToString());
            }
        }

        pe.Post();

        // --- Advance Account Auto-Switch (per ERPNext gotcha #205) ---
        // When company has BookAdvancePaymentsInSeparatePartyAccount enabled AND payment
        // only references SO/PO (no invoices), the party account auto-switches to the
        // advance liability/receivable account for proper advance tracking.
        var advanceCompany = await LazyServiceProvider
            .LazyGetRequiredService<IRepository<MyERP.Core.Entities.Company, Guid>>()
            .GetAsync(pe.CompanyId);
        if (advanceCompany.BookAdvancePaymentsInSeparatePartyAccount && pe.IsAdvance)
        {
            // For Receive (customer advance): switch paid_to → advance received account
            if (pe.PaymentType == PaymentType.Receive && advanceCompany.DefaultAdvanceReceivedAccountId.HasValue)
            {
                pe.PaidToAccountId = advanceCompany.DefaultAdvanceReceivedAccountId.Value;
            }
            // For Pay (supplier advance): switch paid_from → advance paid account
            else if (pe.PaymentType == PaymentType.Pay && advanceCompany.DefaultAdvancePaidAccountId.HasValue)
            {
                pe.PaidFromAccountId = advanceCompany.DefaultAdvancePaidAccountId.Value;
            }
        }

        // Term-based allocation validation: if invoice uses payment terms with
        // allocate_payment_based_on_payment_terms, each reference must specify PaymentTermId
        if (pe.References != null && pe.References.Any())
        {
            var peManager = LazyServiceProvider
                .LazyGetRequiredService<MyERP.Accounting.DomainServices.PaymentEntryManager>();
            await peManager.ValidateTermBasedAllocationAsync(pe, async (refId) =>
            {
                // Check if the referenced invoice has a payment terms template with term-based allocation
                var scheduleRepo = LazyServiceProvider
                    .LazyGetRequiredService<IRepository<MyERP.Accounting.Entities.PaymentScheduleEntry, Guid>>();
                var scheduleQuery = await scheduleRepo.GetQueryableAsync();
                return scheduleQuery.Any(s => s.ParentId == refId);
            });
        }

        // Resolve source exchange rate from linked invoice for gain/loss calculation
        if (pe.AgainstInvoiceId.HasValue)
        {
            if (pe.AgainstInvoiceType == "SalesInvoice")
            {
                var si = await _salesInvoiceRepository.GetAsync(pe.AgainstInvoiceId.Value);
                pe.SourceExchangeRate = si.ExchangeRate;

                // Stale outstanding validation: hard error only when outstanding > 0 but payment exceeds it
                if (si.OutstandingAmount > 0 && pe.PaidAmount > si.OutstandingAmount)
                {
                    throw new Volo.Abp.BusinessException(MyERPDomainErrorCodes.OverAllocation)
                        .WithData("outstanding", si.OutstandingAmount)
                        .WithData("allocated", pe.PaidAmount);
                }
                // Per ERPNext validate_paid_invoices: outstanding <= 0 on non-return = soft warning only
                // Do NOT throw — log and allow PE to proceed
            }
            else if (pe.AgainstInvoiceType == "PurchaseInvoice")
            {
                var pi = await _purchaseInvoiceRepository.GetAsync(pe.AgainstInvoiceId.Value);
                pe.SourceExchangeRate = pi.ExchangeRate;

                // Invoice-level hold: independent of Supplier.HoldType (already checked above)
                if (pi.IsBlocked)
                {
                    throw new Volo.Abp.BusinessException(MyERPDomainErrorCodes.PurchaseInvoiceOnHold)
                        .WithData("invoiceNumber", pi.InvoiceNumber);
                }

                // Stale outstanding validation: hard error only when outstanding > 0
                if (pi.OutstandingAmount > 0 && pe.PaidAmount > pi.OutstandingAmount)
                {
                    throw new Volo.Abp.BusinessException(MyERPDomainErrorCodes.OverAllocation)
                        .WithData("outstanding", pi.OutstandingAmount)
                        .WithData("allocated", pe.PaidAmount);
                }
            }
        }

        // GL posting + PLE to reduce outstanding on allocated invoice
        if (pe.PartyType != null && pe.PartyId.HasValue && pe.AgainstInvoiceId.HasValue)
        {
            var allocations = new[]
            {
                new PaymentAllocation
                {
                    VoucherType = pe.AgainstInvoiceType ?? "SalesInvoice",
                    VoucherId = pe.AgainstInvoiceId.Value,
                    AllocatedAmount = pe.PaidAmount
                }
            };

            // Per the Angular form's own resolveAccounts(): Receive → PaidFrom=Receivable, PaidTo=Bank;
            // Pay → PaidFrom=Bank, PaidTo=Payable. The PLE row's account must be the party's own
            // receivable/payable account, not the bank/cash account — this was hardcoded to
            // PaidToAccountId unconditionally, which is only correct for Pay. Every Receive Payment
            // Entry (customer collection) posted its PLE against the bank account instead.
            var partyAccountId = pe.PaymentType == PaymentType.Receive ? pe.PaidFromAccountId : pe.PaidToAccountId;

            await _postingOrchestrator.PostPaymentEntryAsync(
                pe,
                partyAccountId: partyAccountId,
                partyType: pe.PartyType,
                partyId: pe.PartyId.Value,
                accountCurrency: pe.CurrencyCode,
                exchangeRate: pe.ExchangeRate,
                allocations: allocations,
                paidFromAccountId: pe.PaidFromAccountId,
                paidToAccountId: pe.PaidToAccountId);

            // Update the linked invoice's AmountPaid (with optimistic concurrency retry)
            await UpdateInvoiceAmountPaidAsync(pe.AgainstInvoiceType!, pe.AgainstInvoiceId.Value, pe.PaidAmount);

            // Allocate payment to schedule entries (FIFO by due date)
            var scheduleQuery = await _scheduleRepository.GetQueryableAsync();
            var scheduleEntries = scheduleQuery
                .Where(s => s.ParentType == pe.AgainstInvoiceType
                         && s.ParentId == pe.AgainstInvoiceId.Value)
                .OrderBy(s => s.DueDate)
                .ToList();

            if (scheduleEntries.Any())
            {
                var remainingPayment = pe.PaidAmount;
                foreach (var entry in scheduleEntries)
                {
                    if (remainingPayment <= 0) break;
                    var allocated = entry.RecordPayment(remainingPayment);
                    remainingPayment -= allocated;
                    await _scheduleRepository.UpdateAsync(entry);
                }
            }

            // Exchange Gain/Loss JE: when payment rate differs from invoice rate
            if (pe.ExchangeGainLoss != 0 && pe.ExchangeRate != pe.SourceExchangeRate)
            {
                var company = await LazyServiceProvider
                    .LazyGetRequiredService<IRepository<MyERP.Core.Entities.Company, Guid>>()
                    .GetAsync(pe.CompanyId);
                var (fxPartyAccountId, isGain) = ResolveExchangeGainLossPosting(
                    pe.PaymentType, pe.PaidFromAccountId, pe.PaidToAccountId, pe.ExchangeGainLoss);
                var fxAccountId = company.GetExchangeGainLossAccountId(isGain);
                if (fxAccountId.HasValue)
                {
                    // Resolve fiscal year for the posting date
                    var fyRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<MyERP.Accounting.Entities.FiscalYear, Guid>>();
                    var fyQuery = await fyRepo.GetQueryableAsync();
                    var fy = fyQuery.FirstOrDefault(f => f.CompanyId == pe.CompanyId
                        && f.StartDate <= pe.PostingDate && f.EndDate >= pe.PostingDate);

                    if (fy != null)
                    {
                        var je = new MyERP.Accounting.Entities.JournalEntry(
                            GuidGenerator.Create(), pe.CompanyId, fy.Id, pe.PostingDate, pe.TenantId)
                        {
                            VoucherType = MyERP.Accounting.JournalEntryVoucherType.ExchangeGainOrLoss,
                            ReferenceType = "PaymentEntry",
                            ReferenceId = pe.Id,
                        };

                        var gainLossAmount = Math.Abs(pe.ExchangeGainLoss);
                        if (isGain)
                        {
                            // Gain: DR party account (clears the residual the main JE left), CR Exchange Gain
                            je.AddLineWithDimensions(fxPartyAccountId, gainLossAmount, true,
                                pe.CostCenterId, pe.ProjectId, null, "Exchange Gain");
                            if (!string.IsNullOrEmpty(pe.AgainstInvoiceType) && pe.AgainstInvoiceId.HasValue)
                            {
                                je.Lines[^1].AgainstVoucherType = pe.AgainstInvoiceType;
                                je.Lines[^1].AgainstVoucherId = pe.AgainstInvoiceId;
                            }
                            je.AddLineWithDimensions(fxAccountId.Value, gainLossAmount, false,
                                pe.CostCenterId, pe.ProjectId, null, "Exchange Gain");
                        }
                        else
                        {
                            // Loss: DR Exchange Loss, CR party account
                            je.AddLineWithDimensions(fxAccountId.Value, gainLossAmount, true,
                                pe.CostCenterId, pe.ProjectId, null, "Exchange Loss");
                            je.AddLineWithDimensions(fxPartyAccountId, gainLossAmount, false,
                                pe.CostCenterId, pe.ProjectId, null, "Exchange Loss");
                            if (!string.IsNullOrEmpty(pe.AgainstInvoiceType) && pe.AgainstInvoiceId.HasValue)
                            {
                                je.Lines[^1].AgainstVoucherType = pe.AgainstInvoiceType;
                                je.Lines[^1].AgainstVoucherId = pe.AgainstInvoiceId;
                            }
                        }

                        je.Validate();
                        je.Post();
                        await LazyServiceProvider
                            .LazyGetRequiredService<IRepository<MyERP.Accounting.Entities.JournalEntry, Guid>>()
                            .InsertAsync(je);
                    }
                }
            }
        }

        // Advance payment against order (no invoice linked yet)
        if (pe.IsAdvance && pe.AgainstOrderId.HasValue)
        {
            if (pe.AgainstOrderType == "SalesOrder")
            {
                var so = await LazyServiceProvider.LazyGetRequiredService<IRepository<MyERP.Sales.Entities.SalesOrder, Guid>>()
                    .GetAsync(pe.AgainstOrderId.Value);
                so.AdvancePaid += pe.PaidAmount;
                await LazyServiceProvider.LazyGetRequiredService<IRepository<MyERP.Sales.Entities.SalesOrder, Guid>>()
                    .UpdateAsync(so);
            }
            else if (pe.AgainstOrderType == "PurchaseOrder")
            {
                var po = await LazyServiceProvider.LazyGetRequiredService<IRepository<MyERP.Purchasing.Entities.PurchaseOrder, Guid>>()
                    .GetAsync(pe.AgainstOrderId.Value);
                po.AdvancePaid += pe.PaidAmount;
                await LazyServiceProvider.LazyGetRequiredService<IRepository<MyERP.Purchasing.Entities.PurchaseOrder, Guid>>()
                    .UpdateAsync(po);
            }
        }

        // Multi-reference allocation: when PE has explicit references, validate + allocate per reference
        if (pe.References?.Any() == true)
        {
            // Build PLE allocations for multi-ref posting
            var multiAllocations = new List<PaymentAllocation>();

            foreach (var refRow in pe.References)
            {
                // Per ERPNext PR #47334 / commit b9a02b466b:
                // Skip allocation if reference doctype or reference id are not set
                if (string.IsNullOrWhiteSpace(refRow.ReferenceType) || refRow.ReferenceId == Guid.Empty)
                    continue;

                if (refRow.ReferenceType == "SalesInvoice")
                {
                    var si = await _salesInvoiceRepository.GetAsync(refRow.ReferenceId);
                    var allocatedInInvCurrency = ConvertAllocatedToInvoiceCurrency(
                        refRow.AllocatedAmount, pe.CurrencyCode, pe.ExchangeRate, si.CurrencyCode, si.ExchangeRate);

                    // Stale outstanding validation per reference at currency precision (ERPNext PR #59783)
                    PaymentEntryManager.ValidateReferenceAllocation(
                        allocatedInInvCurrency, si.OutstandingAmount, si.InvoiceNumber);

                    await UpdateInvoiceAmountPaidAsync("SalesInvoice", refRow.ReferenceId, allocatedInInvCurrency);

                    multiAllocations.Add(new PaymentAllocation
                    {
                        VoucherType = "SalesInvoice",
                        VoucherId = refRow.ReferenceId,
                        AllocatedAmount = refRow.AllocatedAmount
                    });

                    // FIFO payment schedule allocation per referenced invoice
                    await AllocateToPaymentScheduleAsync(refRow.ReferenceType, refRow.ReferenceId, allocatedInInvCurrency);
                }
                else if (refRow.ReferenceType == "PurchaseInvoice")
                {
                    var pi = await _purchaseInvoiceRepository.GetAsync(refRow.ReferenceId);

                    if (pi.IsBlocked)
                    {
                        throw new Volo.Abp.BusinessException(MyERPDomainErrorCodes.PurchaseInvoiceOnHold)
                            .WithData("invoiceNumber", pi.InvoiceNumber);
                    }

                    var allocatedInInvCurrency = ConvertAllocatedToInvoiceCurrency(
                        refRow.AllocatedAmount, pe.CurrencyCode, pe.ExchangeRate, pi.CurrencyCode, pi.ExchangeRate);

                    // Stale outstanding validation per reference at currency precision (ERPNext PR #59783)
                    PaymentEntryManager.ValidateReferenceAllocation(
                        allocatedInInvCurrency, pi.OutstandingAmount, pi.InvoiceNumber);

                    await UpdateInvoiceAmountPaidAsync("PurchaseInvoice", refRow.ReferenceId, allocatedInInvCurrency);

                    multiAllocations.Add(new PaymentAllocation
                    {
                        VoucherType = "PurchaseInvoice",
                        VoucherId = refRow.ReferenceId,
                        AllocatedAmount = refRow.AllocatedAmount
                    });

                    await AllocateToPaymentScheduleAsync(refRow.ReferenceType, refRow.ReferenceId, allocatedInInvCurrency);
                }
            }

            // Create PLE entries for multi-ref allocations (was missing — payment ledger was incomplete)
            if (multiAllocations.Any() && pe.PartyType != null && pe.PartyId.HasValue)
            {
                // Same party-account fix as the single-ref path above: PaidFrom for Receive.
                var partyAccountId = pe.PaymentType == PaymentType.Receive ? pe.PaidFromAccountId : pe.PaidToAccountId;
                await _postingOrchestrator.PostPaymentEntryAsync(
                    pe,
                    partyAccountId: partyAccountId,
                    partyType: pe.PartyType,
                    partyId: pe.PartyId.Value,
                    accountCurrency: pe.CurrencyCode,
                    exchangeRate: pe.ExchangeRate,
                    allocations: multiAllocations.ToArray(),
                    paidFromAccountId: pe.PaidFromAccountId,
                    paidToAccountId: pe.PaidToAccountId);
            }

            // Per-reference exchange gain/loss JE for multi-currency multi-ref payments
            // Per DO-NOT: each reference may have different exchange rate; gain/loss calculated PER REFERENCE
            var companyForFx = await LazyServiceProvider
                .LazyGetRequiredService<IRepository<MyERP.Core.Entities.Company, Guid>>()
                .FindAsync(pe.CompanyId);
            if (companyForFx != null)
            {
                foreach (var refRow in pe.References)
                {
                    var refExchangeRate = refRow.ExchangeRate > 0 ? refRow.ExchangeRate : pe.SourceExchangeRate;
                    var refGainLoss = refRow.AllocatedAmount * (pe.ExchangeRate - refExchangeRate);
                    if (Math.Abs(refGainLoss) < 0.01m) continue;

                    // Same direction-awareness as the single-ref path above: refGainLoss's raw sign is
                    // Receive-oriented, and the offsetting leg must hit the party account (PaidFrom for
                    // Receive, PaidTo for Pay), not pe.PaidToAccountId unconditionally.
                    var (partyAccountIdForFx, isRefGain) = ResolveExchangeGainLossPosting(
                        pe.PaymentType, pe.PaidFromAccountId, pe.PaidToAccountId, refGainLoss);
                    var refFxAccountId = companyForFx.GetExchangeGainLossAccountId(isRefGain);
                    if (!refFxAccountId.HasValue) continue;

                    var fxFyRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<MyERP.Accounting.Entities.FiscalYear, Guid>>();
                    var fxFyQuery = await fxFyRepo.GetQueryableAsync();
                    var fxFy = fxFyQuery.FirstOrDefault(f => f.CompanyId == pe.CompanyId
                        && f.StartDate <= pe.PostingDate && f.EndDate >= pe.PostingDate);
                    if (fxFy == null) continue;

                    var fxJe = new MyERP.Accounting.Entities.JournalEntry(
                        GuidGenerator.Create(), pe.CompanyId, fxFy.Id, pe.PostingDate, pe.TenantId)
                    {
                        VoucherType = MyERP.Accounting.JournalEntryVoucherType.ExchangeGainOrLoss,
                        ReferenceType = "PaymentEntry",
                        ReferenceId = pe.Id,
                    };

                    if (isRefGain)
                    {
                        fxJe.AddLineWithDimensions(partyAccountIdForFx, Math.Abs(refGainLoss), true,
                            pe.CostCenterId, pe.ProjectId, null, "Exchange Gain"); // DR party account
                        fxJe.Lines[^1].AgainstVoucherType = refRow.ReferenceType;
                        fxJe.Lines[^1].AgainstVoucherId = refRow.ReferenceId;
                        fxJe.AddLineWithDimensions(refFxAccountId.Value, Math.Abs(refGainLoss), false,
                            pe.CostCenterId, pe.ProjectId, null, "Exchange Gain"); // CR Exchange GL
                    }
                    else
                    {
                        fxJe.AddLineWithDimensions(refFxAccountId.Value, Math.Abs(refGainLoss), true,
                            pe.CostCenterId, pe.ProjectId, null, "Exchange Loss"); // DR Exchange GL
                        fxJe.AddLineWithDimensions(partyAccountIdForFx, Math.Abs(refGainLoss), false,
                            pe.CostCenterId, pe.ProjectId, null, "Exchange Loss"); // CR party account
                        fxJe.Lines[^1].AgainstVoucherType = refRow.ReferenceType;
                        fxJe.Lines[^1].AgainstVoucherId = refRow.ReferenceId;
                    }

                    fxJe.Post();
                    await LazyServiceProvider
                        .LazyGetRequiredService<IRepository<MyERP.Accounting.Entities.JournalEntry, Guid>>()
                        .InsertAsync(fxJe);
                }
            }
        }

        // --- Standalone / Internal Transfer / Unallocated Payment Entry GL Posting ---
        if (pe.PaymentType == PaymentType.InternalTransfer)
        {
            var fyRepoTransfer = LazyServiceProvider.LazyGetRequiredService<IRepository<FiscalYear, Guid>>();
            var fyQueryTransfer = await fyRepoTransfer.GetQueryableAsync();
            var fyTransfer = fyQueryTransfer.FirstOrDefault(f => f.CompanyId == pe.CompanyId
                && f.StartDate <= pe.PostingDate && f.EndDate >= pe.PostingDate);

            if (fyTransfer != null)
            {
                var transferJe = new JournalEntry(
                    GuidGenerator.Create(), pe.CompanyId, fyTransfer.Id, pe.PostingDate, pe.TenantId)
                {
                    VoucherType = JournalEntryVoucherType.BankEntry,
                    ReferenceType = "PaymentEntry",
                    ReferenceId = pe.Id,
                };

                var basePaid = Math.Round(pe.PaidAmount * (pe.SourceExchangeRate > 0 ? pe.SourceExchangeRate : pe.ExchangeRate), 2);
                var baseReceived = Math.Round(pe.ReceivedAmount * (pe.TargetExchangeRate > 0 ? pe.TargetExchangeRate : pe.ExchangeRate), 2);

                // CR Source Bank Account (funds outgoing)
                transferJe.AddLineWithDimensions(pe.PaidFromAccountId, basePaid, false, pe.CostCenterId, pe.ProjectId, null, "Internal Transfer - Paid From");

                // DR Destination Bank Account (funds incoming)
                transferJe.AddLineWithDimensions(pe.PaidToAccountId, baseReceived, true, pe.CostCenterId, pe.ProjectId, null, "Internal Transfer - Paid To");

                // Process deductions (e.g. Bank Charges)
                decimal otherDeductions = 0m;
                foreach (var tax in pe.Taxes.Where(t => !t.IsExchangeGainLoss))
                {
                    tax.Calculate(pe.PaidAmount, pe.ExchangeRate);
                    var deductionAmount = tax.BaseTaxAmount > 0 ? tax.BaseTaxAmount : tax.TaxAmount;
                    if (deductionAmount > 0)
                    {
                        otherDeductions += deductionAmount;
                        transferJe.AddLineWithDimensions(tax.AccountId, deductionAmount, true, tax.CostCenterId ?? pe.CostCenterId, pe.ProjectId, null, tax.Description ?? "Bank Charges / Deductions");
                    }
                }

                // Residual Exchange Gain/Loss or Bank Charges (upstream PR #57840):
                // For single-currency internal transfers, use BankChargesAccountId if configured;
                // for cross-currency transfers, use ExchangeGainLossAccountId.
                var fxResidual = Math.Round(basePaid - baseReceived - otherDeductions, 2);
                if (fxResidual != 0)
                {
                    var companyTransfer = await LazyServiceProvider
                        .LazyGetRequiredService<IRepository<Company, Guid>>()
                        .GetAsync(pe.CompanyId);

                    var isSingleCurrency = (pe.SourceExchangeRate <= 0 || pe.SourceExchangeRate == 1m)
                        && (pe.TargetExchangeRate <= 0 || pe.TargetExchangeRate == 1m)
                        || (pe.SourceExchangeRate == pe.TargetExchangeRate);
                    var residualAccountId = (isSingleCurrency && companyTransfer.BankChargesAccountId.HasValue)
                        ? companyTransfer.BankChargesAccountId
                        : companyTransfer.ExchangeGainLossAccountId;

                    if (residualAccountId.HasValue)
                    {
                        var lineDesc = isSingleCurrency ? "Bank Charges" : (fxResidual > 0 ? "Exchange Loss" : "Exchange Gain");
                        if (fxResidual > 0)
                        {
                            // Loss / Bank Charge: DR
                            transferJe.AddLineWithDimensions(residualAccountId.Value, fxResidual, true, pe.CostCenterId, pe.ProjectId, null, lineDesc);
                        }
                        else
                        {
                            // Gain: CR
                            transferJe.AddLineWithDimensions(residualAccountId.Value, Math.Abs(fxResidual), false, pe.CostCenterId, pe.ProjectId, null, lineDesc);
                        }
                    }
                }

                transferJe.Validate();
                transferJe.Post();
                var jeRepo = LazyServiceProvider
                    .LazyGetRequiredService<IRepository<JournalEntry, Guid>>();
                await jeRepo.InsertAsync(transferJe);
            }
        }
        else if (!pe.AgainstInvoiceId.HasValue && (pe.References == null || !pe.References.Any()))
        {
            var partyAccountId = pe.PaymentType == PaymentType.Receive ? pe.PaidFromAccountId : pe.PaidToAccountId;
            await _postingOrchestrator.PostPaymentEntryAsync(
                pe,
                partyAccountId: partyAccountId,
                partyType: pe.PartyType ?? string.Empty,
                partyId: pe.PartyId ?? Guid.Empty,
                accountCurrency: pe.CurrencyCode ?? "MYR",
                exchangeRate: pe.ExchangeRate,
                allocations: Array.Empty<PaymentAllocation>(),
                paidFromAccountId: pe.PaidFromAccountId,
                paidToAccountId: pe.PaidToAccountId);
        }

        // --- Payment Entry Tax GL Posting ---
        // Per ERPNext: PE taxes post separate GL entries per tax row
        // Per gotcha #624: direction = add_deduct_tax × payment_type
        if (pe.PaymentType != PaymentType.InternalTransfer && pe.Taxes.Any(t => !t.IsExchangeGainLoss))
        {
            pe.RecalculateTaxes();

            var fyRepo2 = LazyServiceProvider.LazyGetRequiredService<IRepository<MyERP.Accounting.Entities.FiscalYear, Guid>>();
            var fyQuery2 = await fyRepo2.GetQueryableAsync();
            var fy2 = fyQuery2.FirstOrDefault(f => f.CompanyId == pe.CompanyId
                && f.StartDate <= pe.PostingDate && f.EndDate >= pe.PostingDate);

            if (fy2 != null)
            {
                var taxJe = new MyERP.Accounting.Entities.JournalEntry(
                    GuidGenerator.Create(), pe.CompanyId, fy2.Id, pe.PostingDate, pe.TenantId)
                {
                    VoucherType = JournalEntryVoucherType.PaymentTax,
                    ReferenceType = "PaymentEntry",
                    ReferenceId = pe.Id,
                };
                var bankAccount = pe.PaymentType == PaymentType.Receive
                    ? pe.PaidFromAccountId : pe.PaidToAccountId;

                foreach (var tax in pe.Taxes.Where(t => !t.IsExchangeGainLoss && t.TaxAmount != 0))
                {
                    var isDebit = tax.IsDebit(pe.PaymentType.ToString());
                    if (isDebit)
                    {
                        taxJe.AddLine(tax.AccountId, tax.BaseTaxAmount, true, tax.Description ?? "Payment Tax");
                        if (!tax.IncludedInPaidAmount)
                            taxJe.AddLine(bankAccount, tax.BaseTaxAmount, false, "Payment Tax Contra");
                    }
                    else
                    {
                        taxJe.AddLine(tax.AccountId, tax.BaseTaxAmount, false, tax.Description ?? "Payment Tax");
                        if (!tax.IncludedInPaidAmount)
                            taxJe.AddLine(bankAccount, tax.BaseTaxAmount, true, "Payment Tax Contra");
                    }
                }

                if (taxJe.Lines.Any())
                {
                    taxJe.Validate();
                    taxJe.Post();
                    var jeRepo = LazyServiceProvider
                        .LazyGetRequiredService<IRepository<MyERP.Accounting.Entities.JournalEntry, Guid>>();
                    await jeRepo.InsertAsync(taxJe);
                }
            }
        }

        await _repository.UpdateAsync(pe, autoSave: true);

        // Audit trail
        await _activityLogRepository.InsertAsync(new DocumentActivityLog(
            GuidGenerator.Create(), "PaymentEntry", pe.Id, "Posted",
            pe.CompanyId, pe.PaymentNumber, "Submitted", "Posted",
            CurrentUser.Id, tenantId: pe.TenantId));

        // Notify: payment received (for sales team visibility)
        if (pe.PartyType == "Customer" && pe.PartyId.HasValue && CurrentUser.Id.HasValue)
        {
            try
            {
                var notifSvc = LazyServiceProvider
                    .LazyGetRequiredService<Notification.DomainServices.BusinessNotificationService>();
                await notifSvc.NotifyPaymentReceivedAsync(
                    CurrentUser.Id.Value,
                    pe.PartyType ?? "Customer",
                    pe.PaidAmount,
                    pe.CurrencyCode ?? "MYR",
                    pe.Id,
                    pe.TenantId);
            }
            catch (Exception ex) { Logger.LogWarning(ex, "Payment notification failed for PE {Id}", pe.Id); }
        }

        return ObjectMapper.Map<PaymentEntry, PaymentEntryDto>(pe);
    }

    /// <summary>
    /// Cancel a posted payment entry — reverses all effects:
    /// reduces invoice AmountPaid, reverses PLE, deallocates payment schedule entries,
    /// reduces SO/PO AdvancePaid for advance payments.
    /// </summary>
    [Authorize(MyERPPermissions.PaymentEntries.Cancel)]
    public async Task<PaymentEntryDto> CancelAsync(Guid id)
    {
        var pe = (await _repository.WithDetailsAsync(p => p.References, p => p.Taxes))
            .FirstOrDefault(p => p.Id == id)
            ?? await _repository.GetAsync(id);

        // Validate posting period is not frozen/closed (reversals can't post to locked periods)
        await _postingOrchestrator.ValidatePostingPeriodAsync(pe.CompanyId, pe.PostingDate, "PaymentEntry");

        // Guard: prevent cancel if PE has been used in reconciliation (non-delinked PLE entries)
        var pleQuery = await _pleRepository.GetQueryableAsync();
        var activeReconciliationEntries = pleQuery
            .Where(p => p.VoucherType == "PaymentEntry"
                     && p.VoucherId == pe.Id
                     && !p.Delinked
                     && !p.IsReversal
                     && p.AgainstVoucherType != "PaymentEntry") // Exclude self-referencing PLEs
            .Count();

        if (activeReconciliationEntries > 0)
        {
            throw new BusinessException(MyERPDomainErrorCodes.PaymentEntryUsedInReconciliation)
                .WithData("paymentNumber", pe.PaymentNumber ?? "")
                .WithData("reconciliationCount", activeReconciliationEntries);
        }

        pe.Cancel();

        // Reverse PLE entries + reverse the posted GL Journal Entry (main JE, any per-reference
        // exchange gain/loss JEs, and the separate tax JE are all reversed independently — see
        // ReverseGlForDocumentAsync's doc comment for why they're excluded from it).
        await _postingOrchestrator.ReversePleForDocumentAsync("PaymentEntry", pe.Id);
        await _postingOrchestrator.ReverseGlForDocumentAsync("PaymentEntry", pe.Id);
        await _postingOrchestrator.ReverseExchangeGainLossJournalEntriesAsync("PaymentEntry", pe.Id);
        await _postingOrchestrator.ReversePaymentTaxJournalEntriesAsync("PaymentEntry", pe.Id);

        // Reverse invoice AmountPaid (with concurrency retry)
        if (pe.AgainstInvoiceId.HasValue)
        {
            await UpdateInvoiceAmountPaidAsync(pe.AgainstInvoiceType!, pe.AgainstInvoiceId.Value, -pe.PaidAmount);
            await ReversePaymentScheduleAllocationAsync(pe.AgainstInvoiceType!, pe.AgainstInvoiceId.Value, pe.PaidAmount);
        }

        // Reverse advance payment on SO/PO
        if (pe.IsAdvance && pe.AgainstOrderId.HasValue)
        {
            if (pe.AgainstOrderType == "SalesOrder")
            {
                var so = await LazyServiceProvider.LazyGetRequiredService<IRepository<MyERP.Sales.Entities.SalesOrder, Guid>>()
                    .GetAsync(pe.AgainstOrderId.Value);
                so.AdvancePaid = Math.Max(0, so.AdvancePaid - pe.PaidAmount);
                await LazyServiceProvider.LazyGetRequiredService<IRepository<MyERP.Sales.Entities.SalesOrder, Guid>>()
                    .UpdateAsync(so);
            }
            else if (pe.AgainstOrderType == "PurchaseOrder")
            {
                var po = await LazyServiceProvider.LazyGetRequiredService<IRepository<MyERP.Purchasing.Entities.PurchaseOrder, Guid>>()
                    .GetAsync(pe.AgainstOrderId.Value);
                po.AdvancePaid = Math.Max(0, po.AdvancePaid - pe.PaidAmount);
                await LazyServiceProvider.LazyGetRequiredService<IRepository<MyERP.Purchasing.Entities.PurchaseOrder, Guid>>()
                    .UpdateAsync(po);
            }
        }

        // Reverse multi-reference allocations
        if (pe.References?.Any() == true)
        {
            foreach (var refRow in pe.References)
            {
                if (refRow.ReferenceType == "SalesInvoice")
                {
                    var si = await _salesInvoiceRepository.GetAsync(refRow.ReferenceId);
                    var allocatedInInvCurrency = ConvertAllocatedToInvoiceCurrency(
                        refRow.AllocatedAmount, pe.CurrencyCode, pe.ExchangeRate, si.CurrencyCode, si.ExchangeRate);
                    await UpdateInvoiceAmountPaidAsync(refRow.ReferenceType, refRow.ReferenceId, -allocatedInInvCurrency);
                    await ReversePaymentScheduleAllocationAsync(refRow.ReferenceType, refRow.ReferenceId, allocatedInInvCurrency);
                }
                else if (refRow.ReferenceType == "PurchaseInvoice")
                {
                    var pi = await _purchaseInvoiceRepository.GetAsync(refRow.ReferenceId);
                    var allocatedInInvCurrency = ConvertAllocatedToInvoiceCurrency(
                        refRow.AllocatedAmount, pe.CurrencyCode, pe.ExchangeRate, pi.CurrencyCode, pi.ExchangeRate);
                    await UpdateInvoiceAmountPaidAsync(refRow.ReferenceType, refRow.ReferenceId, -allocatedInInvCurrency);
                    await ReversePaymentScheduleAllocationAsync(refRow.ReferenceType, refRow.ReferenceId, allocatedInInvCurrency);
                }
            }
        }

        await _repository.UpdateAsync(pe, autoSave: true);

        // Reopen any Payment Request that was settled by this Payment Entry (ERPNext commit 389206f2dc)
        var prRepo = LazyServiceProvider?.LazyGetService<IRepository<PaymentRequest, Guid>>();
        if (prRepo != null)
        {
            var prQuery = await prRepo.GetQueryableAsync();
            if (prQuery != null)
            {
                var paidRequests = prQuery
                    .Where(p => p.PaymentEntryId == pe.Id && p.Status == PaymentRequestStatus.Paid)
                    .ToList();
                foreach (var pr in paidRequests)
                {
                    pr.ReopenOnPaymentCancelled();
                    await prRepo.UpdateAsync(pr);
                }
            }
        }

        // Audit trail
        await _activityLogRepository.InsertAsync(new DocumentActivityLog(
            GuidGenerator.Create(), "PaymentEntry", pe.Id, "Cancelled",
            pe.CompanyId, pe.PaymentNumber, "Posted", "Cancelled",
            CurrentUser.Id, tenantId: pe.TenantId));

        return ObjectMapper.Map<PaymentEntry, PaymentEntryDto>(pe);
    }

    /// <summary>
    /// Gets outstanding invoices for a party (used by PE form to select allocation targets).
    /// Returns invoices with outstanding > 0 for the given party.
    /// </summary>
    public async Task<List<OutstandingInvoiceForPaymentDto>> GetOutstandingForPartyAsync(
        string partyType, Guid partyId, Guid companyId)
    {
        var result = await GetPartyOutstandingAsync(partyType, partyId, companyId);
        return result.Invoices;
    }

    public async Task<PartyOutstandingDto> GetPartyOutstandingAsync(
        string partyType, Guid partyId, Guid companyId)
    {
        var today = DateTime.UtcNow.Date;
        var invoices = new List<OutstandingInvoiceForPaymentDto>();
        var orders = new List<OutstandingOrderForPaymentDto>();

        if (partyType == "Customer")
        {
            var siRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Sales.Entities.SalesInvoice, Guid>>();
            var siQuery = await siRepo.GetQueryableAsync();
            var outstanding = siQuery
                .Where(si => si.CustomerId == partyId
                    && si.CompanyId == companyId
                    && si.Status == Core.DocumentStatus.Posted
                    && !si.IsReturn
                    && (si.GrandTotal - si.AmountPaid - si.WriteOffAmount - si.TotalAdvance) > 0.01m)
                .Select(si => new { si.Id, si.InvoiceNumber, si.IssueDate, si.DueDate, si.GrandTotal, si.AmountPaid, si.WriteOffAmount, si.TotalAdvance, si.CurrencyCode })
                .ToList();

            invoices.AddRange(outstanding.Select(si =>
            {
                var ost = si.GrandTotal - si.AmountPaid - si.WriteOffAmount - si.TotalAdvance;
                var daysOverdue = si.DueDate.HasValue ? Math.Max(0, (int)(today - si.DueDate.Value).TotalDays) : 0;
                return new OutstandingInvoiceForPaymentDto
                {
                    InvoiceId = si.Id,
                    InvoiceNumber = si.InvoiceNumber,
                    IssueDate = si.IssueDate,
                    DueDate = si.DueDate,
                    GrandTotal = si.GrandTotal,
                    Outstanding = ost,
                    CurrencyCode = si.CurrencyCode,
                    InvoiceType = "SalesInvoice",
                    DaysOverdue = daysOverdue,
                    IsOverdue = si.DueDate.HasValue && si.DueDate.Value < today && ost > 0.01m
                };
            }));

            var soRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Sales.Entities.SalesOrder, Guid>>();
            var soQuery = await soRepo.GetQueryableAsync();
            var activeOrders = soQuery
                .Where(so => so.CustomerId == partyId
                    && so.CompanyId == companyId
                    && (so.Status == Core.DocumentStatus.ToDeliverAndBill
                        || so.Status == Core.DocumentStatus.ToDeliver
                        || so.Status == Core.DocumentStatus.ToBill)
                    && so.GrandTotal > so.AdvancePaid)
                .Select(so => new { so.Id, so.OrderNumber, so.OrderDate, so.GrandTotal, so.AdvancePaid, so.CurrencyCode })
                .ToList();

            orders.AddRange(activeOrders.Select(so => new OutstandingOrderForPaymentDto
            {
                OrderId = so.Id,
                OrderNumber = so.OrderNumber,
                OrderDate = so.OrderDate,
                GrandTotal = so.GrandTotal,
                AdvancePaid = so.AdvancePaid,
                PendingAdvance = so.GrandTotal - so.AdvancePaid,
                CurrencyCode = so.CurrencyCode,
                OrderType = "SalesOrder"
            }));
        }
        else if (partyType == "Supplier")
        {
            var piRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Purchasing.Entities.PurchaseInvoice, Guid>>();
            var piQuery = await piRepo.GetQueryableAsync();
            var outstanding = piQuery
                .Where(pi => pi.SupplierId == partyId
                    && pi.CompanyId == companyId
                    && pi.Status == Core.DocumentStatus.Posted
                    && !pi.IsReturn
                    && (pi.GrandTotal - pi.AmountPaid - pi.WriteOffAmount - pi.TotalAdvance) > 0.01m)
                .Select(pi => new { pi.Id, pi.InvoiceNumber, pi.IssueDate, pi.DueDate, pi.GrandTotal, pi.AmountPaid, pi.WriteOffAmount, pi.TotalAdvance, pi.CurrencyCode })
                .ToList();

            invoices.AddRange(outstanding.Select(pi =>
            {
                var ost = pi.GrandTotal - pi.AmountPaid - pi.WriteOffAmount - pi.TotalAdvance;
                var daysOverdue = pi.DueDate.HasValue ? Math.Max(0, (int)(today - pi.DueDate.Value).TotalDays) : 0;
                return new OutstandingInvoiceForPaymentDto
                {
                    InvoiceId = pi.Id,
                    InvoiceNumber = pi.InvoiceNumber,
                    IssueDate = pi.IssueDate,
                    DueDate = pi.DueDate,
                    GrandTotal = pi.GrandTotal,
                    Outstanding = ost,
                    CurrencyCode = pi.CurrencyCode,
                    InvoiceType = "PurchaseInvoice",
                    DaysOverdue = daysOverdue,
                    IsOverdue = pi.DueDate.HasValue && pi.DueDate.Value < today && ost > 0.01m
                };
            }));

            var poRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Purchasing.Entities.PurchaseOrder, Guid>>();
            var poQuery = await poRepo.GetQueryableAsync();
            var activeOrders = poQuery
                .Where(po => po.SupplierId == partyId
                    && po.CompanyId == companyId
                    && (po.Status == Core.DocumentStatus.ToDeliverAndBill
                        || po.Status == Core.DocumentStatus.ToDeliver
                        || po.Status == Core.DocumentStatus.ToBill)
                    && po.GrandTotal > po.AdvancePaid)
                .Select(po => new { po.Id, po.OrderNumber, po.OrderDate, po.GrandTotal, po.AdvancePaid, po.CurrencyCode })
                .ToList();

            orders.AddRange(activeOrders.Select(po => new OutstandingOrderForPaymentDto
            {
                OrderId = po.Id,
                OrderNumber = po.OrderNumber,
                OrderDate = po.OrderDate,
                GrandTotal = po.GrandTotal,
                AdvancePaid = po.AdvancePaid,
                PendingAdvance = po.GrandTotal - po.AdvancePaid,
                CurrencyCode = po.CurrencyCode,
                OrderType = "PurchaseOrder"
            }));
        }

        invoices = invoices.OrderBy(r => r.DueDate ?? r.IssueDate).ToList();
        orders = orders.OrderByDescending(r => r.PendingAdvance).ToList();

        return new PartyOutstandingDto
        {
            Invoices = invoices,
            Orders = orders,
            TotalInvoiceOutstanding = invoices.Sum(i => i.Outstanding),
            TotalOrderPending = orders.Sum(o => o.PendingAdvance)
        };
    }

    /// <summary>
    /// Auto-allocates a payment amount across outstanding invoices using FIFO (oldest due date first).
    /// Per ERPNext allocate_amount_to_references: distributes greedily across invoices.
    /// Returns suggested allocation references for the PE form.
    /// </summary>
    public async Task<AutoAllocationResultDto> AutoAllocateAsync(AutoAllocateRequestDto input)
    {
        Check.NotDefaultOrNull<Guid>(input.CompanyId, nameof(input.CompanyId));
        if (input.PaymentAmount <= 0)
            throw new BusinessException("MyERP:01008").WithData("field", "PaymentAmount");

        var outstanding = await GetPartyOutstandingAsync(input.PartyType, input.PartyId, input.CompanyId);
        var invoices = outstanding.Invoices;

        var allocations = new List<AllocationSuggestionDto>();
        var remaining = input.PaymentAmount;

        foreach (var invoice in invoices)
        {
            if (remaining <= 0) break;
            var allocate = Math.Min(remaining, invoice.Outstanding);
            allocations.Add(new AllocationSuggestionDto
            {
                InvoiceId = invoice.InvoiceId,
                InvoiceNumber = invoice.InvoiceNumber,
                InvoiceType = invoice.InvoiceType,
                Outstanding = invoice.Outstanding,
                AllocatedAmount = allocate,
                DueDate = invoice.DueDate,
                IsOverdue = invoice.IsOverdue
            });
            remaining -= allocate;
        }

        var unallocated = Math.Max(0, remaining);
        var writeOffAmount = 0m;

        // Auto-write-off: when remaining < threshold (default RM 1), suggest as write-off
        if (unallocated > 0 && unallocated <= (input.WriteOffThreshold ?? 1m))
        {
            writeOffAmount = unallocated;
            unallocated = 0;
        }

        return new AutoAllocationResultDto
        {
            Allocations = allocations,
            TotalAllocated = allocations.Sum(a => a.AllocatedAmount),
            UnallocatedAmount = unallocated,
            WriteOffAmount = writeOffAmount,
            InvoiceCount = allocations.Count
        };
    }

    /// <summary>
    /// Allocates payment to invoice's payment schedule entries in FIFO order (earliest due date first).
    /// Reused by both legacy single-invoice and multi-reference allocation paths.
    /// </summary>
    private async Task AllocateToPaymentScheduleAsync(string invoiceType, Guid invoiceId, decimal amount)
    {
        var scheduleQuery = await _scheduleRepository.GetQueryableAsync();
        var scheduleEntries = scheduleQuery
            .Where(s => s.ParentType == invoiceType && s.ParentId == invoiceId)
            .OrderBy(s => s.DueDate)
            .ToList();

        if (!scheduleEntries.Any()) return;

        var remaining = amount;
        foreach (var entry in scheduleEntries)
        {
            if (remaining <= 0) break;
            var allocated = entry.RecordPayment(remaining);
            remaining -= allocated;
            await _scheduleRepository.UpdateAsync(entry);
        }
    }

    /// <summary>
    /// Reverses payment schedule entry allocations in LIFO order (latest due date first).
    /// Restores outstanding and base outstanding balances on invoice payment schedule.
    /// Per ERPNext PR #61273: payment cancellation restores base outstanding on schedule.
    /// </summary>
    private async Task ReversePaymentScheduleAllocationAsync(string invoiceType, Guid invoiceId, decimal amount)
    {
        var scheduleQuery = await _scheduleRepository.GetQueryableAsync();
        var scheduleEntries = scheduleQuery
            .Where(s => s.ParentType == invoiceType
                     && s.ParentId == invoiceId)
            .OrderByDescending(s => s.DueDate) // Reverse: latest-first
            .ToList();

        if (!scheduleEntries.Any()) return;

        var remainingReversal = amount;
        foreach (var entry in scheduleEntries)
        {
            if (remainingReversal <= 0) break;
            var reversable = Math.Min(remainingReversal, entry.PaidAmount);
            entry.PaidAmount -= reversable;
            remainingReversal -= reversable;
            await _scheduleRepository.UpdateAsync(entry);
        }
    }

    /// <summary>
    /// Updates invoice AmountPaid with optimistic concurrency retry.
    /// ABP's ConcurrencyStamp on SI/PI detects concurrent modifications —
    /// on conflict, re-reads the entity and retries (up to 3 attempts).
    /// </summary>
    private async Task UpdateInvoiceAmountPaidAsync(string invoiceType, Guid invoiceId, decimal amount)
    {
        const int maxRetries = 3;
        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                if (invoiceType == "SalesInvoice")
                {
                    var si = await _salesInvoiceRepository.GetAsync(invoiceId);
                    si.AmountPaid = Math.Max(0, si.AmountPaid + amount);
                    await _salesInvoiceRepository.UpdateAsync(si, autoSave: true);
                }
                else if (invoiceType == "PurchaseInvoice")
                {
                    var pi = await _purchaseInvoiceRepository.GetAsync(invoiceId);
                    pi.AmountPaid = Math.Max(0, pi.AmountPaid + amount);
                    await _purchaseInvoiceRepository.UpdateAsync(pi, autoSave: true);
                }
                return; // Success
            }
            catch (Volo.Abp.Data.AbpDbConcurrencyException) when (attempt < maxRetries)
            {
                Logger.LogWarning(
                    "Concurrency conflict updating {InvoiceType} {InvoiceId} AmountPaid (attempt {Attempt}/{Max})",
                    invoiceType, invoiceId, attempt, maxRetries);
                await Task.Delay(attempt * 10); // Brief backoff
            }
        }
    }

    [Authorize(MyERPPermissions.PaymentEntries.Edit)]
    public async Task<PaymentEntryDto> UpdateAsync(Guid id, CreatePaymentEntryDto input)
    {
        var entry = (await _repository.WithDetailsAsync(p => p.References, p => p.Taxes))
            .FirstOrDefault(x => x.Id == id) ?? await _repository.GetAsync(id);
        if (entry.Status != Core.DocumentStatus.Draft)
            throw new Volo.Abp.BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition)
                .WithData("detail", "Only Draft payment entries can be edited");

        if (input.PaidAmount <= 0)
        {
            throw new BusinessException(MyERPDomainErrorCodes.AmountMustBePositive)
                .WithData("field", nameof(input.PaidAmount));
        }

        if (input.ReceivedAmount.HasValue && input.ReceivedAmount.Value <= 0)
        {
            throw new BusinessException(MyERPDomainErrorCodes.AmountMustBePositive)
                .WithData("field", nameof(input.ReceivedAmount));
        }

        if (input.ExchangeRate <= 0)
        {
            throw new BusinessException(MyERPDomainErrorCodes.AmountMustBePositive)
                .WithData("field", nameof(input.ExchangeRate));
        }

        entry.PaymentType = input.PaymentType;
        entry.PostingDate = input.PostingDate != default ? input.PostingDate : entry.PostingDate;
        entry.ModeOfPayment = input.ModeOfPayment;
        await ValidateEnabledModeOfPaymentAsync(entry.ModeOfPaymentId, entry.ModeOfPayment);
        entry.PaidAmount = input.PaidAmount;
        entry.PaidFromAccountId = input.PaidFromAccountId != Guid.Empty ? input.PaidFromAccountId : entry.PaidFromAccountId;
        entry.PaidToAccountId = input.PaidToAccountId != Guid.Empty ? input.PaidToAccountId : entry.PaidToAccountId;
        entry.ValidateInternalTransferAccounts();
        await SetInternalTransferBankAccountsAsync(entry);
        await ValidateTransactionReferenceAsync(
            entry.PaymentType, entry.PaidFromAccountId, entry.PaidToAccountId, input.ReferenceNumber, input.ReferenceDate);
        entry.ReferenceNumber = input.ReferenceNumber;
        entry.ReferenceDate = input.ReferenceDate;

        if (input.PartyType != null)
            entry.PartyType = input.PartyType;
        if (input.PartyId.HasValue)
            entry.PartyId = input.PartyId;

        await ValidatePartyDetailsAsync(
            entry.PartyType,
            entry.PartyId,
            entry.PaymentType,
            entry.PaidFromAccountId,
            entry.PaidToAccountId,
            entry.CompanyId);

        if (input.References != null)
        {
            var duplicateRef = input.References
                .GroupBy(r => (r.ReferenceType, r.ReferenceId, r.PaymentTermId))
                .FirstOrDefault(g => g.Count() > 1);
            if (duplicateRef != null)
            {
                throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                    .WithData("detail", $"Duplicate reference entry: {duplicateRef.Key.ReferenceType} {duplicateRef.Key.ReferenceId} appears more than once.");
            }

            entry.References.Clear();
            foreach (var refDto in input.References)
            {
                if (string.IsNullOrWhiteSpace(refDto.ReferenceType) || refDto.ReferenceId == Guid.Empty)
                    continue;

                var reference = new PaymentEntryReference(
                    GuidGenerator.Create(),
                    entry.Id,
                    refDto.ReferenceType,
                    refDto.ReferenceId,
                    totalAmount: refDto.AllocatedAmount,
                    outstandingAmount: refDto.AllocatedAmount,
                    allocatedAmount: refDto.AllocatedAmount)
                {
                    ExchangeRate = refDto.ExchangeRate,
                    PaymentTermId = refDto.PaymentTermId
                };
                entry.References.Add(reference);
            }
        }

        if (input.Taxes != null)
        {
            entry.Taxes.Clear();
            ApplyTaxRows(entry, input.Taxes);
            await ValidateDunningDeductionsAsync(entry);
        }

        await _repository.UpdateAsync(entry, autoSave: true);
        var resultDto = ObjectMapper.Map<PaymentEntry, PaymentEntryDto>(entry);
        await PopulateAccountDetailsAsync(resultDto);
        resultDto.PartyName = await ResolvePartyNameAsync(entry.PartyType, entry.PartyId);
        return resultDto;
    }

    /// <summary>
    /// Maps input tax/charge rows onto a Draft Payment Entry. Per ERPNext Advance Taxes
    /// and Charges: PE has its own tax engine (see PaymentEntryTax) separate from SI/PI —
    /// GL posting for these happens in PostAsync, not here.
    /// </summary>
    private void ApplyTaxRows(PaymentEntry pe, List<PaymentEntryTaxDto>? taxRows)
    {
        if (taxRows == null) return;
        foreach (var row in taxRows)
        {
            var tax = new PaymentEntryTax(GuidGenerator.Create(), pe.Id, row.AccountId, pe.TenantId)
            {
                ChargeType = row.ChargeType,
                Rate = row.Rate,
                TaxAmount = row.TaxAmount,
                IncludedInPaidAmount = row.IncludedInPaidAmount,
                AddDeductTax = row.AddDeductTax,
                Description = row.Description,
                CostCenterId = row.CostCenterId,
                DunningId = row.DunningId,
            };
            pe.AddTax(tax);
        }
    }

    [Authorize(MyERPPermissions.PaymentEntries.Delete)]
    public async Task DeleteAsync(Guid id)
    {
        var entry = await _repository.GetAsync(id);
        if (entry.Status != Core.DocumentStatus.Draft)
            throw new Volo.Abp.BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition)
                .WithData("detail", "Only Draft payment entries can be deleted");
        await _repository.DeleteAsync(id);
    }

    [Authorize(MyERPPermissions.PaymentEntries.Submit)]
    public async Task<BulkOperationResultDto> BulkSubmitAsync(List<Guid> ids)
    {
        var result = new BulkOperationResultDto();
        foreach (var id in ids)
        {
            try
            {
                var entry = await _repository.GetAsync(id);
                if (entry.Status != Core.DocumentStatus.Draft) continue;
                entry.Submit();
                await _repository.UpdateAsync(entry, autoSave: true);
                result.Succeeded++;
            }
            catch (Exception ex)
            {
                result.Failed++;
                result.Errors.Add(new BulkOperationError { Id = id, Message = ex.Message });
            }
        }
        return result;
    }

    [Authorize(MyERPPermissions.PaymentEntries.Submit)]
    public async Task<BulkOperationResultDto> BulkPostAsync(List<Guid> ids)
    {
        var result = new BulkOperationResultDto();
        foreach (var id in ids)
        {
            try
            {
                await PostAsync(id);
                result.Succeeded++;
            }
            catch (Exception ex)
            {
                result.Failed++;
                result.Errors.Add(new BulkOperationError { Id = id, Message = ex.Message });
            }
        }
        return result;
    }

    /// <summary>
    /// Amend a cancelled Payment Entry — creates a new draft copy with amendment link.
    /// Per gotcha #11 / #265: clearance_date is cleared and NOT carried over into the amendment.
    /// </summary>
    [Authorize(MyERPPermissions.PaymentEntries.Create)]
    public async Task<PaymentEntryDto> AmendAsync(Guid id)
    {
        var original = await _repository.GetAsync(id);
        var amendService = LazyServiceProvider.LazyGetRequiredService<Core.DomainServices.DocumentAmendmentService>();

        amendService.ValidateCanAmend(original.Status);
        var newNumber = amendService.GenerateAmendedNumber(original.PaymentNumber ?? original.Id.ToString()[..8], original.AmendmentIndex + 1);

        var amended = new PaymentEntry(
            GuidGenerator.Create(),
            original.CompanyId,
            original.PaymentType,
            DateTime.UtcNow.Date,
            original.PaidAmount,
            original.PaidFromAccountId,
            original.PaidToAccountId,
            original.TenantId)
        {
            PaymentNumber = newNumber,
            AmendedFromId = original.Id,
            AmendmentIndex = original.AmendmentIndex + 1,
            PartyType = original.PartyType,
            PartyId = original.PartyId,
            ModeOfPayment = original.ModeOfPayment,
            ModeOfPaymentId = original.ModeOfPaymentId,
            CostCenterId = original.CostCenterId,
            ProjectId = original.ProjectId,
            CurrencyCode = original.CurrencyCode,
            ExchangeRate = original.ExchangeRate,
            SourceExchangeRate = original.SourceExchangeRate,
            TargetExchangeRate = original.TargetExchangeRate,
            ReceivedAmount = original.ReceivedAmount,
            ReferenceNumber = original.ReferenceNumber,
            ReferenceDate = original.ReferenceDate,
            Notes = original.Notes,
            AgainstInvoiceId = original.AgainstInvoiceId,
            AgainstInvoiceType = original.AgainstInvoiceType,
            AgainstOrderId = original.AgainstOrderId,
            AgainstOrderType = original.AgainstOrderType
        };

        // Explicitly ensure ClearanceDate is null (stale clearance date prevention per gotcha #11/#265)
        amended.SetClearanceDate(null);

        foreach (var refRow in original.References)
        {
            var refCopy = new PaymentEntryReference(
                GuidGenerator.Create(), amended.Id, refRow.ReferenceType, refRow.ReferenceId,
                refRow.TotalAmount, refRow.OutstandingAmount, refRow.AllocatedAmount,
                refRow.ReferenceNumber)
            {
                ExchangeRate = refRow.ExchangeRate,
                PaymentTermId = refRow.PaymentTermId
            };
            amended.References.Add(refCopy);
        }

        foreach (var taxRow in original.Taxes)
        {
            var taxCopy = new PaymentEntryTax(
                GuidGenerator.Create(), amended.Id, taxRow.AccountId, amended.TenantId)
            {
                ChargeType = taxRow.ChargeType,
                Rate = taxRow.Rate,
                TaxAmount = taxRow.TaxAmount,
                BaseTaxAmount = taxRow.BaseTaxAmount,
                IncludedInPaidAmount = taxRow.IncludedInPaidAmount,
                AddDeductTax = taxRow.AddDeductTax,
                Description = taxRow.Description,
                CostCenterId = taxRow.CostCenterId,
                AccountHead = taxRow.AccountHead,
                IsExchangeGainLoss = taxRow.IsExchangeGainLoss,
                DunningId = taxRow.DunningId
            };
            amended.Taxes.Add(taxCopy);
        }

        await _repository.InsertAsync(amended, autoSave: true);
        return ObjectMapper.Map<PaymentEntry, PaymentEntryDto>(amended);
    }

    [Authorize(MyERPPermissions.PaymentEntries.Create)]
    public async Task<CreatePaymentEntryDto> GetPaymentEntryTemplateAsync(string documentType, Guid documentId)
    {
        Check.NotNullOrWhiteSpace(documentType, nameof(documentType));
        Check.NotDefaultOrNull<Guid>(documentId, nameof(documentId));

        var companyRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Core.Entities.Company, Guid>>();
        CreatePaymentEntryDto template;

        switch (documentType)
        {
            case "SalesOrder":
            {
                await AuthorizationService.CheckAsync(MyERPPermissions.SalesOrders.Default);
                var soRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Sales.Entities.SalesOrder, Guid>>();
                var so = await soRepo.GetAsync(documentId);

                if (so.Status is Core.DocumentStatus.Cancelled or Core.DocumentStatus.Closed)
                    throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);

                var company = await companyRepo.GetAsync(so.CompanyId);
                if (so.PerBilled >= (100m + company.OverBillingAllowance))
                    throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                        .WithData("detail", "Can only make payment against unbilled Sales Order.");

                var customerRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Sales.Entities.Customer, Guid>>();
                var customer = await customerRepo.GetAsync(so.CustomerId);

                var partyAccountId = customer.DefaultReceivableAccountId
                    ?? company.DefaultReceivableAccountId
                    ?? Guid.Empty;
                var bankAccountId = company.DefaultBankAccountId
                    ?? Guid.Empty;

                var outstanding = Math.Max(0, so.GrandTotal - so.AdvancePaid);

                template = new CreatePaymentEntryDto
                {
                    CompanyId = so.CompanyId,
                    PaymentType = PaymentType.Receive,
                    PostingDate = DateTime.UtcNow.Date,
                    PaidAmount = outstanding > 0 ? outstanding : 0.01m,
                    ReceivedAmount = outstanding > 0 ? outstanding : 0.01m,
                    PaidFromAccountId = partyAccountId,
                    PaidToAccountId = bankAccountId,
                    PartyType = "Customer",
                    PartyId = so.CustomerId,
                    AgainstOrderId = so.Id,
                    AgainstOrderType = "SalesOrder",
                    PaymentCurrency = so.CurrencyCode,
                    ExchangeRate = 1m,
                    References = new List<PaymentReferenceDto>
                    {
                        new()
                        {
                            ReferenceType = "SalesOrder",
                            ReferenceId = so.Id,
                            AllocatedAmount = outstanding > 0 ? outstanding : 0.01m,
                            ExchangeRate = 1m
                        }
                    }
                };
                break;
            }
            case "PurchaseOrder":
            {
                await AuthorizationService.CheckAsync(MyERPPermissions.PurchaseOrders.Default);
                var poRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Purchasing.Entities.PurchaseOrder, Guid>>();
                var po = await poRepo.GetAsync(documentId);

                if (po.Status is Core.DocumentStatus.Cancelled or Core.DocumentStatus.Closed)
                    throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);

                var company = await companyRepo.GetAsync(po.CompanyId);
                if (po.PerBilled >= (100m + company.OverBillingAllowance))
                    throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                        .WithData("detail", "Can only make payment against unbilled Purchase Order.");

                var supplierRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Purchasing.Entities.Supplier, Guid>>();
                var supplier = await supplierRepo.GetAsync(po.SupplierId);

                var partyAccountId = supplier.DefaultPayableAccountId
                    ?? company.DefaultPayableAccountId
                    ?? Guid.Empty;
                var bankAccountId = company.DefaultBankAccountId
                    ?? Guid.Empty;

                var outstanding = Math.Max(0, po.GrandTotal - po.AdvancePaid);

                template = new CreatePaymentEntryDto
                {
                    CompanyId = po.CompanyId,
                    PaymentType = PaymentType.Pay,
                    PostingDate = DateTime.UtcNow.Date,
                    PaidAmount = outstanding > 0 ? outstanding : 0.01m,
                    ReceivedAmount = outstanding > 0 ? outstanding : 0.01m,
                    PaidFromAccountId = bankAccountId,
                    PaidToAccountId = partyAccountId,
                    PartyType = "Supplier",
                    PartyId = po.SupplierId,
                    AgainstOrderId = po.Id,
                    AgainstOrderType = "PurchaseOrder",
                    PaymentCurrency = po.CurrencyCode,
                    ExchangeRate = 1m,
                    References = new List<PaymentReferenceDto>
                    {
                        new()
                        {
                            ReferenceType = "PurchaseOrder",
                            ReferenceId = po.Id,
                            AllocatedAmount = outstanding > 0 ? outstanding : 0.01m,
                            ExchangeRate = 1m
                        }
                    }
                };
                break;
            }
            case "SalesInvoice":
            {
                await AuthorizationService.CheckAsync(MyERPPermissions.SalesInvoices.Default);
                var si = await _salesInvoiceRepository.GetAsync(documentId);

                if (si.Status != Core.DocumentStatus.Submitted)
                    throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);

                var company = await companyRepo.GetAsync(si.CompanyId);
                var customerRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Sales.Entities.Customer, Guid>>();
                var customer = await customerRepo.GetAsync(si.CustomerId);

                var partyAccountId = customer.DefaultReceivableAccountId
                    ?? company.DefaultReceivableAccountId
                    ?? Guid.Empty;
                var bankAccountId = company.DefaultBankAccountId
                    ?? Guid.Empty;

                var outstanding = Math.Max(0, si.GrandTotal - si.AmountPaid);

                template = new CreatePaymentEntryDto
                {
                    CompanyId = si.CompanyId,
                    PaymentType = PaymentType.Receive,
                    PostingDate = DateTime.UtcNow.Date,
                    PaidAmount = outstanding > 0 ? outstanding : 0.01m,
                    ReceivedAmount = outstanding > 0 ? outstanding : 0.01m,
                    PaidFromAccountId = partyAccountId,
                    PaidToAccountId = bankAccountId,
                    PartyType = "Customer",
                    PartyId = si.CustomerId,
                    AgainstInvoiceId = si.Id,
                    AgainstInvoiceType = "SalesInvoice",
                    PaymentCurrency = si.CurrencyCode,
                    ExchangeRate = 1m,
                    References = new List<PaymentReferenceDto>
                    {
                        new()
                        {
                            ReferenceType = "SalesInvoice",
                            ReferenceId = si.Id,
                            AllocatedAmount = outstanding > 0 ? outstanding : 0.01m,
                            ExchangeRate = 1m
                        }
                    }
                };
                break;
            }
            case "PurchaseInvoice":
            {
                await AuthorizationService.CheckAsync(MyERPPermissions.PurchaseInvoices.Default);
                var pi = await _purchaseInvoiceRepository.GetAsync(documentId);

                if (pi.Status != Core.DocumentStatus.Submitted)
                    throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);

                var company = await companyRepo.GetAsync(pi.CompanyId);
                var supplierRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Purchasing.Entities.Supplier, Guid>>();
                var supplier = await supplierRepo.GetAsync(pi.SupplierId);

                var partyAccountId = supplier.DefaultPayableAccountId
                    ?? company.DefaultPayableAccountId
                    ?? Guid.Empty;
                var bankAccountId = company.DefaultBankAccountId
                    ?? Guid.Empty;

                var outstanding = Math.Max(0, pi.GrandTotal - pi.AmountPaid);

                template = new CreatePaymentEntryDto
                {
                    CompanyId = pi.CompanyId,
                    PaymentType = PaymentType.Pay,
                    PostingDate = DateTime.UtcNow.Date,
                    PaidAmount = outstanding > 0 ? outstanding : 0.01m,
                    ReceivedAmount = outstanding > 0 ? outstanding : 0.01m,
                    PaidFromAccountId = bankAccountId,
                    PaidToAccountId = partyAccountId,
                    PartyType = "Supplier",
                    PartyId = pi.SupplierId,
                    AgainstInvoiceId = pi.Id,
                    AgainstInvoiceType = "PurchaseInvoice",
                    PaymentCurrency = pi.CurrencyCode,
                    ExchangeRate = 1m,
                    References = new List<PaymentReferenceDto>
                    {
                        new()
                        {
                            ReferenceType = "PurchaseInvoice",
                            ReferenceId = pi.Id,
                            AllocatedAmount = outstanding > 0 ? outstanding : 0.01m,
                            ExchangeRate = 1m
                        }
                    }
                };
                break;
            }
            case "Dunning":
            {
                // Per ERPNext commits 7ca8bc3290 & 8a09633536 (get_payment_entry for Dunning):
                // Resolve overdue amounts and unpaid dunning fees/interest in the receivable account currency.
                await AuthorizationService.CheckAsync(MyERPPermissions.Dunnings.Default);
                var dunningRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Sales.Entities.Dunning, Guid>>();
                var d = (await dunningRepo.WithDetailsAsync()).FirstOrDefault(x => x.Id == documentId)
                    ?? await dunningRepo.GetAsync(documentId);

                if (d.Status != Core.DocumentStatus.Submitted)
                    throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);

                var company = await companyRepo.GetAsync(d.CompanyId);
                var companyCurrency = !string.IsNullOrWhiteSpace(company.CurrencyCode) ? company.CurrencyCode : "MYR";
                var customerRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Sales.Entities.Customer, Guid>>();
                var customer = await customerRepo.GetAsync(d.CustomerId);
                var accountRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Account, Guid>>();

                Guid partyAccountId = Guid.Empty;
                var invoiceLookup = new Dictionary<Guid, Sales.Entities.SalesInvoice>();
                if (d.OverduePayments.Any())
                {
                    var invIds = d.OverduePayments.Select(p => p.SalesInvoiceId).Distinct().ToList();
                    var siQuery = await _salesInvoiceRepository.GetQueryableAsync();
                    invoiceLookup = siQuery.Where(si => invIds.Contains(si.Id)).ToDictionary(si => si.Id);
                    var firstWithDebitTo = d.OverduePayments
                        .Select(p => invoiceLookup.GetValueOrDefault(p.SalesInvoiceId))
                        .FirstOrDefault(si => si != null && si.DebitToAccountId != Guid.Empty);
                    if (firstWithDebitTo != null)
                        partyAccountId = firstWithDebitTo.DebitToAccountId;
                }

                if (partyAccountId == Guid.Empty)
                {
                    partyAccountId = customer.DefaultReceivableAccountId
                        ?? company.DefaultReceivableAccountId
                        ?? Guid.Empty;
                }

                var partyAccount = partyAccountId != Guid.Empty ? await accountRepo.FindAsync(partyAccountId) : null;
                var partyAccountCurrency = !string.IsNullOrWhiteSpace(partyAccount?.Currency)
                    ? partyAccount.Currency!
                    : companyCurrency;

                decimal partyExchangeRate = 1m;
                if (!string.Equals(partyAccountCurrency, companyCurrency, StringComparison.OrdinalIgnoreCase))
                {
                    if (string.Equals(partyAccountCurrency, d.Currency, StringComparison.OrdinalIgnoreCase) && d.ConversionRate > 0)
                    {
                        partyExchangeRate = d.ConversionRate;
                    }
                    else
                    {
                        var exchangeService = LazyServiceProvider.LazyGetRequiredService<CurrencyExchangeService>();
                        var fetchedRate = await exchangeService.GetExchangeRateAsync(partyAccountCurrency, companyCurrency, DateTime.UtcNow.Date);
                        if (fetchedRate > 0) partyExchangeRate = fetchedRate;
                    }
                }

                var dunningAppService = LazyServiceProvider.LazyGetRequiredService<Sales.IDunningAppService>();
                var unpaidOverdue = await dunningAppService.GetUnpaidOverduePaymentsAsync(documentId, inAccountCurrency: true);

                var references = unpaidOverdue
                    .GroupBy(p => p.SalesInvoiceId)
                    .Select(g =>
                    {
                        var alloc = Math.Round(g.Sum(x => x.OutstandingAmount), 2);
                        var inv = invoiceLookup.GetValueOrDefault(g.Key);
                        var refRate = (inv != null
                                && string.Equals(partyAccountCurrency, inv.CurrencyCode, StringComparison.OrdinalIgnoreCase)
                                && inv.ExchangeRate > 0)
                            ? inv.ExchangeRate
                            : partyExchangeRate;
                        return new PaymentReferenceDto
                        {
                            ReferenceType = "SalesInvoice",
                            ReferenceId = g.Key,
                            AllocatedAmount = alloc,
                            ExchangeRate = refRate
                        };
                    })
                    .Where(r => r.AllocatedAmount > 0m)
                    .ToList();

                decimal unpaidCharges;
                if (string.Equals(partyAccountCurrency, d.Currency, StringComparison.OrdinalIgnoreCase))
                {
                    unpaidCharges = d.UnpaidDunningAmount;
                }
                else if (string.Equals(partyAccountCurrency, companyCurrency, StringComparison.OrdinalIgnoreCase))
                {
                    unpaidCharges = d.UnpaidBaseDunningAmount;
                }
                else
                {
                    unpaidCharges = partyExchangeRate > 0
                        ? Math.Round(d.UnpaidBaseDunningAmount / partyExchangeRate, 2)
                        : d.UnpaidBaseDunningAmount;
                }

                var overdueTotal = references.Sum(r => r.AllocatedAmount);
                var grandTotal = Math.Round(overdueTotal + unpaidCharges, 2);

                Guid? incomeAccountId = d.IncomeAccountId;
                if ((!incomeAccountId.HasValue || incomeAccountId.Value == Guid.Empty) && d.DunningTypeId.HasValue)
                {
                    var dtRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Sales.Entities.DunningType, Guid>>();
                    var dt = await dtRepo.FindAsync(d.DunningTypeId.Value);
                    incomeAccountId = dt?.IncomeAccountId;
                }
                if (!incomeAccountId.HasValue || incomeAccountId.Value == Guid.Empty)
                {
                    incomeAccountId = company.DefaultIncomeAccountId;
                }

                var taxes = new List<PaymentEntryTaxDto>();
                if (unpaidCharges > 0m && incomeAccountId.HasValue && incomeAccountId.Value != Guid.Empty)
                {
                    taxes.Add(new PaymentEntryTaxDto
                    {
                        AccountId = incomeAccountId.Value,
                        ChargeType = PaymentTaxChargeType.Actual,
                        AddDeductTax = TaxAddDeduct.Add,
                        TaxAmount = unpaidCharges,
                        BaseTaxAmount = Math.Round(unpaidCharges * partyExchangeRate, 2),
                        IncludedInPaidAmount = false,
                        DunningId = d.Id,
                        CostCenterId = d.CostCenterId,
                        Description = $"Dunning Level {d.DunningLevel} Charges"
                    });
                }

                var bankAccountId = company.DefaultBankAccountId ?? Guid.Empty;
                template = new CreatePaymentEntryDto
                {
                    CompanyId = d.CompanyId,
                    PaymentType = PaymentType.Receive,
                    PostingDate = DateTime.UtcNow.Date,
                    PaidAmount = grandTotal > 0 ? grandTotal : 0.01m,
                    ReceivedAmount = grandTotal > 0 ? grandTotal : 0.01m,
                    PaidFromAccountId = partyAccountId,
                    PaidFromAccountCurrency = partyAccountCurrency,
                    PaidToAccountId = bankAccountId,
                    PartyType = "Customer",
                    PartyId = d.CustomerId,
                    CostCenterId = d.CostCenterId,
                    PaymentCurrency = partyAccountCurrency,
                    ExchangeRate = partyExchangeRate,
                    References = references,
                    Taxes = taxes
                };
                break;
            }
            default:
                throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition)
                    .WithData("documentType", documentType);
        }

        // Per ERPNext PR #47069 / commit a854beeb40: set account type and currency if missing
        await PopulateAccountDetailsAsync(template);

        // Per ERPNext PR #47171 / commit 9612521894:
        // Set correct paid/receive amount and exchange rate if doc currency differs from party account currency or bank currency
        if (!string.IsNullOrEmpty(template.PaidFromAccountCurrency) && !string.IsNullOrEmpty(template.PaidToAccountCurrency))
        {
            var sourceCurrency = template.PaymentType == PaymentType.Receive
                ? template.PaidFromAccountCurrency
                : template.PaidToAccountCurrency;
            var targetCurrency = template.PaymentType == PaymentType.Receive
                ? template.PaidToAccountCurrency
                : template.PaidFromAccountCurrency;

            if (!sourceCurrency.Equals(targetCurrency, StringComparison.OrdinalIgnoreCase))
            {
                var exchangeService = LazyServiceProvider.LazyGetRequiredService<CurrencyExchangeService>();
                var rate = await exchangeService.GetExchangeRateAsync(sourceCurrency, targetCurrency, template.PostingDate);
                if (rate > 0)
                {
                    template.ExchangeRate = rate;
                    if (template.PaymentType == PaymentType.Receive)
                    {
                        template.ReceivedAmount = Math.Round(template.PaidAmount * rate, 2);
                    }
                    else
                    {
                        template.PaidAmount = Math.Round((template.ReceivedAmount ?? template.PaidAmount) * rate, 2);
                    }
                }
            }
        }

        return template;
    }

    /// <summary>
    /// Sets account currency and account type if missing on PaymentEntryDto.
    /// Per ERPNext PR #47069 / commit a854beeb40.
    /// </summary>
    private async Task PopulateAccountDetailsAsync(PaymentEntryDto dto)
    {
        var accountRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Account, Guid>>();
        if (dto.PaidFromAccountId != Guid.Empty && (string.IsNullOrEmpty(dto.PaidFromAccountCurrency) || string.IsNullOrEmpty(dto.PaidFromAccountType)))
        {
            var acc = await accountRepo.FindAsync(dto.PaidFromAccountId);
            if (acc != null)
            {
                dto.PaidFromAccountCurrency ??= acc.Currency;
                dto.PaidFromAccountType ??= acc.AccountType.ToString();
            }
        }
        if (dto.PaidToAccountId != Guid.Empty && (string.IsNullOrEmpty(dto.PaidToAccountCurrency) || string.IsNullOrEmpty(dto.PaidToAccountType)))
        {
            var acc = await accountRepo.FindAsync(dto.PaidToAccountId);
            if (acc != null)
            {
                dto.PaidToAccountCurrency ??= acc.Currency;
                dto.PaidToAccountType ??= acc.AccountType.ToString();
            }
        }
        if (dto.PaidFromBankAccountId.HasValue && string.IsNullOrEmpty(dto.PaidFromBankAccountName))
        {
            var bankRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<BankAccount, Guid>>();
            var b = await bankRepo.FindAsync(dto.PaidFromBankAccountId.Value);
            dto.PaidFromBankAccountName = b?.AccountName;
        }
        if (dto.PaidToBankAccountId.HasValue && string.IsNullOrEmpty(dto.PaidToBankAccountName))
        {
            var bankRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<BankAccount, Guid>>();
            var b = await bankRepo.FindAsync(dto.PaidToBankAccountId.Value);
            dto.PaidToBankAccountName = b?.AccountName;
        }
    }

    /// <summary>
    /// Sets read-only bank account links for Internal Transfer payment entries.
    /// Per ERPNext PR #60585 / commit ff8bbe8f56: sets PaidFromBankAccountId and PaidToBankAccountId
    /// from the active Bank Account linked to PaidFromAccountId / PaidToAccountId.
    /// Clears them when payment type is not Internal Transfer.
    /// </summary>
    private async Task SetInternalTransferBankAccountsAsync(PaymentEntry pe)
    {
        if (pe.PaymentType == PaymentType.InternalTransfer)
        {
            var bankRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<BankAccount, Guid>>();
            var query = await bankRepo.GetQueryableAsync();
            var activeBankAccounts = query.Where(b => !b.IsDisabled).ToList();

            pe.PaidFromBankAccountId = activeBankAccounts.FirstOrDefault(b => b.AccountId == pe.PaidFromAccountId)?.Id;
            pe.PaidToBankAccountId = activeBankAccounts.FirstOrDefault(b => b.AccountId == pe.PaidToAccountId)?.Id;
        }
        else
        {
            pe.PaidFromBankAccountId = null;
            pe.PaidToBankAccountId = null;
        }
    }

    /// <summary>
    /// Sets account currency and account type if missing on CreatePaymentEntryDto.
    /// Per ERPNext PR #47069 / commit a854beeb40.
    /// </summary>
    private async Task PopulateAccountDetailsAsync(CreatePaymentEntryDto dto)
    {
        var accountRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Account, Guid>>();
        if (dto.PaidFromAccountId != Guid.Empty && (string.IsNullOrEmpty(dto.PaidFromAccountCurrency) || string.IsNullOrEmpty(dto.PaidFromAccountType)))
        {
            var acc = await accountRepo.FindAsync(dto.PaidFromAccountId);
            if (acc != null)
            {
                dto.PaidFromAccountCurrency ??= acc.Currency;
                dto.PaidFromAccountType ??= acc.AccountType.ToString();
            }
        }
        if (dto.PaidToAccountId != Guid.Empty && (string.IsNullOrEmpty(dto.PaidToAccountCurrency) || string.IsNullOrEmpty(dto.PaidToAccountType)))
        {
            var acc = await accountRepo.FindAsync(dto.PaidToAccountId);
            if (acc != null)
            {
                dto.PaidToAccountCurrency ??= acc.Currency;
                dto.PaidToAccountType ??= acc.AccountType.ToString();
            }
        }
    }

    /// <summary>
    /// ERPNext validate_transaction_reference: a payment through a Bank account needs the bank
    /// reference number and date (the receiving account for Receive, the paying account otherwise).
    /// </summary>
    private async Task ValidateTransactionReferenceAsync(
        PaymentType paymentType, Guid paidFromAccountId, Guid paidToAccountId, string? referenceNumber, DateTime? referenceDate)
    {
        if (!string.IsNullOrWhiteSpace(referenceNumber) && referenceDate.HasValue)
            return;

        var bankSideAccountId = paymentType == PaymentType.Receive ? paidToAccountId : paidFromAccountId;
        var account = await LazyServiceProvider.LazyGetRequiredService<IRepository<Account, Guid>>().FindAsync(bankSideAccountId);
        if (account?.AccountSubType == AccountSubType.BankAccount)
            throw new Volo.Abp.BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("detail", "Reference No and Reference Date is mandatory for Bank transaction.");
    }

    /// <summary>
    /// Validates registered party type and account type compatibility.
    /// Maps to ERPNext PR #60804 (commit 86bfd5a633).
    /// </summary>
    private async Task ValidatePartyDetailsAsync(
        string? partyType,
        Guid? partyId,
        PaymentType paymentType,
        Guid paidFromAccountId,
        Guid paidToAccountId,
        Guid companyId)
    {
        if (string.IsNullOrWhiteSpace(partyType))
            return;

        var trimmedPartyType = partyType.Trim();
        var partyValidation = LazyServiceProvider.LazyGetRequiredService<PartyValidationService>();
        var partyTypeEntity = await partyValidation.ValidatePartyTypeExistsAsync(trimmedPartyType);

        if (!string.Equals(trimmedPartyType, "Employee", StringComparison.OrdinalIgnoreCase))
        {
            var partyAccountId = paymentType == PaymentType.Receive ? paidFromAccountId :
                                 paymentType == PaymentType.Pay ? paidToAccountId : Guid.Empty;

            if (partyAccountId != Guid.Empty)
            {
                var companyRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Company, Guid>>();
                var company = await companyRepo.FindAsync(companyId);
                var separateAdvance = company?.BookAdvancePaymentsInSeparatePartyAccount == true;

                var accountRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Account, Guid>>();
                var account = await accountRepo.FindAsync(partyAccountId);
                if (account != null)
                {
                    partyValidation.ValidatePartyAccountType(trimmedPartyType, partyTypeEntity.AccountType, account, separateAdvance);
                }
            }
        }

        if (partyId.HasValue)
        {
            await partyValidation.ValidatePartyEntityExistsAsync(trimmedPartyType, partyId.Value);
            await ValidatePartyUsableAsync(trimmedPartyType, partyId.Value, companyId);
        }
    }

    /// <summary>
    /// Resolves party title/display name across supported party types (per ERPNext PR #60804 / commit e36428a61d).
    /// </summary>
    private async Task<string?> ResolvePartyNameAsync(string? partyType, Guid? partyId)
    {
        if (!partyId.HasValue || string.IsNullOrWhiteSpace(partyType))
            return null;

        if (string.Equals(partyType, "Customer", StringComparison.OrdinalIgnoreCase))
        {
            var custRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Sales.Entities.Customer, Guid>>();
            var c = await custRepo.FindAsync(partyId.Value);
            return c?.Name;
        }
        if (string.Equals(partyType, "Supplier", StringComparison.OrdinalIgnoreCase))
        {
            var suppRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Purchasing.Entities.Supplier, Guid>>();
            var s = await suppRepo.FindAsync(partyId.Value);
            return s?.Name;
        }
        if (string.Equals(partyType, "Shareholder", StringComparison.OrdinalIgnoreCase))
        {
            var shRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Shareholder, Guid>>();
            var sh = await shRepo.FindAsync(partyId.Value);
            return sh?.Title;
        }
        if (string.Equals(partyType, "Employee", StringComparison.OrdinalIgnoreCase))
        {
            var empRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<HumanResources.Entities.Employee, Guid>>();
            var emp = await empRepo.FindAsync(partyId.Value);
            return emp?.FullName;
        }

        return null;
    }

    /// <summary>ERPNext validate_party_frozen_disabled: no payment to/from a disabled or (unless overridden) frozen Customer or Supplier.</summary>
    private async Task ValidatePartyUsableAsync(string? partyType, Guid? partyId, Guid companyId)
    {
        if (!partyId.HasValue) return;

        string? name = null;
        var isDisabled = false;
        var isFrozen = false;
        if (string.Equals(partyType, "Customer", StringComparison.OrdinalIgnoreCase))
        {
            var customer = await LazyServiceProvider.LazyGetRequiredService<IRepository<Customer, Guid>>().FindAsync(partyId.Value);
            if (customer == null) return;
            name = customer.Name;
            isDisabled = !customer.IsActive;
            isFrozen = customer.IsFrozen;
        }
        else if (string.Equals(partyType, "Supplier", StringComparison.OrdinalIgnoreCase))
        {
            var supplier = await LazyServiceProvider.LazyGetRequiredService<IRepository<Supplier, Guid>>().FindAsync(partyId.Value);
            if (supplier == null) return;
            name = supplier.Name;
            isDisabled = !supplier.IsActive;
            isFrozen = supplier.IsFrozen;
        }
        else return;

        await LazyServiceProvider.LazyGetRequiredService<PartyValidationService>()
            .ValidatePartyForTransactionAsync(partyType!, isDisabled, isFrozen, name!, companyId);
    }

    private async Task ValidateEnabledModeOfPaymentAsync(Guid? modeOfPaymentId, string? modeOfPaymentName)
    {
        if (!modeOfPaymentId.HasValue && string.IsNullOrWhiteSpace(modeOfPaymentName))
        {
            return;
        }

        var mopRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<ModeOfPayment, Guid>>();
        ModeOfPayment? mop = null;

        if (modeOfPaymentId.HasValue)
        {
            mop = await mopRepo.FindAsync(modeOfPaymentId.Value);
        }
        else if (!string.IsNullOrWhiteSpace(modeOfPaymentName))
        {
            var query = await mopRepo.GetQueryableAsync();
            mop = query.FirstOrDefault(m => m.Name == modeOfPaymentName);
        }

        if (mop != null && !mop.IsActive)
        {
            throw new Volo.Abp.BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("message", $"Mode of Payment '{mop.Name}' is disabled.");
        }
    }

    /// <summary>
    /// Validates dunning deductions linked to Payment Entry per ERPNext PR #61620 (commit a5f08e36ec).
    /// Enforces submitted dunning, customer/company match, receive payment type, and income account alignment.
    /// </summary>
    private async Task ValidateDunningDeductionsAsync(PaymentEntry pe)
    {
        var dunningIds = pe.Taxes
            .Where(t => t.DunningId.HasValue && t.DunningId.Value != Guid.Empty)
            .Select(t => t.DunningId!.Value)
            .Distinct()
            .ToList();

        if (dunningIds.Count == 0) return;

        var dunningRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Sales.Entities.Dunning, Guid>>();
        var dunningQuery = await dunningRepo.GetQueryableAsync();
        var dunnings = dunningQuery
            .Where(d => dunningIds.Contains(d.Id))
            .ToDictionary(d => d.Id);

        var dunningTypeIds = dunnings.Values
            .Where(d => d.DunningTypeId.HasValue)
            .Select(d => d.DunningTypeId!.Value)
            .Distinct()
            .ToList();
        var dtRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Sales.Entities.DunningType, Guid>>();
        var dtQuery = await dtRepo.GetQueryableAsync();
        var dunningTypes = dtQuery
            .Where(dt => dunningTypeIds.Contains(dt.Id))
            .ToDictionary(dt => dt.Id);

        var companyRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Core.Entities.Company, Guid>>();
        var company = await companyRepo.FindAsync(pe.CompanyId);

        var idx = 0;
        foreach (var tax in pe.Taxes.Where(t => t.DunningId.HasValue && t.DunningId.Value != Guid.Empty))
        {
            idx++;
            if (!dunnings.TryGetValue(tax.DunningId!.Value, out var dunning) || dunning.Status != Core.DocumentStatus.Submitted)
            {
                throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                    .WithData("detail", $"Row #{idx}: Dunning {tax.DunningId} must be submitted.");
            }

            if (pe.PaymentType != PaymentType.Receive || !string.Equals(pe.PartyType, "Customer", StringComparison.OrdinalIgnoreCase))
            {
                throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                    .WithData("detail", $"Row #{idx}: Dunning charges require a Receive payment from a Customer.");
            }

            if (pe.PartyId != dunning.CustomerId)
            {
                throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                    .WithData("detail", $"Row #{idx}: Dunning {dunning.Id} belongs to a different customer.");
            }

            if (pe.CompanyId != dunning.CompanyId)
            {
                throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                    .WithData("detail", $"Row #{idx}: Dunning {dunning.Id} belongs to a different company.");
            }

            Guid? expectedIncomeAccountId = dunning.IncomeAccountId;
            if ((!expectedIncomeAccountId.HasValue || expectedIncomeAccountId.Value == Guid.Empty)
                && dunning.DunningTypeId.HasValue
                && dunningTypes.TryGetValue(dunning.DunningTypeId.Value, out var dt))
            {
                expectedIncomeAccountId = dt.IncomeAccountId;
            }
            if (!expectedIncomeAccountId.HasValue || expectedIncomeAccountId.Value == Guid.Empty)
            {
                expectedIncomeAccountId = company?.DefaultIncomeAccountId;
            }

            if (expectedIncomeAccountId.HasValue && expectedIncomeAccountId.Value != Guid.Empty && tax.AccountId != expectedIncomeAccountId.Value)
            {
                throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                    .WithData("detail", $"Row #{idx}: Dunning charges must use the income account configured on Dunning {dunning.Id}.");
            }

            if (tax.AddDeductTax == TaxAddDeduct.Deduct && tax.TaxAmount >= 0)
            {
                throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                    .WithData("detail", $"Row #{idx}: Dunning charges must have a negative deduction amount.");
            }
        }
    }

    private async Task ValidateCumulativeReferencesAsync(PaymentEntry pe)
    {
        if (pe.References == null || pe.References.Count == 0)
            return;

        var scheduleQuery = await _scheduleRepository.GetQueryableAsync();
        var refIds = pe.References
            .Where(r => r.ReferenceId != Guid.Empty)
            .Select(r => r.ReferenceId)
            .Distinct()
            .ToList();

        var schedulesByInvoice = refIds.Count > 0
            ? scheduleQuery
                .Where(s => refIds.Contains(s.ParentId))
                .ToList()
                .GroupBy(s => s.ParentId)
                .ToDictionary(g => g.Key, g => g.ToList())
            : new Dictionary<Guid, List<PaymentScheduleEntry>>();

        var snapshots = new List<MyERP.Accounting.DomainServices.ReferenceAllocationSnapshot>(pe.References.Count);

        foreach (var refRow in pe.References)
        {
            if (string.IsNullOrWhiteSpace(refRow.ReferenceType) || refRow.ReferenceId == Guid.Empty)
                continue;

            if (refRow.ReferenceType == "SalesInvoice")
            {
                var si = await _salesInvoiceRepository.FindAsync(refRow.ReferenceId);
                if (si == null) continue;

                var allocatedInInvCurrency = ConvertAllocatedToInvoiceCurrency(
                    refRow.AllocatedAmount, pe.CurrencyCode, pe.ExchangeRate, si.CurrencyCode, si.ExchangeRate);

                PaymentScheduleEntry? termRow = null;
                if (refRow.PaymentTermId.HasValue && refRow.PaymentTermId.Value != Guid.Empty
                    && schedulesByInvoice.TryGetValue(si.Id, out var siSchedules))
                {
                    termRow = siSchedules.FirstOrDefault(s => s.Id == refRow.PaymentTermId.Value);
                }

                snapshots.Add(new MyERP.Accounting.DomainServices.ReferenceAllocationSnapshot(
                    VoucherType: "SalesInvoice",
                    VoucherId: si.Id,
                    ReferenceName: si.InvoiceNumber,
                    AllocatedAmount: allocatedInInvCurrency,
                    InvoiceOutstandingAmount: si.OutstandingAmount,
                    IsTermBasedAllocation: refRow.PaymentTermId.HasValue && refRow.PaymentTermId.Value != Guid.Empty,
                    PaymentTermId: refRow.PaymentTermId,
                    TermOutstandingAmount: termRow?.Outstanding,
                    PaymentTermDescription: termRow?.Description));
            }
            else if (refRow.ReferenceType == "PurchaseInvoice")
            {
                var pi = await _purchaseInvoiceRepository.FindAsync(refRow.ReferenceId);
                if (pi == null) continue;

                var allocatedInInvCurrency = ConvertAllocatedToInvoiceCurrency(
                    refRow.AllocatedAmount, pe.CurrencyCode, pe.ExchangeRate, pi.CurrencyCode, pi.ExchangeRate);

                PaymentScheduleEntry? termRow = null;
                if (refRow.PaymentTermId.HasValue && refRow.PaymentTermId.Value != Guid.Empty
                    && schedulesByInvoice.TryGetValue(pi.Id, out var piSchedules))
                {
                    termRow = piSchedules.FirstOrDefault(s => s.Id == refRow.PaymentTermId.Value);
                }

                snapshots.Add(new MyERP.Accounting.DomainServices.ReferenceAllocationSnapshot(
                    VoucherType: "PurchaseInvoice",
                    VoucherId: pi.Id,
                    ReferenceName: pi.InvoiceNumber,
                    AllocatedAmount: allocatedInInvCurrency,
                    InvoiceOutstandingAmount: pi.OutstandingAmount,
                    IsTermBasedAllocation: refRow.PaymentTermId.HasValue && refRow.PaymentTermId.Value != Guid.Empty,
                    PaymentTermId: refRow.PaymentTermId,
                    TermOutstandingAmount: termRow?.Outstanding,
                    PaymentTermDescription: termRow?.Description));
            }
        }

        if (snapshots.Count > 0)
        {
            MyERP.Accounting.DomainServices.PaymentEntryManager.ValidateCumulativeReferenceAllocations(snapshots);
        }
    }

    private static decimal ConvertAllocatedToInvoiceCurrency(
        decimal allocatedAmount,
        string? paymentCurrency,
        decimal paymentExchangeRate,
        string? invoiceCurrency,
        decimal invoiceExchangeRate)
    {
        if (string.IsNullOrWhiteSpace(paymentCurrency)
            || string.IsNullOrWhiteSpace(invoiceCurrency)
            || string.Equals(paymentCurrency, invoiceCurrency, StringComparison.OrdinalIgnoreCase))
        {
            return allocatedAmount;
        }

        var peRate = paymentExchangeRate > 0 ? paymentExchangeRate : 1m;
        var invRate = invoiceExchangeRate > 0 ? invoiceExchangeRate : 1m;
        return Math.Round((allocatedAmount * peRate) / invRate, 2);
    }
}
