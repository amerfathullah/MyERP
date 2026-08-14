using System;
using Volo.Abp.Application.Dtos;

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

public class ExchangeRateResultDto
{
    public decimal Rate { get; set; }
    public string FromCurrency { get; set; } = null!;
    public string ToCurrency { get; set; } = null!;
    public DateTime? RateDate { get; set; }
}
