using System;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using MyERP.Accounting.Entities;
using MyERP.Permissions;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Accounting;

[Authorize(MyERPPermissions.CurrencyExchangeSettings.Default)]
public class CurrencyExchangeSettingsAppService : MyERPAppService, ICurrencyExchangeSettingsAppService
{
    private readonly IRepository<CurrencyExchangeSettings, Guid> _repository;
    private readonly IHttpClientFactory? _httpClientFactory;

    public CurrencyExchangeSettingsAppService(
        IRepository<CurrencyExchangeSettings, Guid> repository,
        IHttpClientFactory? httpClientFactory = null)
    {
        _repository = repository;
        _httpClientFactory = httpClientFactory;
    }

    public async Task<CurrencyExchangeSettingsDto> GetAsync()
    {
        var settings = (await _repository.GetQueryableAsync()).FirstOrDefault();
        if (settings == null)
        {
            settings = new CurrencyExchangeSettings(
                GuidGenerator.Create(),
                "frankfurter.dev",
                "https://api.frankfurter.dev/v1/{transaction_date}",
                null,
                null,
                false,
                false,
                CurrentTenant.Id);

            settings.AddResultKey(GuidGenerator.Create(), "rates");
            settings.AddResultKey(GuidGenerator.Create(), "{to_currency}");
            settings.AddParam(GuidGenerator.Create(), "base", "{from_currency}");
            settings.AddParam(GuidGenerator.Create(), "symbols", "{to_currency}");

            await _repository.InsertAsync(settings);
        }

        return new CurrencyExchangeSettingsMapper().Map(settings);
    }

    [Authorize(MyERPPermissions.CurrencyExchangeSettings.Edit)]
    public async Task<CurrencyExchangeSettingsDto> UpdateAsync(UpdateCurrencyExchangeSettingsDto input)
    {
        var settings = (await _repository.GetQueryableAsync()).FirstOrDefault();
        if (settings == null)
        {
            settings = new CurrencyExchangeSettings(
                GuidGenerator.Create(),
                input.ServiceProvider.Trim(),
                input.ApiEndpoint.Trim(),
                input.AccessKey?.Trim(),
                input.Url?.Trim(),
                input.UseHttp,
                input.Disabled,
                CurrentTenant.Id);
            await _repository.InsertAsync(settings);
        }
        else
        {
            settings.ServiceProvider = input.ServiceProvider.Trim();
            settings.ApiEndpoint = input.ApiEndpoint.Trim();
            settings.AccessKey = input.AccessKey?.Trim();
            settings.Url = input.Url?.Trim();
            settings.UseHttp = input.UseHttp;
            settings.Disabled = input.Disabled;
        }

        settings.ClearParamsAndResults();

        if (input.ReqParams != null)
        {
            foreach (var p in input.ReqParams)
            {
                settings.AddParam(GuidGenerator.Create(), p.Key.Trim(), p.Value.Trim());
            }
        }

        if (input.ResultKeys != null)
        {
            foreach (var r in input.ResultKeys)
            {
                settings.AddResultKey(GuidGenerator.Create(), r.Key.Trim());
            }
        }

        await _repository.UpdateAsync(settings);
        return new CurrencyExchangeSettingsMapper().Map(settings);
    }

    public async Task<TestCurrencyExchangeApiResponseDto> TestConnectionAsync(TestCurrencyExchangeApiRequestDto input)
    {
        var settings = (await _repository.GetQueryableAsync()).FirstOrDefault();
        if (settings == null)
        {
            return new TestCurrencyExchangeApiResponseDto
            {
                Success = false,
                ErrorMessage = "Settings not configured"
            };
        }

        var fromCurrency = string.IsNullOrWhiteSpace(input.FromCurrency) ? "USD" : input.FromCurrency.Trim().ToUpperInvariant();
        var toCurrency = string.IsNullOrWhiteSpace(input.ToCurrency) ? "MYR" : input.ToCurrency.Trim().ToUpperInvariant();
        var txDate = (input.TransactionDate ?? DateTime.UtcNow).ToString("yyyy-MM-dd");

        var endpoint = settings.ApiEndpoint
            .Replace("{transaction_date}", txDate)
            .Replace("{from_currency}", fromCurrency)
            .Replace("{to_currency}", toCurrency);

        var queryParams = string.Join("&", settings.ReqParams.Select(p =>
            $"{Uri.EscapeDataString(p.Key)}={Uri.EscapeDataString(p.Value.Replace("{transaction_date}", txDate).Replace("{from_currency}", fromCurrency).Replace("{to_currency}", toCurrency))}"));

        var fullUrl = queryParams.Length > 0 ? (endpoint.Contains('?') ? $"{endpoint}&{queryParams}" : $"{endpoint}?{queryParams}") : endpoint;

        try
        {
            using var client = _httpClientFactory?.CreateClient() ?? new HttpClient();
            var response = await client.GetAsync(fullUrl);
            var content = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                return new TestCurrencyExchangeApiResponseDto
                {
                    Success = false,
                    ResolvedUrl = fullUrl,
                    RawResponse = content,
                    ErrorMessage = $"HTTP {response.StatusCode}: {content}"
                };
            }

            using var doc = JsonDocument.Parse(content);
            JsonElement current = doc.RootElement;

            foreach (var rk in settings.ResultKeys)
            {
                var key = rk.Key
                    .Replace("{transaction_date}", txDate)
                    .Replace("{from_currency}", fromCurrency)
                    .Replace("{to_currency}", toCurrency);

                if (current.ValueKind == JsonValueKind.Object && current.TryGetProperty(key, out var next))
                {
                    current = next;
                }
                else
                {
                    return new TestCurrencyExchangeApiResponseDto
                    {
                        Success = false,
                        ResolvedUrl = fullUrl,
                        RawResponse = content,
                        ErrorMessage = $"Failed to find key '{key}' in JSON response."
                    };
                }
            }

            decimal rate = 0;
            if (current.ValueKind == JsonValueKind.Number && current.TryGetDecimal(out var decRate))
            {
                rate = decRate;
            }

            return new TestCurrencyExchangeApiResponseDto
            {
                Success = true,
                ResolvedUrl = fullUrl,
                RawResponse = content,
                ExchangeRate = rate
            };
        }
        catch (Exception ex)
        {
            return new TestCurrencyExchangeApiResponseDto
            {
                Success = false,
                ResolvedUrl = fullUrl,
                ErrorMessage = ex.Message
            };
        }
    }
}
