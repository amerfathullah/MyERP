import type { CreateUpdateSubcontractingBomDto, SubcontractingBomDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedAndSortedResultRequestDto, PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class SubcontractingBomService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  create = (input: CreateUpdateSubcontractingBomDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, SubcontractingBomDto>({
      method: 'POST',
      url: '/api/app/subcontracting-bom',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/subcontracting-bom/${id}`,
    },
    { apiName: this.apiName,...config });
  

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, SubcontractingBomDto>({
      method: 'GET',
      url: `/api/app/subcontracting-bom/${id}`,
    },
    { apiName: this.apiName,...config });
  

  getList = (input: PagedAndSortedResultRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<SubcontractingBomDto>>({
      method: 'GET',
      url: '/api/app/subcontracting-bom',
      params: { sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  update = (id: string, input: CreateUpdateSubcontractingBomDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, SubcontractingBomDto>({
      method: 'PUT',
      url: `/api/app/subcontracting-bom/${id}`,
      body: input,
    },
    { apiName: this.apiName,...config });
}