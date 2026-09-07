import type { CreateSubcontractingInwardOrderDto, MapSubcontractingInwardOrderFromSalesOrderDto, ScioReceiveItemsDto, SubcontractingInwardOrderActionSummaryDto, SubcontractingInwardOrderDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';
import type { CompanyFilteredPagedRequestDto } from '../shared/models';

@Injectable({
  providedIn: 'root',
})
export class SubcontractingInwardOrderService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  cancel = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, SubcontractingInwardOrderDto>({
      method: 'POST',
      url: `/api/app/subcontracting-inward-order/${id}/cancel`,
    },
    { apiName: this.apiName,...config });
  

  close = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, SubcontractingInwardOrderDto>({
      method: 'POST',
      url: `/api/app/subcontracting-inward-order/${id}/close`,
    },
    { apiName: this.apiName,...config });
  

  create = (input: CreateSubcontractingInwardOrderDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, SubcontractingInwardOrderDto>({
      method: 'POST',
      url: '/api/app/subcontracting-inward-order',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, SubcontractingInwardOrderDto>({
      method: 'GET',
      url: `/api/app/subcontracting-inward-order/${id}`,
    },
    { apiName: this.apiName,...config });
  

  getActionSummary = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, SubcontractingInwardOrderActionSummaryDto>({
      method: 'GET',
      url: `/api/app/subcontracting-inward-order/${id}/action-summary`,
    },
    { apiName: this.apiName,...config });
  

  getList = (input: CompanyFilteredPagedRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<SubcontractingInwardOrderDto>>({
      method: 'GET',
      url: '/api/app/subcontracting-inward-order',
      params: { companyId: input.companyId, filter: input.filter, status: input.status, fromDate: input.fromDate, toDate: input.toDate, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  mapFromSalesOrder = (input: MapSubcontractingInwardOrderFromSalesOrderDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, CreateSubcontractingInwardOrderDto>({
      method: 'POST',
      url: '/api/app/subcontracting-inward-order/map-from-sales-order',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  receiveItems = (id: string, input: ScioReceiveItemsDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, SubcontractingInwardOrderDto>({
      method: 'POST',
      url: `/api/app/subcontracting-inward-order/${id}/receive-items`,
      body: input,
    },
    { apiName: this.apiName,...config });
  

  reopen = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, SubcontractingInwardOrderDto>({
      method: 'POST',
      url: `/api/app/subcontracting-inward-order/${id}/reopen`,
    },
    { apiName: this.apiName,...config });
  

  submit = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, SubcontractingInwardOrderDto>({
      method: 'POST',
      url: `/api/app/subcontracting-inward-order/${id}/submit`,
    },
    { apiName: this.apiName,...config });
}