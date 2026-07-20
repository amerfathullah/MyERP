import type { CreateUpdateSalaryComponentDto, SalaryComponentDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedAndSortedResultRequestDto, PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class SalaryComponentService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  create = (input: CreateUpdateSalaryComponentDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, SalaryComponentDto>({
      method: 'POST',
      url: '/api/app/salary-component',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/salary-component/${id}`,
    },
    { apiName: this.apiName,...config });
  

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, SalaryComponentDto>({
      method: 'GET',
      url: `/api/app/salary-component/${id}`,
    },
    { apiName: this.apiName,...config });
  

  getList = (input: PagedAndSortedResultRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<SalaryComponentDto>>({
      method: 'GET',
      url: '/api/app/salary-component',
      params: { sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  update = (id: string, input: CreateUpdateSalaryComponentDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, SalaryComponentDto>({
      method: 'PUT',
      url: `/api/app/salary-component/${id}`,
      body: input,
    },
    { apiName: this.apiName,...config });
}