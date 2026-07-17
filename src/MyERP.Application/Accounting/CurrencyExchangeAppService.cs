using System;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Accounting.Entities;
using MyERP.Permissions;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Accounting;

public class CurrencyExchangeDto : EntityDto<Guid>
{
    public string FromCurrency { get; set; } = null!;
    public string ToCurrency { get; set; } = null!;
    public decimal ExchangeRate { get; set; }
    public DateTime Date { get; set; }
}

public class CreateCurrencyExchangeDto
{
    public string FromCurrency { get; set; } = null!;
    public string ToCurrency { get; set; } = null!;
    public decimal ExchangeRate { get; set; }
    public DateTime Date { get; set; }
}

[Authorize(MyERPPermissions.Accounts.Default)]
public class CurrencyExchangeAppService : ApplicationService
{
    private readonly IRepository<CurrencyExchange, Guid> _repository;
    public CurrencyExchangeAppService(IRepository<CurrencyExchange, Guid> repository) => _repository = repository;

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
        var ce = new CurrencyExchange(GuidGenerator.Create(), input.FromCurrency,
            input.ToCurrency, input.ExchangeRate, input.Date, CurrentTenant.Id);
        await _repository.InsertAsync(ce);
        return ObjectMapper.Map<CurrencyExchange, CurrencyExchangeDto>(ce);
    }

    [Authorize(MyERPPermissions.Accounts.Delete)]
    public async Task DeleteAsync(Guid id) => await _repository.DeleteAsync(id);


}
