import type { CreateUpdateEmployeeGroupDto, EmployeeGroupDto, GetEmployeeGroupListDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class EmployeeGroupService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  
  create = (input: CreateUpdateEmployeeGroupDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, EmployeeGroupDto>({
      method: 'POST',
      headers: { Accept: 'application/json' },
      url: '/api/app/employee-group',
      body: input,
    },
      { apiName: this.apiName,...config });
  

  
  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/employee-group/${id}`,
    },
      { apiName: this.apiName,...config });
  

  
  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, EmployeeGroupDto>({
      method: 'GET',
      headers: { Accept: 'application/json' },
      url: `/api/app/employee-group/${id}`,
    },
      { apiName: this.apiName,...config });
  

  
  getList = (input: GetEmployeeGroupListDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<EmployeeGroupDto>>({
      method: 'GET',
      headers: { Accept: 'application/json' },
      url: '/api/app/employee-group',
      params: { companyId: input.companyId, isDisabled: input.isDisabled, filter: input.filter, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
      { apiName: this.apiName,...config });
  

  
  update = (id: string, input: CreateUpdateEmployeeGroupDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, EmployeeGroupDto>({
      method: 'PUT',
      headers: { Accept: 'application/json' },
      url: `/api/app/employee-group/${id}`,
      body: input,
    },
      { apiName: this.apiName,...config });
}
