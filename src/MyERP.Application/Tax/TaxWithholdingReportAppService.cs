using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using MyERP.Accounting.Entities;
using MyERP.Core;
using MyERP.Permissions;
using MyERP.Purchasing.Entities;
using MyERP.Sales.Entities;
using MyERP.Tax.Entities;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Tax;

/// <summary>
/// Tax Withholding Details and TDS Computation Summary reports.
/// Maps to ERPNext accounts/report/tax_withholding_details and tds_computation_summary.
/// </summary>
[Authorize(MyERPPermissions.TaxCategories.Default)]
public class TaxWithholdingReportAppService : ApplicationService, ITaxWithholdingReportAppService
{
    private readonly IRepository<TaxWithholdingEntry, Guid> _entryRepository;
    private readonly IRepository<Supplier, Guid>? _supplierRepository;
    private readonly IRepository<Customer, Guid>? _customerRepository;
    private readonly IRepository<PurchaseInvoice, Guid>? _piRepository;
    private readonly IRepository<SalesInvoice, Guid>? _siRepository;
    private readonly IRepository<PaymentEntry, Guid>? _peRepository;
    private readonly IRepository<JournalEntry, Guid>? _jeRepository;
    private readonly IRepository<FiscalYear, Guid>? _fyRepository;

    public TaxWithholdingReportAppService(
        IRepository<TaxWithholdingEntry, Guid> entryRepository,
        IRepository<Supplier, Guid>? supplierRepository = null,
        IRepository<Customer, Guid>? customerRepository = null,
        IRepository<PurchaseInvoice, Guid>? piRepository = null,
        IRepository<SalesInvoice, Guid>? siRepository = null,
        IRepository<PaymentEntry, Guid>? peRepository = null,
        IRepository<JournalEntry, Guid>? jeRepository = null,
        IRepository<FiscalYear, Guid>? fyRepository = null)
    {
        _entryRepository = entryRepository;
        _supplierRepository = supplierRepository;
        _customerRepository = customerRepository;
        _piRepository = piRepository;
        _siRepository = siRepository;
        _peRepository = peRepository;
        _jeRepository = jeRepository;
        _fyRepository = fyRepository;
    }

    public async Task<TaxWithholdingDetailsReportDto> GetDetailsAsync(TaxWithholdingReportFilterDto input)
    {
        var (from, to) = NormalizeAndValidateDateRange(input.FromDate, input.ToDate);
        var rows = await BuildDetailRowsAsync(input, from, to);

        return new TaxWithholdingDetailsReportDto
        {
            Rows = rows,
            TotalCount = rows.Count,
            TotalTaxableAmount = rows.Sum(r => r.TaxableAmount),
            TotalWithheldAmount = rows.Sum(r => r.WithheldAmount),
        };
    }

    public async Task<TdsComputationSummaryReportDto> GetTdsComputationSummaryAsync(TaxWithholdingReportFilterDto input)
    {
        var (from, to) = NormalizeAndValidateDateRange(input.FromDate, input.ToDate);
        var fiscalYearName = await ValidateSameFiscalYearAsync(input.CompanyId, from, to);

        var detailRows = await BuildDetailRowsAsync(input, from, to);

        // Per ERPNext tds_computation_summary.py:
        // lower deduction certificates or mid-year rate changes must not merge into the normal rate.
        var grouped = detailRows
            .GroupBy(r => (r.PartyType, r.PartyId, r.TaxCategory, r.Rate))
            .Select(g =>
            {
                var first = g.First();
                return new TdsComputationSummaryRowDto
                {
                    PartyType = first.PartyType,
                    PartyId = first.PartyId,
                    PartyName = first.PartyName,
                    Tin = first.Tin,
                    TaxCategory = first.TaxCategory,
                    Rate = first.Rate,
                    HasLdc = g.Any(x => x.HasLdc),
                    TransactionCount = g.Count(),
                    TotalTaxableAmount = g.Sum(x => x.TaxableAmount),
                    TotalWithheldAmount = g.Sum(x => x.WithheldAmount),
                };
            })
            .OrderBy(r => r.TaxCategory)
            .ThenBy(r => r.PartyName)
            .ThenBy(r => r.Rate)
            .ToList();

        return new TdsComputationSummaryReportDto
        {
            FiscalYearName = fiscalYearName,
            Rows = grouped,
            TotalCount = grouped.Count,
            TotalTaxableAmount = grouped.Sum(r => r.TotalTaxableAmount),
            TotalWithheldAmount = grouped.Sum(r => r.TotalWithheldAmount),
        };
    }

    private async Task<List<TaxWithholdingDetailRowDto>> BuildDetailRowsAsync(
        TaxWithholdingReportFilterDto input,
        DateTime from,
        DateTime to)
    {
        var toExclusive = to.AddDays(1);
        var query = await _entryRepository.GetQueryableAsync();
        query = query.Where(e =>
            e.CompanyId == input.CompanyId &&
            e.Status == DocumentStatus.Submitted &&
            e.PostingDate >= from &&
            e.PostingDate < toExclusive);

        if (!string.IsNullOrWhiteSpace(input.PartyType))
            query = query.Where(e => e.PartyType == input.PartyType);
        if (input.PartyId.HasValue)
            query = query.Where(e => e.PartyId == input.PartyId.Value);
        if (!string.IsNullOrWhiteSpace(input.TaxCategory))
            query = query.Where(e => e.TaxCategory == input.TaxCategory);

        var entries = query.ToList();
        if (entries.Count == 0)
        {
            return new List<TaxWithholdingDetailRowDto>();
        }

        var (supplierMap, customerMap) = await LoadPartyMapsAsync(entries);
        var voucherNumberMap = await LoadVoucherNumbersAsync(entries);

        return entries
            .Select(e =>
            {
                string partyName = e.PartyId.ToString();
                string? tin = null;
                if (string.Equals(e.PartyType, "Customer", StringComparison.OrdinalIgnoreCase))
                {
                    if (customerMap.TryGetValue(e.PartyId, out var c))
                    {
                        partyName = c.Name;
                        tin = c.Tin;
                    }
                }
                else if (supplierMap.TryGetValue(e.PartyId, out var s))
                {
                    partyName = s.Name;
                    tin = s.Tin;
                }

                var rate = e.HasLDC && e.LdcRate.HasValue ? e.LdcRate.Value : e.WithholdingRate;
                var voucherNumber = voucherNumberMap.GetValueOrDefault((e.VoucherType, e.VoucherId), e.VoucherId.ToString());

                return new TaxWithholdingDetailRowDto
                {
                    EntryId = e.Id,
                    PartyType = e.PartyType,
                    PartyId = e.PartyId,
                    PartyName = partyName,
                    Tin = tin,
                    TaxCategory = e.TaxCategory ?? string.Empty,
                    Rate = rate,
                    TaxableAmount = e.TaxableAmount,
                    WithheldAmount = e.WithheldAmount,
                    VoucherType = e.VoucherType,
                    VoucherId = e.VoucherId,
                    VoucherNumber = voucherNumber,
                    PostingDate = e.PostingDate,
                    HasLdc = e.HasLDC,
                    CertificateNumber = e.CertificateNumber,
                };
            })
            .OrderBy(r => r.TaxCategory)
            .ThenBy(r => r.PostingDate)
            .ThenBy(r => r.VoucherNumber)
            .ToList();
    }

    private async Task<string?> ValidateSameFiscalYearAsync(Guid companyId, DateTime from, DateTime to)
    {
        var fyRepo = _fyRepository ?? LazyServiceProvider?.LazyGetService<IRepository<FiscalYear, Guid>>();
        if (fyRepo == null) return null;

        var fyQuery = await fyRepo.GetQueryableAsync();
        var years = fyQuery.Where(f => f.CompanyId == companyId).ToList();
        if (years.Count == 0) return null;

        var fromFy = years.FirstOrDefault(f => from.Date >= f.StartDate.Date && from.Date <= f.EndDate.Date);
        var toFy = years.FirstOrDefault(f => to.Date >= f.StartDate.Date && to.Date <= f.EndDate.Date);
        if (fromFy != null && toFy != null && fromFy.Id != toFy.Id)
        {
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("detail", "From Date and To Date lie in different Fiscal Year.");
        }

        return fromFy?.Name ?? toFy?.Name;
    }

    private async Task<(Dictionary<Guid, Supplier> Suppliers, Dictionary<Guid, Customer> Customers)> LoadPartyMapsAsync(
        List<TaxWithholdingEntry> entries)
    {
        var supplierMap = new Dictionary<Guid, Supplier>();
        var customerMap = new Dictionary<Guid, Customer>();

        var supplierIds = entries
            .Where(e => !string.Equals(e.PartyType, "Customer", StringComparison.OrdinalIgnoreCase))
            .Select(e => e.PartyId)
            .Distinct()
            .ToList();
        if (supplierIds.Count > 0)
        {
            var suppRepo = _supplierRepository ?? LazyServiceProvider?.LazyGetService<IRepository<Supplier, Guid>>();
            if (suppRepo != null)
            {
                var q = await suppRepo.GetQueryableAsync();
                supplierMap = q.Where(s => supplierIds.Contains(s.Id)).ToDictionary(s => s.Id);
            }
        }

        var customerIds = entries
            .Where(e => string.Equals(e.PartyType, "Customer", StringComparison.OrdinalIgnoreCase))
            .Select(e => e.PartyId)
            .Distinct()
            .ToList();
        if (customerIds.Count > 0)
        {
            var custRepo = _customerRepository ?? LazyServiceProvider?.LazyGetService<IRepository<Customer, Guid>>();
            if (custRepo != null)
            {
                var q = await custRepo.GetQueryableAsync();
                customerMap = q.Where(c => customerIds.Contains(c.Id)).ToDictionary(c => c.Id);
            }
        }

        return (supplierMap, customerMap);
    }

    private async Task<Dictionary<(string VoucherType, Guid VoucherId), string>> LoadVoucherNumbersAsync(
        List<TaxWithholdingEntry> entries)
    {
        var map = new Dictionary<(string, Guid), string>();

        var piIds = entries.Where(e => e.VoucherType == "PurchaseInvoice").Select(e => e.VoucherId).Distinct().ToList();
        if (piIds.Count > 0)
        {
            var piRepo = _piRepository ?? LazyServiceProvider?.LazyGetService<IRepository<PurchaseInvoice, Guid>>();
            if (piRepo != null)
            {
                var q = await piRepo.GetQueryableAsync();
                foreach (var pi in q.Where(x => piIds.Contains(x.Id)).Select(x => new { x.Id, x.InvoiceNumber }).ToList())
                    map[("PurchaseInvoice", pi.Id)] = pi.InvoiceNumber ?? pi.Id.ToString();
            }
        }

        var siIds = entries.Where(e => e.VoucherType == "SalesInvoice").Select(e => e.VoucherId).Distinct().ToList();
        if (siIds.Count > 0)
        {
            var siRepo = _siRepository ?? LazyServiceProvider?.LazyGetService<IRepository<SalesInvoice, Guid>>();
            if (siRepo != null)
            {
                var q = await siRepo.GetQueryableAsync();
                foreach (var si in q.Where(x => siIds.Contains(x.Id)).Select(x => new { x.Id, x.InvoiceNumber }).ToList())
                    map[("SalesInvoice", si.Id)] = si.InvoiceNumber ?? si.Id.ToString();
            }
        }

        var peIds = entries.Where(e => e.VoucherType == "PaymentEntry").Select(e => e.VoucherId).Distinct().ToList();
        if (peIds.Count > 0)
        {
            var peRepo = _peRepository ?? LazyServiceProvider?.LazyGetService<IRepository<PaymentEntry, Guid>>();
            if (peRepo != null)
            {
                var q = await peRepo.GetQueryableAsync();
                foreach (var pe in q.Where(x => peIds.Contains(x.Id)).Select(x => new { x.Id, x.PaymentNumber }).ToList())
                    map[("PaymentEntry", pe.Id)] = pe.PaymentNumber ?? pe.Id.ToString();
            }
        }

        var jeIds = entries.Where(e => e.VoucherType == "JournalEntry").Select(e => e.VoucherId).Distinct().ToList();
        if (jeIds.Count > 0)
        {
            var jeRepo = _jeRepository ?? LazyServiceProvider?.LazyGetService<IRepository<JournalEntry, Guid>>();
            if (jeRepo != null)
            {
                var q = await jeRepo.GetQueryableAsync();
                foreach (var je in q.Where(x => jeIds.Contains(x.Id)).Select(x => new { x.Id, x.EntryNumber }).ToList())
                    map[("JournalEntry", je.Id)] = je.EntryNumber ?? je.Id.ToString();
            }
        }

        return map;
    }

    private static (DateTime From, DateTime To) NormalizeAndValidateDateRange(DateTime? fromDate, DateTime? toDate)
    {
        var to = (toDate ?? DateTime.UtcNow).Date;
        var from = (fromDate ?? new DateTime(to.Year, 1, 1)).Date;
        if (from > to)
        {
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("detail", "From Date cannot be after To Date.");
        }
        return (from, to);
    }
}
