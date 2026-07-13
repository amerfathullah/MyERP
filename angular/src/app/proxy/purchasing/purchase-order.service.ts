import type { CreatePurchaseOrderDto, PurchaseOrderDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedAndSortedResultRequestDto, PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class PurchaseOrderService {
  private restService = inject(RestService);
  apiName = 'Default';


  cancel = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PurchaseOrderDto>({
      method: 'POST',
      url: `/api/app/purchase-order/${id}/cancel`,
    },
    { apiName: this.apiName,...config });


  create = (input: CreatePurchaseOrderDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PurchaseOrderDto>({
      method: 'POST',
      url: '/api/app/purchase-order',
      body: input,
    },
    { apiName: this.apiName,...config });


  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PurchaseOrderDto>({
      method: 'GET',
      url: `/api/app/purchase-order/${id}`,
    },
    { apiName: this.apiName,...config });


  getList = (input: PagedAndSortedResultRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<PurchaseOrderDto>>({
      method: 'GET',
      url: '/api/app/purchase-order',
      params: { sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });


  submit = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PurchaseOrderDto>({
      method: 'POST',
      url: `/api/app/purchase-order/${id}/submit`,
    },
    { apiName: this.apiName,...config });

  close = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PurchaseOrderDto>({
      method: 'POST',
      url: `/api/app/purchase-order/${id}/close`,
    },
    { apiName: this.apiName,...config });

  reopen = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PurchaseOrderDto>({
      method: 'POST',
      url: `/api/app/purchase-order/${id}/reopen`,
    },
    { apiName: this.apiName,...config });
}
