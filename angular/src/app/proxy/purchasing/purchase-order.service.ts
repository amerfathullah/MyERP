import type { CreatePurchaseOrderDto, PurchaseOrderDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';
import type { CompanyFilteredPagedRequestDto } from '../shared/models';

@Injectable({
  providedIn: 'root',
})
export class PurchaseOrderService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  amend = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PurchaseOrderDto>({
      method: 'POST',
      url: `/api/app/purchase-order/${id}/amend`,
    },
    { apiName: this.apiName,...config });
  

  cancel = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PurchaseOrderDto>({
      method: 'POST',
      url: `/api/app/purchase-order/${id}/cancel`,
    },
    { apiName: this.apiName,...config });
  

  bulkSubmit = (ids: string[], config?: Partial<Rest.Config>) =>
    this.restService.request<any, any>({
      method: 'POST',
      url: '/api/app/purchase-order/bulk-submit',
      body: ids,
    },
    { apiName: this.apiName,...config });
  

  close = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PurchaseOrderDto>({
      method: 'POST',
      url: `/api/app/purchase-order/${id}/close`,
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
  

  getList = (input: CompanyFilteredPagedRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<PurchaseOrderDto>>({
      method: 'GET',
      url: '/api/app/purchase-order',
      params: { companyId: input.companyId, filter: input.filter, status: input.status, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  reopen = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PurchaseOrderDto>({
      method: 'POST',
      url: `/api/app/purchase-order/${id}/reopen`,
    },
    { apiName: this.apiName,...config });
  

  submit = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PurchaseOrderDto>({
      method: 'POST',
      url: `/api/app/purchase-order/${id}/submit`,
    },
    { apiName: this.apiName,...config });

  update = (id: string, input: CreatePurchaseOrderDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PurchaseOrderDto>({
      method: 'PUT',
      url: `/api/app/purchase-order/${id}`,
      body: input,
    },
    { apiName: this.apiName,...config });

  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/purchase-order/${id}`,
    },
    { apiName: this.apiName,...config });
}