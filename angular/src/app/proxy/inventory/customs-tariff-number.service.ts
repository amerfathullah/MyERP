import type { CreateUpdateCustomsTariffNumberDto, CustomsTariffNumberDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedAndSortedResultRequestDto, PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class CustomsTariffNumberService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  create = (input: CreateUpdateCustomsTariffNumberDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, CustomsTariffNumberDto>({
      method: 'POST',
      url: '/api/app/customs-tariff-number',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/customs-tariff-number/${id}`,
    },
    { apiName: this.apiName,...config });
  

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, CustomsTariffNumberDto>({
      method: 'GET',
      url: `/api/app/customs-tariff-number/${id}`,
    },
    { apiName: this.apiName,...config });
  

  getList = (input: PagedAndSortedResultRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<CustomsTariffNumberDto>>({
      method: 'GET',
      url: '/api/app/customs-tariff-number',
      params: { sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  update = (id: string, input: CreateUpdateCustomsTariffNumberDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, CustomsTariffNumberDto>({
      method: 'PUT',
      url: `/api/app/customs-tariff-number/${id}`,
      body: input,
    },
    { apiName: this.apiName,...config });
}