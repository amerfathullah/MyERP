using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Volo.Abp.Application.Dtos;

namespace MyERP.Accounting;

public class CurrencyExchangeSettingsDetailDto : CreationAuditedEntityDto<Guid>
{
    public Guid SettingsId { get; set; }
    public string Key { get; set; } = null!;
    public string Value { get; set; } = null!;
}

public class CreateUpdateCurrencyExchangeSettingsDetailDto
{
    [Required]
    [StringLength(CurrencyExchangeSettingsConsts.MaxKeyLength)]
    public string Key { get; set; } = null!;

    [Required]
    [StringLength(CurrencyExchangeSettingsConsts.MaxValueLength)]
    public string Value { get; set; } = null!;
}

public class CurrencyExchangeSettingsResultDto : CreationAuditedEntityDto<Guid>
{
    public Guid SettingsId { get; set; }
    public string Key { get; set; } = null!;
}

public class CreateUpdateCurrencyExchangeSettingsResultDto
{
    [Required]
    [StringLength(CurrencyExchangeSettingsConsts.MaxKeyLength)]
    public string Key { get; set; } = null!;
}

public class CurrencyExchangeSettingsDto : FullAuditedEntityDto<Guid>
{
    public string ServiceProvider { get; set; } = null!;
    public string ApiEndpoint { get; set; } = null!;
    public string? AccessKey { get; set; }
    public string? Url { get; set; }
    public bool UseHttp { get; set; }
    public bool Disabled { get; set; }

    public List<CurrencyExchangeSettingsDetailDto> ReqParams { get; set; } = new();
    public List<CurrencyExchangeSettingsResultDto> ResultKeys { get; set; } = new();
}

public class UpdateCurrencyExchangeSettingsDto
{
    [Required]
    [StringLength(CurrencyExchangeSettingsConsts.MaxServiceProviderLength)]
    public string ServiceProvider { get; set; } = "frankfurter.dev";

    [Required]
    [StringLength(CurrencyExchangeSettingsConsts.MaxApiEndpointLength)]
    public string ApiEndpoint { get; set; } = "https://api.frankfurter.dev/v1/{transaction_date}";

    [StringLength(CurrencyExchangeSettingsConsts.MaxAccessKeyLength)]
    public string? AccessKey { get; set; }

    [StringLength(CurrencyExchangeSettingsConsts.MaxUrlLength)]
    public string? Url { get; set; }

    public bool UseHttp { get; set; }
    public bool Disabled { get; set; }

    public List<CreateUpdateCurrencyExchangeSettingsDetailDto> ReqParams { get; set; } = new();
    public List<CreateUpdateCurrencyExchangeSettingsResultDto> ResultKeys { get; set; } = new();
}

public class TestCurrencyExchangeApiRequestDto
{
    public string? FromCurrency { get; set; }
    public string? ToCurrency { get; set; }
    public DateTime? TransactionDate { get; set; }
}

public class TestCurrencyExchangeApiResponseDto
{
    public bool Success { get; set; }
    public decimal ExchangeRate { get; set; }
    public string? ResolvedUrl { get; set; }
    public string? RawResponse { get; set; }
    public string? ErrorMessage { get; set; }
}
