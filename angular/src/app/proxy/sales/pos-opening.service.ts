import type { CreatePosOpeningDto, PosOpeningDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';
import type { CompanyFilteredPagedRequestDto } from '../shared/models';

@Injectable({
  providedIn: 'root',
})
export class PosOpeningService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  cancel = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PosOpeningDto>({
      method: 'POST',
      url: `/api/app/pos-opening/${id}/cancel`,
    },
    { apiName: this.apiName,...config });
  

  create = (input: CreatePosOpeningDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PosOpeningDto>({
      method: 'POST',
      url: '/api/app/pos-opening',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PosOpeningDto>({
      method: 'GET',
      url: `/api/app/pos-opening/${id}`,
    },
    { apiName: this.apiName,...config });
  

  getCurrentOpen = (userId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PosOpeningDto>({
      method: 'GET',
      url: `/api/app/pos-opening/current-open/${userId}`,
    },
    { apiName: this.apiName,...config });
  

  getList = (input: CompanyFilteredPagedRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<PosOpeningDto>>({
      method: 'GET',
      url: '/api/app/pos-opening',
      params: { companyId: input.companyId, filter: input.filter, status: input.status, fromDate: input.fromDate, toDate: input.toDate, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
}