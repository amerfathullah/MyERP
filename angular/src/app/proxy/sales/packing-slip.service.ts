import type { CreatePackingSlipDto, PackingSlipDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';
import type { CompanyFilteredPagedRequestDto } from '../shared/models';

@Injectable({
  providedIn: 'root',
})
export class PackingSlipService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  cancel = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PackingSlipDto>({
      method: 'POST',
      url: `/api/app/packing-slip/${id}/cancel`,
    },
    { apiName: this.apiName,...config });
  

  create = (input: CreatePackingSlipDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PackingSlipDto>({
      method: 'POST',
      url: '/api/app/packing-slip',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/packing-slip/${id}`,
    },
    { apiName: this.apiName,...config });
  

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PackingSlipDto>({
      method: 'GET',
      url: `/api/app/packing-slip/${id}`,
    },
    { apiName: this.apiName,...config });
  

  getList = (input: CompanyFilteredPagedRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<PackingSlipDto>>({
      method: 'GET',
      url: '/api/app/packing-slip',
      params: { companyId: input.companyId, filter: input.filter, status: input.status, fromDate: input.fromDate, toDate: input.toDate, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  submit = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PackingSlipDto>({
      method: 'POST',
      url: `/api/app/packing-slip/${id}/submit`,
    },
    { apiName: this.apiName,...config });
}