import type { CreateSalesForecastDto, SalesForecastDto, UpdateSalesForecastDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

export interface GetSalesForecastListDto {
  companyId?: string;
  filter?: string;
  sorting?: string;
  skipCount?: number;
  maxResultCount?: number;
}

@Injectable({
  providedIn: 'root',
})
export class SalesForecastService {
  private restService = inject(RestService);
  apiName = 'Default';


  cancel = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, SalesForecastDto>({
      method: 'POST',
      url: `/api/app/sales-forecast/${id}/cancel`,
    },
    { apiName: this.apiName,...config });


  create = (input: CreateSalesForecastDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, SalesForecastDto>({
      method: 'POST',
      url: '/api/app/sales-forecast',
      body: input,
    },
    { apiName: this.apiName,...config });


  createMps = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, string>({
      method: 'POST',
      url: `/api/app/sales-forecast/${id}/create-mps`,
    },
    { apiName: this.apiName,...config });


  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/sales-forecast/${id}`,
    },
    { apiName: this.apiName,...config });


  generateDemand = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, SalesForecastDto>({
      method: 'POST',
      url: `/api/app/sales-forecast/${id}/generate-demand`,
    },
    { apiName: this.apiName,...config });


  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, SalesForecastDto>({
      method: 'GET',
      url: `/api/app/sales-forecast/${id}`,
    },
    { apiName: this.apiName,...config });


  getList = (input: GetSalesForecastListDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<SalesForecastDto>>({
      method: 'GET',
      url: '/api/app/sales-forecast',
      params: { companyId: input.companyId, filter: input.filter, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });


  submit = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, SalesForecastDto>({
      method: 'POST',
      url: `/api/app/sales-forecast/${id}/submit`,
    },
    { apiName: this.apiName,...config });


  update = (id: string, input: UpdateSalesForecastDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, SalesForecastDto>({
      method: 'PUT',
      url: `/api/app/sales-forecast/${id}`,
      body: input,
    },
    { apiName: this.apiName,...config });
}
