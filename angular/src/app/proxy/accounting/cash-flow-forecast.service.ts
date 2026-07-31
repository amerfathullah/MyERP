import type { CashFlowForecastDto, CashFlowForecastRequestDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class CashFlowForecastService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  getForecast = (input: CashFlowForecastRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, CashFlowForecastDto>({
      method: 'GET',
      url: '/api/app/cash-flow-forecast/forecast',
      params: { companyId: input.companyId, asOfDate: input.asOfDate, forecastDays: input.forecastDays },
    },
    { apiName: this.apiName,...config });
}