using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Core;
using MyERP.Core.DomainServices;
using MyERP.Permissions;
using MyERP.Purchasing.Entities;
using MyERP.Shared;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Purchasing;

[Authorize(MyERPPermissions.PurchaseOrders.Default)]
public class RequestForQuotationAppService : ApplicationService
{
    private readonly IRepository<RequestForQuotation, Guid> _repository;
    private readonly IRepository<Supplier, Guid> _supplierRepository;
    private readonly IDocumentNumberGenerator _numberGenerator;

    public RequestForQuotationAppService(
        IRepository<RequestForQuotation, Guid> repository,
        IRepository<Supplier, Guid> supplierRepository,
        IDocumentNumberGenerator numberGenerator)
    {
        _repository = repository;
        _supplierRepository = supplierRepository;
        _numberGenerator = numberGenerator;
    }

    public async Task<RfqDto> GetAsync(Guid id)
    {
        var rfq = await _repository.GetAsync(id);
        return ObjectMapper.Map<RequestForQuotation, RfqDto>(rfq);
    }

    public async Task<PagedResultDto<RfqDto>> GetListAsync(CompanyFilteredPagedRequestDto input)
    {
        var query = await _repository.GetQueryableAsync();

        if (input.CompanyId.HasValue)
            query = query.Where(x => x.CompanyId == input.CompanyId.Value);
        if (!string.IsNullOrWhiteSpace(input.Filter))
        {
            var f = input.Filter;
            query = query.Where(x => x.RfqNumber.ToLower().Contains(f));
        }

        if (!string.IsNullOrWhiteSpace(input.Status) && Enum.TryParse<DocumentStatus>(input.Status, true, out var status))
            query = query.Where(x => x.Status == status);

        var count = query.Count();
        var list = query.OrderByDescending(x => x.TransactionDate)
            .Skip(input.SkipCount).Take(input.MaxResultCount).ToList();

        return new PagedResultDto<RfqDto>(count, list.Select(x => ObjectMapper.Map<RequestForQuotation, RfqDto>(x)).ToList());
    }

    [Authorize(MyERPPermissions.PurchaseOrders.Create)]
    public async Task<RfqDto> CreateAsync(CreateRfqDto input)
    {
        var rfqNumber = await _numberGenerator.GenerateAsync("RFQ", input.CompanyId);
        var rfq = new RequestForQuotation(GuidGenerator.Create(), input.CompanyId, rfqNumber, input.TransactionDate, CurrentTenant.Id);
        rfq.CurrencyCode = input.CurrencyCode ?? "MYR";
        rfq.MessageForSupplier = input.MessageForSupplier;

        // Validate all items are active
        var itemValidation = LazyServiceProvider.LazyGetRequiredService<MyERP.Inventory.DomainServices.ItemTransactionValidationService>();
        await itemValidation.ValidateItemsForTransactionAsync(input.Items.Select(i => i.ItemId).ToArray());

        foreach (var item in input.Items)
            rfq.AddItem(item.ItemId, item.Description, item.Qty, item.Uom);

        foreach (var supplier in input.Suppliers)
        {
            // Validate supplier scorecard: prevent_rfqs blocks
            var supplierEntity = await _supplierRepository.GetAsync(supplier.SupplierId);
            if (supplierEntity.PreventRfqs)
                throw new Volo.Abp.BusinessException(MyERPDomainErrorCodes.ScorecardBlockedRFQ)
                    .WithData("supplierName", supplierEntity.Name);

            rfq.AddSupplier(supplier.SupplierId, supplierEntity.Name, supplier.Email);
        }

        await _repository.InsertAsync(rfq, autoSave: true);
        return ObjectMapper.Map<RequestForQuotation, RfqDto>(rfq);
    }

    [Authorize(MyERPPermissions.PurchaseOrders.Edit)]
    public async Task<RfqDto> SubmitAsync(Guid id)
    {
        var rfq = await _repository.GetAsync(id);
        rfq.Submit();
        await _repository.UpdateAsync(rfq, autoSave: true);
        return ObjectMapper.Map<RequestForQuotation, RfqDto>(rfq);
    }

    [Authorize(MyERPPermissions.PurchaseOrders.Edit)]
    public async Task<RfqDto> CancelAsync(Guid id)
    {
        var rfq = await _repository.GetAsync(id);
        rfq.Cancel();
        await _repository.UpdateAsync(rfq, autoSave: true);
        return ObjectMapper.Map<RequestForQuotation, RfqDto>(rfq);
    }
}

public class RfqDto
{
    public Guid Id { get; set; }
    public Guid CompanyId { get; set; }
    public string RfqNumber { get; set; } = null!;
    public DateTime TransactionDate { get; set; }
    public string CurrencyCode { get; set; } = null!;
    public string? MessageForSupplier { get; set; }
    public string Status { get; set; } = null!;
    public List<RfqItemDto> Items { get; set; } = new();
    public List<RfqSupplierDto> Suppliers { get; set; } = new();
}

public class RfqItemDto
{
    public Guid Id { get; set; }
    public Guid ItemId { get; set; }
    public string Description { get; set; } = null!;
    public decimal Qty { get; set; }
    public string Uom { get; set; } = null!;
}

public class RfqSupplierDto
{
    public Guid Id { get; set; }
    public Guid SupplierId { get; set; }
    public string SupplierName { get; set; } = null!;
    public string? Email { get; set; }
    public bool EmailSent { get; set; }
    public string QuoteStatus { get; set; } = null!;
}

public class CreateRfqDto
{
    public Guid CompanyId { get; set; }
    public DateTime TransactionDate { get; set; }
    public string? CurrencyCode { get; set; }
    public string? MessageForSupplier { get; set; }
    public List<CreateRfqItemDto> Items { get; set; } = new();
    public List<CreateRfqSupplierDto> Suppliers { get; set; } = new();
}

public class CreateRfqItemDto
{
    public Guid ItemId { get; set; }
    public string Description { get; set; } = null!;
    public decimal Qty { get; set; }
    public string Uom { get; set; } = "Unit";
}

public class CreateRfqSupplierDto
{
    public Guid SupplierId { get; set; }
    public string? Email { get; set; }
}

