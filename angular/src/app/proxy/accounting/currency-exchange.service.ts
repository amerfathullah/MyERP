import type { CreateCurrencyExchangeDto, CurrencyExchangeDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedAndSortedResultRequestDto, PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class CurrencyExchangeService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  create = (input: CreateCurrencyExchangeDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, CurrencyExchangeDto>({
      method: 'POST',
      url: '/api/app/currency-exchange',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/currency-exchange/${id}`,
    },
    { apiName: this.apiName,...config });
  

  getList = (input: PagedAndSortedResultRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<CurrencyExchangeDto>>({
      method: 'GET',
      url: '/api/app/currency-exchange',
      params: { sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
}