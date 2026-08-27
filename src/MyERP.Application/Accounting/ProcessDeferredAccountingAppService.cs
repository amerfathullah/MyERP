using System;
using System.Linq;
using System.Linq.Dynamic.Core;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using MyERP.Accounting.DomainServices;
using MyERP.Accounting.Entities;
using MyERP.Core.Entities;
using MyERP.Permissions;
using MyERP.Purchasing.Entities;
using MyERP.Sales.Entities;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Accounting;

[Authorize(MyERPPermissions.ProcessDeferredAccountings.Default)]
public class ProcessDeferredAccountingAppService : MyERPAppService, IProcessDeferredAccountingAppService
{
    private readonly IRepository<ProcessDeferredAccounting, Guid> _repository;
    private readonly IRepository<Company, Guid> _companyRepository;
    private readonly IRepository<Account, Guid> _accountRepository;
    private readonly IRepository<SalesInvoice, Guid> _salesInvoiceRepository;
    private readonly IRepository<PurchaseInvoice, Guid> _purchaseInvoiceRepository;
    private readonly IRepository<JournalEntry, Guid> _journalEntryRepository;
    private readonly DeferredAccountingService _deferredAccountingService;

    public ProcessDeferredAccountingAppService(
        IRepository<ProcessDeferredAccounting, Guid> repository,
        IRepository<Company, Guid> companyRepository,
        IRepository<Account, Guid> accountRepository,
        IRepository<SalesInvoice, Guid> salesInvoiceRepository,
        IRepository<PurchaseInvoice, Guid> purchaseInvoiceRepository,
        IRepository<JournalEntry, Guid> journalEntryRepository,
        DeferredAccountingService deferredAccountingService)
    {
        _repository = repository;
        _companyRepository = companyRepository;
        _accountRepository = accountRepository;
        _salesInvoiceRepository = salesInvoiceRepository;
        _purchaseInvoiceRepository = purchaseInvoiceRepository;
        _journalEntryRepository = journalEntryRepository;
        _deferredAccountingService = deferredAccountingService;
    }

    public async Task<PagedResultDto<ProcessDeferredAccountingDto>> GetListAsync(ProcessDeferredAccountingGetListInput input)
    {
        var queryable = await _repository.GetQueryableAsync();

        if (!string.IsNullOrWhiteSpace(input.Filter))
        {
            queryable = queryable.Where(x => x.ProcessNumber.Contains(input.Filter));
        }

        if (input.CompanyId.HasValue)
        {
            queryable = queryable.Where(x => x.CompanyId == input.CompanyId.Value);
        }

        if (input.Type.HasValue)
        {
            queryable = queryable.Where(x => x.Type == input.Type.Value);
        }

        if (input.FromDate.HasValue)
        {
            queryable = queryable.Where(x => x.PostingDate >= input.FromDate.Value.Date);
        }

        if (input.ToDate.HasValue)
        {
            queryable = queryable.Where(x => x.PostingDate <= input.ToDate.Value.Date);
        }

        var totalCount = await AsyncExecuter.CountAsync(queryable);
        var sorting = string.IsNullOrWhiteSpace(input.Sorting) ? $"{nameof(ProcessDeferredAccounting.PostingDate)} desc, {nameof(ProcessDeferredAccounting.CreationTime)} desc" : input.Sorting;

        var items = await AsyncExecuter.ToListAsync(queryable
            .OrderBy(sorting)
            .Skip(input.SkipCount)
            .Take(input.MaxResultCount));

        var companies = (await _companyRepository.GetListAsync()).ToDictionary(c => c.Id, c => c.Name);
        var accounts = (await _accountRepository.GetListAsync()).ToDictionary(a => a.Id, a => a.AccountName);

        var mapper = new ProcessDeferredAccountingMapper();
        var dtos = items.Select(x =>
        {
            var dto = mapper.Map(x);
            if (companies.TryGetValue(x.CompanyId, out var compName)) dto.CompanyName = compName;
            if (x.AccountId.HasValue && accounts.TryGetValue(x.AccountId.Value, out var accName)) dto.AccountName = accName;
            return dto;
        }).ToList();

        return new PagedResultDto<ProcessDeferredAccountingDto>(totalCount, dtos);
    }

    public async Task<ProcessDeferredAccountingDto> GetAsync(Guid id)
    {
        var entity = await _repository.GetAsync(id);
        var dto = new ProcessDeferredAccountingMapper().Map(entity);

        var company = await _companyRepository.FindAsync(entity.CompanyId);
        if (company != null) dto.CompanyName = company.Name;

        if (entity.AccountId.HasValue)
        {
            var account = await _accountRepository.FindAsync(entity.AccountId.Value);
            if (account != null) dto.AccountName = account.AccountName;
        }

        return dto;
    }

    [Authorize(MyERPPermissions.ProcessDeferredAccountings.Create)]
    public async Task<ProcessDeferredAccountingDto> CreateAsync(CreateProcessDeferredAccountingDto input)
    {
        var processNumber = $"ACC-PDA-{DateTime.UtcNow:yyyyMMdd}-{GuidGenerator.Create().ToString()[..6].ToUpperInvariant()}";

        var entity = new ProcessDeferredAccounting(
            GuidGenerator.Create(),
            processNumber,
            input.CompanyId,
            input.Type,
            input.PostingDate,
            input.StartDate,
            input.EndDate,
            input.AccountId,
            CurrentTenant.Id);

        await _repository.InsertAsync(entity, autoSave: true);
        return await GetAsync(entity.Id);
    }

    [Authorize(MyERPPermissions.ProcessDeferredAccountings.Edit)]
    public async Task<ProcessDeferredAccountingDto> UpdateAsync(Guid id, UpdateProcessDeferredAccountingDto input)
    {
        var entity = await _repository.GetAsync(id);
        if (entity.IsSubmitted)
        {
            throw new BusinessException("MyERP:ProcessDeferredAccounting:CannotEditSubmitted", "Cannot edit a submitted Process Deferred Accounting.");
        }

        entity.CompanyId = input.CompanyId;
        entity.Type = input.Type;
        entity.AccountId = input.AccountId;
        entity.PostingDate = input.PostingDate.Date;
        entity.StartDate = input.StartDate.Date;
        entity.EndDate = input.EndDate.Date;
        entity.ValidateDates();

        await _repository.UpdateAsync(entity, autoSave: true);
        return await GetAsync(entity.Id);
    }

    [Authorize(MyERPPermissions.ProcessDeferredAccountings.Delete)]
    public async Task DeleteAsync(Guid id)
    {
        var entity = await _repository.GetAsync(id);
        if (entity.IsSubmitted)
        {
            throw new BusinessException("MyERP:ProcessDeferredAccounting:CannotDeleteSubmitted", "Cannot delete a submitted Process Deferred Accounting.");
        }

        await _repository.DeleteAsync(entity);
    }

    [Authorize(MyERPPermissions.ProcessDeferredAccountings.Submit)]
    public async Task<ProcessDeferredAccountingDto> SubmitAsync(Guid id)
    {
        var entity = await _repository.GetAsync(id);
        if (entity.IsSubmitted)
        {
            throw new BusinessException("MyERP:ProcessDeferredAccounting:AlreadySubmitted", "Process Deferred Accounting is already submitted.");
        }

        int processed = 0;
        if (entity.Type == DeferredAccountingType.Income)
        {
            processed = await _deferredAccountingService.ProcessDeferredRevenueAsync(entity.CompanyId, entity.EndDate, CurrentTenant.Id);
        }
        else
        {
            processed = await _deferredAccountingService.ProcessDeferredExpenseAsync(entity.CompanyId, entity.EndDate, CurrentTenant.Id);
        }

        entity.Submit(processed);
        await _repository.UpdateAsync(entity, autoSave: true);
        return await GetAsync(entity.Id);
    }

    [Authorize(MyERPPermissions.ProcessDeferredAccountings.Cancel)]
    public async Task<ProcessDeferredAccountingDto> CancelAsync(Guid id)
    {
        var entity = await _repository.GetAsync(id);
        entity.Cancel();
        await _repository.UpdateAsync(entity, autoSave: true);
        return await GetAsync(entity.Id);
    }

    /// <summary>
    /// Previews deferred revenue or expense recognitions for a given period without posting JEs (Gotcha #5995).
    /// </summary>
    [Authorize(MyERPPermissions.ProcessDeferredAccountings.Default)]
    public async Task<DeferredAccountingPreviewDto> PreviewDeferredAccountingAsync(PreviewDeferredAccountingInput input)
    {
        if (input.StartDate > input.EndDate)
        {
            throw new BusinessException(MyERPDomainErrorCodes.InvalidDateRange)
                .WithData("detail", "StartDate cannot be after EndDate.");
        }

        var result = new DeferredAccountingPreviewDto
        {
            CompanyId = input.CompanyId,
            Type = input.Type,
            StartDate = input.StartDate,
            EndDate = input.EndDate
        };

        var jeQuery = await _journalEntryRepository.GetQueryableAsync();
        var refType = input.Type == DeferredAccountingType.Income ? "DeferredRevenue" : "DeferredExpense";
        var existingDeferredJes = jeQuery
            .Where(je => je.CompanyId == input.CompanyId
                      && je.ReferenceType == refType
                      && je.Status == Core.DocumentStatus.Posted)
            .Select(je => je.PostingDate)
            .ToHashSet();

        if (input.Type == DeferredAccountingType.Income)
        {
            var siQuery = await _salesInvoiceRepository.GetQueryableAsync();
            var invoices = siQuery
                .Where(si => si.CompanyId == input.CompanyId && si.Status == Core.DocumentStatus.Posted)
                .ToList();

            var matchingInvoices = new System.Collections.Generic.HashSet<Guid>();

            foreach (var invoice in invoices)
            {
                var deferredItems = invoice.Items
                    .Where(i => i.EnableDeferredRevenue
                             && i.ServiceStartDate.HasValue
                             && i.ServiceEndDate.HasValue
                             && i.DeferredRevenueAccountId.HasValue
                             && (!input.AccountId.HasValue || i.DeferredRevenueAccountId == input.AccountId.Value))
                    .ToList();

                foreach (var item in deferredItems)
                {
                    var schedule = _deferredAccountingService.GenerateSchedule(item, input.EndDate);
                    foreach (var entry in schedule)
                    {
                        if (existingDeferredJes.Contains(entry.PostingDate)) continue;
                        if (entry.PostingDate > input.EndDate || entry.PostingDate < input.StartDate) continue;

                        result.Items.Add(new DeferredAccountingPreviewItemDto
                        {
                            InvoiceId = invoice.Id,
                            InvoiceNumber = invoice.InvoiceNumber,
                            ItemId = item.ItemId,
                            ItemDescription = item.Description,
                            ServiceStartDate = item.ServiceStartDate!.Value,
                            ServiceEndDate = item.ServiceEndDate!.Value,
                            TotalAmount = item.LineTotal,
                            AmountToRecognize = entry.Amount,
                            DeferredAccountId = item.DeferredRevenueAccountId!.Value,
                            PostingDate = entry.PostingDate
                        });

                        matchingInvoices.Add(invoice.Id);
                    }
                }
            }

            result.TotalInvoicesCount = matchingInvoices.Count;
            result.TotalAmountToRecognize = result.Items.Sum(i => i.AmountToRecognize);
        }
        else
        {
            var piQuery = await _purchaseInvoiceRepository.GetQueryableAsync();
            var invoices = piQuery
                .Where(pi => pi.CompanyId == input.CompanyId && pi.Status == Core.DocumentStatus.Posted)
                .ToList();

            var matchingInvoices = new System.Collections.Generic.HashSet<Guid>();

            foreach (var invoice in invoices)
            {
                var deferredItems = invoice.Items
                    .Where(i => i.EnableDeferredExpense
                             && i.ServiceStartDate.HasValue
                             && i.ServiceEndDate.HasValue
                             && i.DeferredExpenseAccountId.HasValue
                             && (!input.AccountId.HasValue || i.DeferredExpenseAccountId == input.AccountId.Value))
                    .ToList();

                foreach (var item in deferredItems)
                {
                    var schedule = _deferredAccountingService.GenerateExpenseSchedule(item, input.EndDate);
                    foreach (var entry in schedule)
                    {
                        if (existingDeferredJes.Contains(entry.PostingDate)) continue;
                        if (entry.PostingDate > input.EndDate || entry.PostingDate < input.StartDate) continue;

                        result.Items.Add(new DeferredAccountingPreviewItemDto
                        {
                            InvoiceId = invoice.Id,
                            InvoiceNumber = invoice.InvoiceNumber,
                            ItemId = item.ItemId,
                            ItemDescription = item.Description,
                            ServiceStartDate = item.ServiceStartDate!.Value,
                            ServiceEndDate = item.ServiceEndDate!.Value,
                            TotalAmount = item.LineTotal,
                            AmountToRecognize = entry.Amount,
                            DeferredAccountId = item.DeferredExpenseAccountId!.Value,
                            PostingDate = entry.PostingDate
                        });

                        matchingInvoices.Add(invoice.Id);
                    }
                }
            }

            result.TotalInvoicesCount = matchingInvoices.Count;
            result.TotalAmountToRecognize = result.Items.Sum(i => i.AmountToRecognize);
        }

        return result;
    }

    /// <summary>
    /// Retrieves summary metrics for a Process Deferred Accounting document (Gotcha #5995).
    /// </summary>
    [Authorize(MyERPPermissions.ProcessDeferredAccountings.Default)]
    public async Task<ProcessDeferredAccountingSummaryDto> GetSummaryAsync(Guid id)
    {
        var entity = await _repository.GetAsync(id);
        return new ProcessDeferredAccountingSummaryDto
        {
            Id = entity.Id,
            ProcessNumber = entity.ProcessNumber,
            IsSubmitted = entity.IsSubmitted,
            IsCancelled = entity.IsCancelled,
            EntriesProcessed = entity.EntriesProcessed
        };
    }
}
