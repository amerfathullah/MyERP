import type { CreateSubcontractingOrderDto, CreateSubcontractingReceiptDto, GetScoListDto, SubcontractingOrderDto, SubcontractingReceiptDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class SubcontractingService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  cancelOrder = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, SubcontractingOrderDto>({
      method: 'POST',
      url: `/api/app/subcontracting/${id}/cancel-order`,
    },
    { apiName: this.apiName,...config });
  

  cancelReceipt = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, SubcontractingReceiptDto>({
      method: 'POST',
      url: `/api/app/subcontracting/${id}/cancel-receipt`,
    },
    { apiName: this.apiName,...config });
  

  createOrder = (input: CreateSubcontractingOrderDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, SubcontractingOrderDto>({
      method: 'POST',
      url: '/api/app/subcontracting/order',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  createReceipt = (input: CreateSubcontractingReceiptDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, SubcontractingReceiptDto>({
      method: 'POST',
      url: '/api/app/subcontracting/receipt',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  getOrder = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, SubcontractingOrderDto>({
      method: 'GET',
      url: `/api/app/subcontracting/${id}/order`,
    },
    { apiName: this.apiName,...config });
  

  getOrderList = (input: GetScoListDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<SubcontractingOrderDto>>({
      method: 'GET',
      url: '/api/app/subcontracting/order-list',
      params: { status: input.status, companyId: input.companyId, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  submitOrder = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, SubcontractingOrderDto>({
      method: 'POST',
      url: `/api/app/subcontracting/${id}/submit-order`,
    },
    { apiName: this.apiName,...config });
  

  submitReceipt = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, SubcontractingReceiptDto>({
      method: 'POST',
      url: `/api/app/subcontracting/${id}/submit-receipt`,
    },
    { apiName: this.apiName,...config });
}