import type { CreatePurchaseReceiptDto, PurchaseReceiptDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedAndSortedResultRequestDto, PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class PurchaseReceiptService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  cancel = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PurchaseReceiptDto>({
      method: 'POST',
      url: `/api/app/purchase-receipt/${id}/cancel`,
    },
    { apiName: this.apiName,...config });
  

  create = (input: CreatePurchaseReceiptDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PurchaseReceiptDto>({
      method: 'POST',
      url: '/api/app/purchase-receipt',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PurchaseReceiptDto>({
      method: 'GET',
      url: `/api/app/purchase-receipt/${id}`,
    },
    { apiName: this.apiName,...config });
  

  getList = (input: PagedAndSortedResultRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<PurchaseReceiptDto>>({
      method: 'GET',
      url: '/api/app/purchase-receipt',
      params: { sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  submit = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PurchaseReceiptDto>({
      method: 'POST',
      url: `/api/app/purchase-receipt/${id}/submit`,
    },
    { apiName: this.apiName,...config });
}