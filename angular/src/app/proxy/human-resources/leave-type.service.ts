import type { CreateUpdateLeaveTypeDto, LeaveTypeDetailDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedAndSortedResultRequestDto, PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class LeaveTypeService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  create = (input: CreateUpdateLeaveTypeDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, LeaveTypeDetailDto>({
      method: 'POST',
      url: '/api/app/leave-type',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/leave-type/${id}`,
    },
    { apiName: this.apiName,...config });
  

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, LeaveTypeDetailDto>({
      method: 'GET',
      url: `/api/app/leave-type/${id}`,
    },
    { apiName: this.apiName,...config });
  

  getList = (input: PagedAndSortedResultRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<LeaveTypeDetailDto>>({
      method: 'GET',
      url: '/api/app/leave-type',
      params: { sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  update = (id: string, input: CreateUpdateLeaveTypeDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, LeaveTypeDetailDto>({
      method: 'PUT',
      url: `/api/app/leave-type/${id}`,
      body: input,
    },
    { apiName: this.apiName,...config });
}