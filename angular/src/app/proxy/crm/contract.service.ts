import { RestService, Rest } from '@abp/ng.core';
import type { PagedAndSortedResultRequestDto, PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

export interface ContractDto {
  id: string;
  contractNumber: string;
  companyId: string;
  partyType: string;
  partyId?: string | null;
  startDate: string;
  endDate?: string | null;
  contractValue?: number | null;
  contractTerms?: string | null;
  status: number;
  fulfilmentStatus: number;
  isAutoRenew: boolean;
}

export interface CreateUpdateContractDto {
  contractNumber: string;
  companyId: string;
  partyType: string;
  partyId?: string | null;
  startDate: string;
  endDate?: string | null;
  contractValue?: number | null;
  contractTerms?: string | null;
  isAutoRenew?: boolean;
}

export interface GetContractListDto extends PagedAndSortedResultRequestDto {
  filter?: string;
  status?: number | null;
}

@Injectable({
  providedIn: 'root',
})
export class ContractService {
  private restService = inject(RestService);
  apiName = 'Default';

  getList = (input: GetContractListDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<ContractDto>>({
      method: 'GET',
      url: '/api/app/contract',
      params: { filter: input.filter, status: input.status, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName, ...config });

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ContractDto>({
      method: 'GET',
      url: `/api/app/contract/${id}`,
    },
    { apiName: this.apiName, ...config });

  create = (input: CreateUpdateContractDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ContractDto>({
      method: 'POST',
      url: '/api/app/contract',
      body: input,
    },
    { apiName: this.apiName, ...config });

  update = (id: string, input: CreateUpdateContractDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ContractDto>({
      method: 'PUT',
      url: `/api/app/contract/${id}`,
      body: input,
    },
    { apiName: this.apiName, ...config });

  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/contract/${id}`,
    },
    { apiName: this.apiName, ...config });

  sign = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ContractDto>({
      method: 'POST',
      url: `/api/app/contract/${id}/sign`,
    },
    { apiName: this.apiName, ...config });

  cancel = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ContractDto>({
      method: 'POST',
      url: `/api/app/contract/${id}/cancel`,
    },
    { apiName: this.apiName, ...config });
}
