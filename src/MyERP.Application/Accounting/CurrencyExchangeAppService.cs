using System;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Accounting.DomainServices;
using MyERP.Accounting.Entities;
using MyERP.Permissions;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Accounting;

[Authorize(MyERPPermissions.Accounts.Default)]
public class CurrencyExchangeAppService : ApplicationService, ICurrencyExchangeAppService
{
    private readonly IRepository<CurrencyExchange, Guid> _repository;
    private readonly CurrencyExchangeService _exchangeService;

    public CurrencyExchangeAppService(
        IRepository<CurrencyExchange, Guid> repository,
        CurrencyExchangeService exchangeService)
    {
        _repository = repository;
        _exchangeService = exchangeService;
    }

    public async Task<PagedResultDto<CurrencyExchangeDto>> GetListAsync(PagedAndSortedResultRequestDto input)
    {
        var query = await _repository.GetQueryableAsync();
        var totalCount = query.Count();
        var items = query.OrderByDescending(c => c.Date)
            .Skip(input.SkipCount).Take(input.MaxResultCount).ToList();
        return new PagedResultDto<CurrencyExchangeDto>(totalCount, items.Select(ObjectMapper.Map<CurrencyExchange, CurrencyExchangeDto>).ToList());
    }

    [Authorize(MyERPPermissions.Accounts.Create)]
    public async Task<CurrencyExchangeDto> CreateAsync(CreateCurrencyExchangeDto input)
    {
        if (input.ExchangeRate <= 0)
        {
            throw new Volo.Abp.BusinessException(MyERPDomainErrorCodes.AmountMustBePositive)
                .WithData("field", "ExchangeRate");
        }

        var ce = new CurrencyExchange(GuidGenerator.Create(), input.FromCurrency,
            input.ToCurrency, input.ExchangeRate, input.Date, CurrentTenant.Id);
        await _repository.InsertAsync(ce);

        var activityLogRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Core.Entities.DocumentActivityLog, Guid>>();
        await activityLogRepo.InsertAsync(new Core.Entities.DocumentActivityLog(
            GuidGenerator.Create(), "CurrencyExchange", ce.Id,
            "Created", Guid.Empty,
            $"{ce.FromCurrency}->{ce.ToCurrency}", "Draft", "Active",
            CurrentUser.Id,
            $"Currency exchange rate {ce.FromCurrency}->{ce.ToCurrency} set to {ce.ExchangeRate}", CurrentTenant.Id));

        return ObjectMapper.Map<CurrencyExchange, CurrencyExchangeDto>(ce);
    }

    [Authorize(MyERPPermissions.Accounts.Delete)]
    public async Task DeleteAsync(Guid id) => await _repository.DeleteAsync(id);

    /// <summary>
    /// Gets the exchange rate for a currency pair on a given date.
    /// Per ERPNext: checks local Currency Exchange table first, then falls back to external API.
    /// Returns 1.0 for same-currency pairs.
    /// Called by transaction forms when currency changes to auto-fill exchange rate.
    /// </summary>
    public async Task<ExchangeRateResultDto> GetRateAsync(string fromCurrency, string toCurrency, DateTime? transactionDate = null)
    {
        if (string.Equals(fromCurrency, toCurrency, StringComparison.OrdinalIgnoreCase))
            return new ExchangeRateResultDto { Rate = 1m, FromCurrency = fromCurrency, ToCurrency = toCurrency };

        var date = transactionDate ?? DateTime.UtcNow.Date;
        var rate = await _exchangeService.GetExchangeRateAsync(fromCurrency, toCurrency, date);
        return new ExchangeRateResultDto
        {
            Rate = rate,
            FromCurrency = fromCurrency,
            ToCurrency = toCurrency,
            RateDate = date,
        };
    }
}
