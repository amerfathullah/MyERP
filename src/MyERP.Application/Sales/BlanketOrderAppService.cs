using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Core;
using MyERP.Core.DomainServices;
using MyERP.Sales.DomainServices;
using MyERP.Sales.Entities;
using MyERP.Permissions;
using MyERP.Shared;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Sales;

[Authorize(MyERPPermissions.SalesOrders.Default)]
public class BlanketOrderAppService : ApplicationService, IBlanketOrderAppService
{
    private readonly IRepository<BlanketOrder, Guid> _repository;

    public BlanketOrderAppService(IRepository<BlanketOrder, Guid> repository) => _repository = repository;

    public async Task<PagedResultDto<BlanketOrderDto>> GetListAsync(CompanyFilteredPagedRequestDto input)
    {
        var query = (await _repository.WithDetailsAsync()).AsQueryable();

        if (input.CompanyId.HasValue)
            query = query.Where(x => x.CompanyId == input.CompanyId.Value);

        if (!string.IsNullOrWhiteSpace(input.Filter))
        {
            var filter = input.Filter;
            query = query.Where(x => x.OrderNumber.Contains(filter)
                || (x.PartyName != null && x.PartyName.Contains(filter)));
        }

        if (!string.IsNullOrWhiteSpace(input.Status) && Enum.TryParse<DocumentStatus>(input.Status, true, out var status))
            query = query.Where(x => x.Status == status);

        var totalCount = query.Count();
        var items = query.OrderByDescending(b => b.CreationTime)
            .Skip(input.SkipCount).Take(input.MaxResultCount).ToList();
        return new PagedResultDto<BlanketOrderDto>(totalCount, items.Select(x => ObjectMapper.Map<BlanketOrder, BlanketOrderDto>(x)).ToList());
    }

    /// <summary>
    /// Returns active submitted Blanket Orders strictly scoped to the requested party
    /// (or the Customer converted from a Lead/Prospect). Per ERPNext queries.get_blanket_orders
    /// (commits a5a5cbbb05, 09fd286163): when no party is resolved, returns an empty list
    /// rather than offering another party's Blanket Orders.
    /// </summary>
    public async Task<List<BlanketOrderDto>> GetActiveForPartyAsync(GetActiveBlanketOrdersInput input)
    {
        if (input.CompanyId == Guid.Empty)
            return new List<BlanketOrderDto>();

        var orderType = string.IsNullOrWhiteSpace(input.OrderType) ? "Selling" : input.OrderType;
        Guid? effectivePartyId = input.PartyId.HasValue && input.PartyId.Value != Guid.Empty
            ? input.PartyId.Value
            : null;

        if (!effectivePartyId.HasValue && string.Equals(orderType, "Selling", StringComparison.OrdinalIgnoreCase))
        {
            var boRateService = LazyServiceProvider?.LazyGetService<BlanketOrderRateService>();
            if (boRateService != null)
            {
                effectivePartyId = await boRateService.ResolveCustomerIdAsync(
                    customerId: null, input.LeadId, input.ProspectId, input.CompanyId);
            }
        }

        if (!effectivePartyId.HasValue)
            return new List<BlanketOrderDto>();

        var txDate = (input.TransactionDate ?? DateTime.UtcNow).Date;
        var query = (await _repository.WithDetailsAsync()).AsQueryable()
            .Where(bo => bo.CompanyId == input.CompanyId
                && bo.Status == DocumentStatus.Submitted
                && bo.OrderType == orderType
                && bo.PartyId == effectivePartyId.Value
                && bo.FromDate.Date <= txDate
                && bo.ToDate.Date >= txDate);

        if (!string.IsNullOrWhiteSpace(input.Currency))
        {
            var currency = input.Currency.Trim().ToUpperInvariant();
            query = query.Where(bo => bo.Currency == currency);
        }

        var candidates = query.OrderByDescending(b => b.CreationTime).ToList();
        if (input.ItemId.HasValue && input.ItemId.Value != Guid.Empty)
        {
            var itemId = input.ItemId.Value;
            candidates = candidates
                .Where(bo => bo.Items.Any(i => i.ItemId == itemId && !i.IsClosed && i.RemainingQty > 0))
                .ToList();
        }

        return candidates.Select(x => ObjectMapper.Map<BlanketOrder, BlanketOrderDto>(x)).ToList();
    }

    public async Task<BlanketOrderDto> GetAsync(Guid id)
    {
        var bo = (await _repository.WithDetailsAsync()).First(b => b.Id == id);
        return ObjectMapper.Map<BlanketOrder, BlanketOrderDto>(bo);
    }

    [Authorize(MyERPPermissions.SalesOrders.Create)]
    public async Task<BlanketOrderDto> CreateAsync(CreateBlanketOrderDto input)
    {
        // ERPNext validate_dates: rejected at save, not only at submit.
        if (input.FromDate.Date > input.ToDate.Date)
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("detail", "From Date cannot be greater than To Date.");

        // Validate all items are active
        var itemValidation = LazyServiceProvider.LazyGetRequiredService<MyERP.Inventory.DomainServices.ItemTransactionValidationService>();
        var itemIds = input.Items.Select(i => i.ItemId).ToArray();
        await itemValidation.ValidateItemsForTransactionAsync(itemIds);

        // Company-restriction check: every other Selling/Purchasing document wires this in.
        // PartyId is a Customer for a Selling agreement, a Supplier for a Buying one.
        var companyRestriction = LazyServiceProvider.LazyGetRequiredService<MyERP.Core.DomainServices.CompanyRestrictionValidationService>();
        await companyRestriction.ValidateTransactionCompanyAsync(
            "BlanketOrder", input.CompanyId, itemIds: itemIds,
            customerIds: string.Equals(input.OrderType, "Selling", StringComparison.OrdinalIgnoreCase) ? new[] { input.PartyId } : null,
            supplierIds: string.Equals(input.OrderType, "Selling", StringComparison.OrdinalIgnoreCase) ? null : new[] { input.PartyId });

        var companyCurrency = "MYR";
        if (LazyServiceProvider != null)
        {
            var companyRepo = LazyServiceProvider.LazyGetService<IRepository<MyERP.Core.Entities.Company, Guid>>();
            var company = companyRepo != null ? await companyRepo.FindAsync(input.CompanyId) : null;
            if (!string.IsNullOrWhiteSpace(company?.CurrencyCode))
                companyCurrency = company.CurrencyCode;
        }

        var currency = string.IsNullOrWhiteSpace(input.Currency)
            ? companyCurrency
            : input.Currency.Trim().ToUpperInvariant();
        var exchangeRate = input.ExchangeRate;
        if (string.Equals(currency, companyCurrency, StringComparison.OrdinalIgnoreCase))
        {
            if (exchangeRate <= 0)
                exchangeRate = 1m;
        }
        else if (exchangeRate <= 0)
        {
            var exchangeService = LazyServiceProvider?.LazyGetService<MyERP.Accounting.DomainServices.CurrencyExchangeService>();
            exchangeRate = exchangeService != null
                ? await exchangeService.GetExchangeRateAsync(currency, companyCurrency, input.FromDate)
                : 1m;
            if (exchangeRate <= 0)
                exchangeRate = 1m;
        }

        var bo = new BlanketOrder(GuidGenerator.Create(), input.CompanyId,
            await GenerateOrderNumberAsync(input.CompanyId), input.OrderType,
            input.PartyId, input.FromDate, input.ToDate, CurrentTenant.Id)
        {
            PartyName = input.PartyName,
            Currency = currency,
            ExchangeRate = exchangeRate
        };
        foreach (var item in input.Items)
            bo.AddItem(item.ItemId, item.Qty, item.Rate, item.ItemName, stockUom: item.StockUom);
        await _repository.InsertAsync(bo);
        return ObjectMapper.Map<BlanketOrder, BlanketOrderDto>(bo);
    }

    [Authorize(MyERPPermissions.SalesOrders.Submit)]
    public async Task<BlanketOrderDto> SubmitAsync(Guid id)
    {
        var bo = (await _repository.WithDetailsAsync()).First(b => b.Id == id);
        bo.Submit();
        await _repository.UpdateAsync(bo);

        var activityRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<MyERP.Core.Entities.DocumentActivityLog, Guid>>();
        await activityRepo.InsertAsync(new MyERP.Core.Entities.DocumentActivityLog(
            GuidGenerator.Create(), "BlanketOrder", bo.Id, "Submitted",
            bo.CompanyId, bo.OrderNumber, "Draft", "Submitted",
            CurrentUser.Id, tenantId: bo.TenantId));

        return ObjectMapper.Map<BlanketOrder, BlanketOrderDto>(bo);
    }

    [Authorize(MyERPPermissions.SalesOrders.Cancel)]
    public async Task<BlanketOrderDto> CancelAsync(Guid id)
    {
        var bo = await _repository.GetAsync(id);
        bo.Cancel();
        await _repository.UpdateAsync(bo);

        var activityRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<MyERP.Core.Entities.DocumentActivityLog, Guid>>();
        await activityRepo.InsertAsync(new MyERP.Core.Entities.DocumentActivityLog(
            GuidGenerator.Create(), "BlanketOrder", bo.Id, "Cancelled",
            bo.CompanyId, bo.OrderNumber, "Submitted", "Cancelled",
            CurrentUser.Id, tenantId: bo.TenantId));

        return ObjectMapper.Map<BlanketOrder, BlanketOrderDto>(bo);
    }

    [Authorize(MyERPPermissions.SalesOrders.Edit)]
    public async Task<BlanketOrderDto> CloseAsync(Guid id)
    {
        var bo = await _repository.GetAsync(id);
        bo.Close();
        await _repository.UpdateAsync(bo);

        var activityRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<MyERP.Core.Entities.DocumentActivityLog, Guid>>();
        await activityRepo.InsertAsync(new MyERP.Core.Entities.DocumentActivityLog(
            GuidGenerator.Create(), "BlanketOrder", bo.Id, "Closed",
            bo.CompanyId, bo.OrderNumber, "Submitted", "Closed",
            CurrentUser.Id, tenantId: bo.TenantId));

        return ObjectMapper.Map<BlanketOrder, BlanketOrderDto>(bo);
    }

    [Authorize(MyERPPermissions.SalesOrders.Edit)]
    public async Task<BlanketOrderDto> ReopenAsync(Guid id)
    {
        var bo = await _repository.GetAsync(id);
        bo.Reopen();
        await _repository.UpdateAsync(bo);

        var activityRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<MyERP.Core.Entities.DocumentActivityLog, Guid>>();
        await activityRepo.InsertAsync(new MyERP.Core.Entities.DocumentActivityLog(
            GuidGenerator.Create(), "BlanketOrder", bo.Id, "Reopened",
            bo.CompanyId, bo.OrderNumber, "Closed", "Submitted",
            CurrentUser.Id, tenantId: bo.TenantId));

        return ObjectMapper.Map<BlanketOrder, BlanketOrderDto>(bo);
    }

    [Authorize(MyERPPermissions.SalesOrders.Edit)]
    public async Task<BlanketOrderDto> CloseItemAsync(Guid id, Guid itemId)
    {
        var bo = (await _repository.WithDetailsAsync()).First(b => b.Id == id);
        bo.CloseItem(itemId);
        await _repository.UpdateAsync(bo);
        return ObjectMapper.Map<BlanketOrder, BlanketOrderDto>(bo);
    }

    [Authorize(MyERPPermissions.SalesOrders.Edit)]
    public async Task<BlanketOrderDto> ReopenItemAsync(Guid id, Guid itemId)
    {
        var bo = (await _repository.WithDetailsAsync()).First(b => b.Id == id);
        bo.ReopenItem(itemId);
        await _repository.UpdateAsync(bo);
        return ObjectMapper.Map<BlanketOrder, BlanketOrderDto>(bo);
    }

    /// <summary>
    /// Uses the company's "BlanketOrder" number series when one is configured. No such series is
    /// seeded, so fall back to a timestamp number with a random suffix (the bare timestamp collided
    /// for two orders created within the same second).
    /// </summary>
    private async Task<string> GenerateOrderNumberAsync(Guid companyId)
    {
        try
        {
            return await LazyServiceProvider.LazyGetRequiredService<IDocumentNumberGenerator>()
                .GenerateAsync("BlanketOrder", companyId);
        }
        catch (BusinessException ex) when (ex.Code == MyERPDomainErrorCodes.DocumentSeriesNotConfigured)
        {
            return $"BO-{DateTime.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid().ToString("N")[..4].ToUpperInvariant()}";
        }
    }
}
