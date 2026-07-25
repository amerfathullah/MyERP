using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Core.DomainServices;
using MyERP.Permissions;
using MyERP.Sales.Entities;
using MyERP.Shared;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Sales;

[Authorize(MyERPPermissions.Quotations.Default)]
public class QuotationAppService : ApplicationService, IQuotationAppService
{
    private readonly IRepository<Quotation, Guid> _repository;
    private readonly IRepository<Customer, Guid> _customerRepository;
    private readonly IDocumentNumberGenerator _numberGenerator;

    public QuotationAppService(
        IRepository<Quotation, Guid> repository,
        IRepository<Customer, Guid> customerRepository,
        IDocumentNumberGenerator numberGenerator)
    {
        _repository = repository;
        _customerRepository = customerRepository;
        _numberGenerator = numberGenerator;
    }

    private async Task<string?> ResolveCustomerNameAsync(Guid customerId)
    {
        var customer = await _customerRepository.FindAsync(customerId);
        return customer?.Name;
    }

    public async Task<QuotationDto> GetAsync(Guid id)
    {
        var quotation = await _repository.GetAsync(id);
        var dto = ObjectMapper.Map<Quotation, QuotationDto>(quotation);
        dto.CustomerName = await ResolveCustomerNameAsync(quotation.CustomerId);
        return dto;
    }

    public async Task<PagedResultDto<QuotationDto>> GetListAsync(CompanyFilteredPagedRequestDto input)
    {
        var query = await _repository.GetQueryableAsync();

        if (input.CompanyId.HasValue)
            query = query.Where(x => x.CompanyId == input.CompanyId.Value);

        if (!string.IsNullOrWhiteSpace(input.Filter))
        {
            var filter = input.Filter; query = query.Where(x => x.QuotationNumber.Contains(filter));
        }

        if (!string.IsNullOrWhiteSpace(input.Status) && Enum.TryParse<Core.DocumentStatus>(input.Status, true, out var status))
            query = query.Where(x => x.Status == status);

        if (input.FromDate.HasValue)
            query = query.Where(x => x.IssueDate >= input.FromDate.Value);

        if (input.ToDate.HasValue)
            query = query.Where(x => x.IssueDate <= input.ToDate.Value);

        var totalCount = query.Count();
        var sorted = SortingHelper.ApplySorting(query, input.Sorting,
            q => q.OrderByDescending(x => x.IssueDate),
            ("quotationNumber", x => (object)(x.QuotationNumber ?? string.Empty)),
            ("issueDate", x => x.IssueDate),
            ("grandTotal", x => x.GrandTotal),
            ("status", x => x.Status));
        var quotations = sorted
            .Skip(input.SkipCount)
            .Take(input.MaxResultCount)
            .ToList();

        var customerIds = quotations.Select(q => q.CustomerId).Distinct().ToArray();
        var customers = (await _customerRepository.GetQueryableAsync())
            .Where(c => customerIds.Contains(c.Id))
            .Select(c => new { c.Id, c.Name })
            .ToDictionary(c => c.Id, c => c.Name);

        var dtos = new List<QuotationDto>();
        foreach (var q in quotations)
        {
            var dto = ObjectMapper.Map<Quotation, QuotationDto>(q);
            dto.CustomerName = customers.GetValueOrDefault(q.CustomerId);
            dtos.Add(dto);
        }

        return new PagedResultDto<QuotationDto>(totalCount, dtos);
    }

    [Authorize(MyERPPermissions.Quotations.Create)]
    public async Task<QuotationDto> CreateAsync(CreateQuotationDto input)
    {
        var quotationNumber = await _numberGenerator.GenerateAsync("Quotation", input.CompanyId);

        var quotation = new Quotation(
            GuidGenerator.Create(),
            input.CompanyId,
            input.CustomerId,
            quotationNumber,
            input.IssueDate);

        quotation.ValidUntil = input.ValidUntil;
        quotation.CurrencyCode = input.CurrencyCode;
        quotation.Terms = input.Terms;
        quotation.Notes = input.Notes;

        // Validate all items are active (per DO-NOT: disabled items must not appear in transactions)
        var itemValidation = LazyServiceProvider.LazyGetRequiredService<MyERP.Inventory.DomainServices.ItemTransactionValidationService>();
        await itemValidation.ValidateItemsForTransactionAsync(input.Items.Select(i => i.ItemId).ToArray());

        foreach (var item in input.Items)
        {
            quotation.AddItem(item.ItemId, item.Description, item.Quantity, item.UnitPrice, item.TaxAmount, item.Uom);
        }

        await _repository.InsertAsync(quotation, autoSave: true);
        var createDto = ObjectMapper.Map<Quotation, QuotationDto>(quotation);
        createDto.CustomerName = await ResolveCustomerNameAsync(quotation.CustomerId);
        return createDto;
    }

    [Authorize(MyERPPermissions.Quotations.Edit)]
    public async Task<QuotationDto> UpdateAsync(Guid id, CreateQuotationDto input)
    {
        var quotation = await _repository.GetAsync(id);
        if (quotation.Status != Core.DocumentStatus.Draft)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);

        quotation.ValidUntil = input.ValidUntil;
        quotation.CurrencyCode = input.CurrencyCode;
        quotation.Terms = input.Terms;
        quotation.Notes = input.Notes;

        // Replace items
        quotation.ClearItems();
        foreach (var item in input.Items)
        {
            quotation.AddItem(item.ItemId, item.Description, item.Quantity, item.UnitPrice, item.TaxAmount, item.Uom);
        }

        await _repository.UpdateAsync(quotation, autoSave: true);
        var dto = ObjectMapper.Map<Quotation, QuotationDto>(quotation);
        dto.CustomerName = await ResolveCustomerNameAsync(quotation.CustomerId);
        return dto;
    }

    [Authorize(MyERPPermissions.Quotations.Submit)]
    public async Task<QuotationDto> SubmitAsync(Guid id)
    {
        var quotation = await _repository.GetAsync(id);
        quotation.Submit();
        await _repository.UpdateAsync(quotation, autoSave: true);

        var activityRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<MyERP.Core.Entities.DocumentActivityLog, Guid>>();
        await activityRepo.InsertAsync(new MyERP.Core.Entities.DocumentActivityLog(
            GuidGenerator.Create(), "Quotation", quotation.Id, "Submitted",
            quotation.CompanyId, quotation.QuotationNumber, "Draft", "Submitted",
            CurrentUser.Id, tenantId: quotation.TenantId));

        var submitDto = ObjectMapper.Map<Quotation, QuotationDto>(quotation);
        submitDto.CustomerName = await ResolveCustomerNameAsync(quotation.CustomerId);
        return submitDto;
    }

    [Authorize(MyERPPermissions.Quotations.Cancel)]
    public async Task<QuotationDto> CancelAsync(Guid id)
    {
        var quotation = await _repository.GetAsync(id);
        quotation.Cancel();
        await _repository.UpdateAsync(quotation, autoSave: true);

        var activityRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<MyERP.Core.Entities.DocumentActivityLog, Guid>>();
        await activityRepo.InsertAsync(new MyERP.Core.Entities.DocumentActivityLog(
            GuidGenerator.Create(), "Quotation", quotation.Id, "Cancelled",
            quotation.CompanyId, quotation.QuotationNumber, "Submitted", "Cancelled",
            CurrentUser.Id, tenantId: quotation.TenantId));

        var cancelDto = ObjectMapper.Map<Quotation, QuotationDto>(quotation);
        cancelDto.CustomerName = await ResolveCustomerNameAsync(quotation.CustomerId);
        return cancelDto;
    }

    [Authorize(MyERPPermissions.Quotations.Edit)]
    public async Task<QuotationDto> MarkLostAsync(Guid id)
    {
        var quotation = await _repository.GetAsync(id);
        quotation.MarkLost();
        await _repository.UpdateAsync(quotation, autoSave: true);
        var lostDto = ObjectMapper.Map<Quotation, QuotationDto>(quotation);
        lostDto.CustomerName = await ResolveCustomerNameAsync(quotation.CustomerId);
        return lostDto;
    }
    /// Amend a cancelled or rejected quotation — creates a new draft copy for revision.
    /// </summary>
    [Authorize(MyERPPermissions.Quotations.Create)]
    public async Task<QuotationDto> AmendAsync(Guid id)
    {
        var original = await _repository.GetAsync(id);
        var amendService = LazyServiceProvider.LazyGetRequiredService<Core.DomainServices.DocumentAmendmentService>();

        amendService.ValidateCanAmend(original.Status);
        var newNumber = amendService.GenerateAmendedNumber(original.QuotationNumber, original.AmendmentIndex + 1);

        var amended = new Quotation(
            GuidGenerator.Create(), original.CompanyId, original.CustomerId,
            newNumber, DateTime.UtcNow.Date);

        amended.AmendedFromId = original.Id;
        amended.AmendmentIndex = original.AmendmentIndex + 1;
        amended.CurrencyCode = original.CurrencyCode;
        amended.ValidUntil = DateTime.UtcNow.Date.AddDays(30); // Fresh validity
        amended.Terms = original.Terms;
        amended.Notes = original.Notes;

        foreach (var item in original.Items)
        {
            amended.AddItem(item.ItemId, item.Description, item.Quantity, item.UnitPrice, item.TaxAmount, item.Uom);
        }

        await _repository.InsertAsync(amended, autoSave: true);
        var amendDto = ObjectMapper.Map<Quotation, QuotationDto>(amended);
        amendDto.CustomerName = await ResolveCustomerNameAsync(amended.CustomerId);
        return amendDto;
    }
}

