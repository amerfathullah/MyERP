import type { BranchDto, CreateUpdateBranchDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedAndSortedResultRequestDto, PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class BranchService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  create = (input: CreateUpdateBranchDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, BranchDto>({
      method: 'POST',
      url: '/api/app/branch',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/branch/${id}`,
    },
    { apiName: this.apiName,...config });
  

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, BranchDto>({
      method: 'GET',
      url: `/api/app/branch/${id}`,
    },
    { apiName: this.apiName,...config });
  

  getList = (input: PagedAndSortedResultRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<BranchDto>>({
      method: 'GET',
      url: '/api/app/branch',
      params: { sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  update = (id: string, input: CreateUpdateBranchDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, BranchDto>({
      method: 'PUT',
      url: `/api/app/branch/${id}`,
      body: input,
    },
    { apiName: this.apiName,...config });
}