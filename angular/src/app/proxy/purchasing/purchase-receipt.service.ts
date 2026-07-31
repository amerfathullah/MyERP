import type { CreatePurchaseReceiptDto, PurchaseReceiptDto, PutawayAllocationResultDto, PutawayItemInput } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';
import type { BulkOperationResultDto, CompanyFilteredPagedRequestDto } from '../shared/models';

@Injectable({
  providedIn: 'root',
})
export class PurchaseReceiptService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  amend = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PurchaseReceiptDto>({
      method: 'POST',
      url: `/api/app/purchase-receipt/${id}/amend`,
    },
    { apiName: this.apiName,...config });
  

  bulkSubmit = (ids: string[], config?: Partial<Rest.Config>) =>
    this.restService.request<any, BulkOperationResultDto>({
      method: 'POST',
      url: '/api/app/purchase-receipt/bulk-submit',
      body: ids,
    },
    { apiName: this.apiName,...config });
  

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
  

  getList = (input: CompanyFilteredPagedRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<PurchaseReceiptDto>>({
      method: 'GET',
      url: '/api/app/purchase-receipt',
      params: { companyId: input.companyId, filter: input.filter, status: input.status, fromDate: input.fromDate, toDate: input.toDate, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  getPutawayAllocations = (companyId: string, items: PutawayItemInput[], config?: Partial<Rest.Config>) =>
    this.restService.request<any, PutawayAllocationResultDto[]>({
      method: 'GET',
      url: `/api/app/purchase-receipt/putaway-allocations/${companyId}`,
      params: { items },
    },
    { apiName: this.apiName,...config });
  

  submit = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PurchaseReceiptDto>({
      method: 'POST',
      url: `/api/app/purchase-receipt/${id}/submit`,
    },
    { apiName: this.apiName,...config });
  

  update = (id: string, input: CreatePurchaseReceiptDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PurchaseReceiptDto>({
      method: 'PUT',
      url: `/api/app/purchase-receipt/${id}`,
      body: input,
    },
    { apiName: this.apiName,...config });
}