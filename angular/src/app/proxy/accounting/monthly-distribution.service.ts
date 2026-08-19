import type { CreateUpdateMonthlyDistributionDto, MonthlyDistributionDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { ListResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class MonthlyDistributionService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  create = (input: CreateUpdateMonthlyDistributionDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, MonthlyDistributionDto>({
      method: 'POST',
      url: '/api/app/monthly-distribution',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/monthly-distribution/${id}`,
    },
    { apiName: this.apiName,...config });
  

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, MonthlyDistributionDto>({
      method: 'GET',
      url: `/api/app/monthly-distribution/${id}`,
    },
    { apiName: this.apiName,...config });
  

  getList = (config?: Partial<Rest.Config>) =>
    this.restService.request<any, ListResultDto<MonthlyDistributionDto>>({
      method: 'GET',
      url: '/api/app/monthly-distribution',
    },
    { apiName: this.apiName,...config });
  

  update = (id: string, input: CreateUpdateMonthlyDistributionDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, MonthlyDistributionDto>({
      method: 'PUT',
      url: `/api/app/monthly-distribution/${id}`,
      body: input,
    },
    { apiName: this.apiName,...config });
}