import type { BomCreatorDto, CreateUpdateBomCreatorDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';
import type { CompanyFilteredPagedRequestDto } from '../shared/models';

@Injectable({
  providedIn: 'root',
})
export class BomCreatorService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  create = (input: CreateUpdateBomCreatorDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, BomCreatorDto>({
      method: 'POST',
      url: '/api/app/bom-creator',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  createBoms = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, BomCreatorDto>({
      method: 'POST',
      url: `/api/app/bom-creator/${id}/boms`,
    },
    { apiName: this.apiName,...config });
  

  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/bom-creator/${id}`,
    },
    { apiName: this.apiName,...config });
  

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, BomCreatorDto>({
      method: 'GET',
      url: `/api/app/bom-creator/${id}`,
    },
    { apiName: this.apiName,...config });
  

  getList = (input: CompanyFilteredPagedRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<BomCreatorDto>>({
      method: 'GET',
      url: '/api/app/bom-creator',
      params: { companyId: input.companyId, filter: input.filter, status: input.status, fromDate: input.fromDate, toDate: input.toDate, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  update = (id: string, input: CreateUpdateBomCreatorDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, BomCreatorDto>({
      method: 'PUT',
      url: `/api/app/bom-creator/${id}`,
      body: input,
    },
    { apiName: this.apiName,...config });
}