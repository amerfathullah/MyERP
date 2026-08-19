import type { ContractDto, CreateContractDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';
import type { CompanyFilteredPagedRequestDto } from '../shared/models';

@Injectable({
  providedIn: 'root',
})
export class ContractService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  addFulfilmentItem = (id: string, requirement: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ContractDto>({
      method: 'POST',
      url: `/api/app/contract/${id}/fulfilment-item`,
      params: { requirement },
    },
    { apiName: this.apiName,...config });
  

  cancel = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ContractDto>({
      method: 'POST',
      url: `/api/app/contract/${id}/cancel`,
    },
    { apiName: this.apiName,...config });
  

  create = (input: CreateContractDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ContractDto>({
      method: 'POST',
      url: '/api/app/contract',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ContractDto>({
      method: 'GET',
      url: `/api/app/contract/${id}`,
    },
    { apiName: this.apiName,...config });
  

  getList = (input: CompanyFilteredPagedRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<ContractDto>>({
      method: 'GET',
      url: '/api/app/contract',
      params: { companyId: input.companyId, filter: input.filter, status: input.status, fromDate: input.fromDate, toDate: input.toDate, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  removeFulfilmentItem = (id: string, itemId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ContractDto>({
      method: 'DELETE',
      url: `/api/app/contract/${id}/fulfilment-item/${itemId}`,
    },
    { apiName: this.apiName,...config });
  

  renew = (id: string, newEndDate: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ContractDto>({
      method: 'POST',
      url: `/api/app/contract/${id}/renew`,
      params: { newEndDate },
    },
    { apiName: this.apiName,...config });
  

  setFulfilmentItemStatus = (id: string, itemId: string, fulfilled: boolean, notes?: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ContractDto>({
      method: 'POST',
      url: `/api/app/contract/${id}/set-fulfilment-item-status/${itemId}`,
      params: { fulfilled, notes },
    },
    { apiName: this.apiName,...config });
  

  sign = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ContractDto>({
      method: 'POST',
      url: `/api/app/contract/${id}/sign`,
    },
    { apiName: this.apiName,...config });
  

  update = (id: string, input: CreateContractDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ContractDto>({
      method: 'PUT',
      url: `/api/app/contract/${id}`,
      body: input,
    },
    { apiName: this.apiName,...config });
}