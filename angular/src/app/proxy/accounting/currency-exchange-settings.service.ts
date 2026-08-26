import type {
  CurrencyExchangeSettingsDto,
  UpdateCurrencyExchangeSettingsDto,
  TestCurrencyExchangeApiRequestDto,
  TestCurrencyExchangeApiResponseDto
} from './models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class CurrencyExchangeSettingsService {
  private restService = inject(RestService);
  apiName = 'Default';

  get = (config?: Partial<Rest.Config>) =>
    this.restService.request<any, CurrencyExchangeSettingsDto>({
      method: 'GET',
      url: '/api/app/currency-exchange-settings',
    },
    { apiName: this.apiName, ...config });

  update = (input: UpdateCurrencyExchangeSettingsDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, CurrencyExchangeSettingsDto>({
      method: 'PUT',
      url: '/api/app/currency-exchange-settings',
      body: input,
    },
    { apiName: this.apiName, ...config });

  testConnection = (input: TestCurrencyExchangeApiRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, TestCurrencyExchangeApiResponseDto>({
      method: 'POST',
      url: '/api/app/currency-exchange-settings/test-connection',
      body: input,
    },
    { apiName: this.apiName, ...config });
}
