import type { CreateWarrantyClaimDto, GetWarrantyClaimListDto, WarrantyClaimDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class WarrantyClaimService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  cancel = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'POST',
      url: `/api/app/warranty-claim/${id}/cancel`,
    },
    { apiName: this.apiName,...config });
  

  close = (id: string, resolution: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'POST',
      url: `/api/app/warranty-claim/${id}/close`,
      params: { resolution },
    },
    { apiName: this.apiName,...config });
  

  create = (input: CreateWarrantyClaimDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, WarrantyClaimDto>({
      method: 'POST',
      url: '/api/app/warranty-claim',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, WarrantyClaimDto>({
      method: 'GET',
      url: `/api/app/warranty-claim/${id}`,
    },
    { apiName: this.apiName,...config });
  

  getList = (input: GetWarrantyClaimListDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<WarrantyClaimDto>>({
      method: 'GET',
      url: '/api/app/warranty-claim',
      params: { filter: input.filter, companyId: input.companyId, status: input.status, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  startWork = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'POST',
      url: `/api/app/warranty-claim/${id}/start-work`,
    },
    { apiName: this.apiName,...config });
}