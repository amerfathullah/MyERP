import type { CreateUnreconcilePaymentDto, GetUnreconcilePaymentListDto, UnreconcilePaymentDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class UnreconcilePaymentService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  cancel = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, UnreconcilePaymentDto>({
      method: 'POST',
      url: `/api/app/unreconcile-payment/${id}/cancel`,
    },
    { apiName: this.apiName,...config });
  

  create = (input: CreateUnreconcilePaymentDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, UnreconcilePaymentDto>({
      method: 'POST',
      url: '/api/app/unreconcile-payment',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, UnreconcilePaymentDto>({
      method: 'GET',
      url: `/api/app/unreconcile-payment/${id}`,
    },
    { apiName: this.apiName,...config });
  

  getList = (input: GetUnreconcilePaymentListDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<UnreconcilePaymentDto>>({
      method: 'GET',
      url: '/api/app/unreconcile-payment',
      params: { companyId: input.companyId, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  submit = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, UnreconcilePaymentDto>({
      method: 'POST',
      url: `/api/app/unreconcile-payment/${id}/submit`,
    },
    { apiName: this.apiName,...config });
}