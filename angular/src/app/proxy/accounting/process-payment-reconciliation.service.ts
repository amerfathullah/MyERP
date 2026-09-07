import type { CreateProcessPaymentReconciliationDto, ProcessPaymentReconciliationDto, ProcessPaymentReconciliationProgressDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';
import type { CompanyFilteredPagedRequestDto } from '../shared/models';

@Injectable({
  providedIn: 'root',
})
export class ProcessPaymentReconciliationService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  cancel = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ProcessPaymentReconciliationDto>({
      method: 'POST',
      url: `/api/app/process-payment-reconciliation/${id}/cancel`,
    },
    { apiName: this.apiName,...config });
  

  create = (input: CreateProcessPaymentReconciliationDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ProcessPaymentReconciliationDto>({
      method: 'POST',
      url: '/api/app/process-payment-reconciliation',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ProcessPaymentReconciliationDto>({
      method: 'GET',
      url: `/api/app/process-payment-reconciliation/${id}`,
    },
    { apiName: this.apiName,...config });
  

  getList = (input: CompanyFilteredPagedRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<ProcessPaymentReconciliationDto>>({
      method: 'GET',
      url: '/api/app/process-payment-reconciliation',
      params: { companyId: input.companyId, filter: input.filter, status: input.status, fromDate: input.fromDate, toDate: input.toDate, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  getProgress = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ProcessPaymentReconciliationProgressDto>({
      method: 'GET',
      url: `/api/app/process-payment-reconciliation/${id}/progress`,
    },
    { apiName: this.apiName,...config });
  

  pause = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ProcessPaymentReconciliationDto>({
      method: 'POST',
      url: `/api/app/process-payment-reconciliation/${id}/pause`,
    },
    { apiName: this.apiName,...config });
  

  resume = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ProcessPaymentReconciliationDto>({
      method: 'POST',
      url: `/api/app/process-payment-reconciliation/${id}/resume`,
    },
    { apiName: this.apiName,...config });
  

  submit = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ProcessPaymentReconciliationDto>({
      method: 'POST',
      url: `/api/app/process-payment-reconciliation/${id}/submit`,
    },
    { apiName: this.apiName,...config });
}