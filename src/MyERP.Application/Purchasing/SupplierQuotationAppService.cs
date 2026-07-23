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

public class SupplierQuotationDto : EntityDto<Guid>
{
    public Guid CompanyId { get; set; }
    public Guid SupplierId { get; set; }
    public string? SupplierName { get; set; }
    public string? QuotationNumber { get; set; }
    public DateTime TransactionDate { get; set; }
    public DateTime? ValidTill { get; set; }
    public string Currency { get; set; } = null!;
    public decimal NetTotal { get; set; }
    public decimal GrandTotal { get; set; }
    public int Status { get; set; }
    public SupplierQuotationItemDto[] Items { get; set; } = [];
}

public class SupplierQuotationItemDto
{
    public Guid Id { get; set; }
    public Guid ItemId { get; set; }
    public string? ItemName { get; set; }
    public decimal Qty { get; set; }
    public decimal Rate { get; set; }
    public decimal Amount { get; set; }
}

public class CreateSupplierQuotationDto
{
    public Guid CompanyId { get; set; }
    public Guid SupplierId { get; set; }
    public string? SupplierName { get; set; }
    public DateTime TransactionDate { get; set; }
    public DateTime? ValidTill { get; set; }
    public string Currency { get; set; } = "MYR";
    public Guid? RequestForQuotationId { get; set; }
    public CreateSQItemDto[] Items { get; set; } = [];
}

public class CreateSQItemDto
{
    public Guid ItemId { get; set; }
    public string? ItemName { get; set; }
    public decimal Qty { get; set; }
    public decimal Rate { get; set; }
}

[Authorize(MyERPPermissions.PurchaseOrders.Default)]
public class SupplierQuotationAppService : ApplicationService
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
        // Supplier scorecard enforcement: prevent_rfqs blocks RFQ/SQ creation
        var supplierRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Supplier, Guid>>();
        var supplier = await supplierRepo.GetAsync(input.SupplierId);
        if (supplier.PreventRfqs)
        {
            throw new Volo.Abp.BusinessException("MyERP:04007")
                .WithData("supplierName", supplier.Name);
        }

        var sq = new SupplierQuotation(GuidGenerator.Create(), input.CompanyId,
            input.SupplierId, input.TransactionDate, CurrentTenant.Id)
        {
            QuotationNumber = await _numberGenerator.GenerateAsync("SQ", input.CompanyId),
            SupplierName = input.SupplierName, ValidTill = input.ValidTill,
            Currency = input.Currency, RequestForQuotationId = input.RequestForQuotationId,
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

