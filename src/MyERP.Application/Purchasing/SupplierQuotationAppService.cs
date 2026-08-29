using System;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Core;
using MyERP.Core.DomainServices;
using MyERP.Purchasing.Entities;
using MyERP.Permissions;
using MyERP.Shared;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Purchasing;

[Authorize(MyERPPermissions.PurchaseOrders.Default)]
public class SupplierQuotationAppService : ApplicationService, ISupplierQuotationAppService
{
    private readonly IRepository<SupplierQuotation, Guid> _repository;
    private readonly IDocumentNumberGenerator _numberGenerator;

    public SupplierQuotationAppService(
        IRepository<SupplierQuotation, Guid> repository,
        IDocumentNumberGenerator numberGenerator)
    {
        _repository = repository;
        _numberGenerator = numberGenerator;
    }

    public async Task<PagedResultDto<SupplierQuotationDto>> GetListAsync(CompanyFilteredPagedRequestDto input)
    {
        var query = (await _repository.WithDetailsAsync()).AsQueryable();

        if (input.CompanyId.HasValue)
            query = query.Where(x => x.CompanyId == input.CompanyId.Value);

        if (!string.IsNullOrWhiteSpace(input.Filter))
        {
            var filter = input.Filter;
            query = query.Where(x => x.SupplierName != null && x.SupplierName.Contains(filter));
        }

        if (!string.IsNullOrWhiteSpace(input.Status) && Enum.TryParse<DocumentStatus>(input.Status, true, out var status))
            query = query.Where(x => x.Status == status);

        var totalCount = query.Count();
        var items = query.OrderByDescending(s => s.TransactionDate)
            .Skip(input.SkipCount).Take(input.MaxResultCount).ToList();
        return new PagedResultDto<SupplierQuotationDto>(totalCount, items.Select(x => ObjectMapper.Map<SupplierQuotation, SupplierQuotationDto>(x)).ToList());
    }

    public async Task<SupplierQuotationDto> GetAsync(Guid id)
    {
        var sq = (await _repository.WithDetailsAsync()).First(s => s.Id == id);
        return ObjectMapper.Map<SupplierQuotation, SupplierQuotationDto>(sq);
    }

    [Authorize(MyERPPermissions.PurchaseOrders.Create)]
    public async Task<SupplierQuotationDto> CreateAsync(CreateSupplierQuotationDto input)
    {
        // Valid till date cannot be before transaction date
        if (input.ValidTill.HasValue && input.ValidTill.Value.Date < input.TransactionDate.Date)
        {
            throw new Volo.Abp.BusinessException(MyERPDomainErrorCodes.InvalidDateRange);
        }

        // Supplier scorecard enforcement: prevent_rfqs blocks RFQ/SQ creation
        var supplierRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Supplier, Guid>>();
        var supplier = await supplierRepo.GetAsync(input.SupplierId);
        if (supplier.PreventRfqs)
        {
            throw new Volo.Abp.BusinessException("MyERP:04007")
                .WithData("supplierName", supplier.Name);
        }

        // Validate all items are active
        var itemValidation = LazyServiceProvider.LazyGetRequiredService<MyERP.Inventory.DomainServices.ItemTransactionValidationService>();
        await itemValidation.ValidateItemsForTransactionAsync(input.Items.Select(i => i.ItemId).ToArray());

        // Prevent duplicate supplier quotation against same RFQ (upstream PR #58377)
        if (input.RequestForQuotationId.HasValue)
        {
            var sqQuery = await _repository.GetQueryableAsync();
            var existingSq = sqQuery.FirstOrDefault(x =>
                x.SupplierId == input.SupplierId &&
                x.RequestForQuotationId == input.RequestForQuotationId.Value &&
                x.Status != DocumentStatus.Cancelled);

            if (existingSq != null)
            {
                var rfqRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<RequestForQuotation, Guid>>();
                var rfq = await rfqRepo.FindAsync(input.RequestForQuotationId.Value);
                throw new Volo.Abp.BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                    .WithData("detail", $"Supplier Quotation {existingSq.QuotationNumber} already exists against Request for Quotation {rfq?.RfqNumber ?? input.RequestForQuotationId.ToString()}.");
            }
        }

        var sq = new SupplierQuotation(GuidGenerator.Create(), input.CompanyId,
            input.SupplierId, input.TransactionDate, CurrentTenant.Id)
        {
            QuotationNumber = await _numberGenerator.GenerateAsync("SQ", input.CompanyId),
            SupplierName = input.SupplierName ?? supplier.Name,
            ValidTill = input.ValidTill,
            Currency = input.Currency ?? "MYR",
            RequestForQuotationId = input.RequestForQuotationId,
        };
        foreach (var item in input.Items)
            sq.AddItem(item.ItemId, item.Qty, item.Rate, item.ItemName);
        await _repository.InsertAsync(sq);
        return ObjectMapper.Map<SupplierQuotation, SupplierQuotationDto>(sq);
    }

    [Authorize(MyERPPermissions.PurchaseOrders.Submit)]
    public async Task<SupplierQuotationDto> SubmitAsync(Guid id)
    {
        var sq = (await _repository.WithDetailsAsync()).First(s => s.Id == id);
        sq.Submit();
        await _repository.UpdateAsync(sq);

        // If created from RFQ, update RFQ activity
        if (sq.RequestForQuotationId.HasValue)
        {
            var rfqRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<RequestForQuotation, Guid>>();
            var rfq = await rfqRepo.FindAsync(sq.RequestForQuotationId.Value);
            if (rfq != null)
            {
                var rfqSupplier = rfq.Suppliers.FirstOrDefault(s => s.SupplierId == sq.SupplierId);
                if (rfqSupplier != null)
                {
                    rfqSupplier.EmailSent = true;
                    await rfqRepo.UpdateAsync(rfq);
                }
            }
        }

        return ObjectMapper.Map<SupplierQuotation, SupplierQuotationDto>(sq);
    }

    [Authorize(MyERPPermissions.PurchaseOrders.Cancel)]
    public async Task<SupplierQuotationDto> CancelAsync(Guid id)
    {
        var sq = await _repository.GetAsync(id);
        sq.Cancel();
        await _repository.UpdateAsync(sq);
        return ObjectMapper.Map<SupplierQuotation, SupplierQuotationDto>(sq);
    }
}
