import type { BlanketOrderDto, CreateBlanketOrderDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';
import type { CompanyFilteredPagedRequestDto } from '../shared/models';

@Injectable({
  providedIn: 'root',
})
export class BlanketOrderService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  cancel = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, BlanketOrderDto>({
      method: 'POST',
      url: `/api/app/blanket-order/${id}/cancel`,
    },
    { apiName: this.apiName,...config });
  

  create = (input: CreateBlanketOrderDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, BlanketOrderDto>({
      method: 'POST',
      url: '/api/app/blanket-order',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, BlanketOrderDto>({
      method: 'GET',
      url: `/api/app/blanket-order/${id}`,
    },
    { apiName: this.apiName,...config });
  

  getList = (input: CompanyFilteredPagedRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<BlanketOrderDto>>({
      method: 'GET',
      url: '/api/app/blanket-order',
      params: { companyId: input.companyId, filter: input.filter, status: input.status, fromDate: input.fromDate, toDate: input.toDate, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  submit = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, BlanketOrderDto>({
      method: 'POST',
      url: `/api/app/blanket-order/${id}/submit`,
    },
    { apiName: this.apiName,...config });
}