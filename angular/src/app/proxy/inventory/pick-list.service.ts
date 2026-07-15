import type { CreatePickListDto, PendingTransferDto, PickAllocationResultDto, PickListDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';
import type { CompanyFilteredPagedRequestDto } from '../shared/models';

@Injectable({
  providedIn: 'root',
})
export class PickListService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  allocateStock = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PickAllocationResultDto>({
      method: 'POST',
      url: `/api/app/pick-list/${id}/allocate-stock`,
    },
    { apiName: this.apiName,...config });
  

  cancel = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PickListDto>({
      method: 'POST',
      url: `/api/app/pick-list/${id}/cancel`,
    },
    { apiName: this.apiName,...config });
  

  create = (input: CreatePickListDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PickListDto>({
      method: 'POST',
      url: '/api/app/pick-list',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PickListDto>({
      method: 'GET',
      url: `/api/app/pick-list/${id}`,
    },
    { apiName: this.apiName,...config });
  

  getList = (input: CompanyFilteredPagedRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<PickListDto>>({
      method: 'GET',
      url: '/api/app/pick-list',
      params: { companyId: input.companyId, filter: input.filter, status: input.status, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  getPendingTransfers = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PendingTransferDto[]>({
      method: 'GET',
      url: `/api/app/pick-list/${id}/pending-transfers`,
    },
    { apiName: this.apiName,...config });
  

  submit = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PickListDto>({
      method: 'POST',
      url: `/api/app/pick-list/${id}/submit`,
    },
    { apiName: this.apiName,...config });
}