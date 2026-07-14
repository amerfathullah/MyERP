import type { CreateLeaveApplicationDto, CreateLeaveTypeDto, GetLeaveListDto, LeaveApplicationDto, LeaveTypeDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class LeaveService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  apply = (input: CreateLeaveApplicationDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, LeaveApplicationDto>({
      method: 'POST',
      url: '/api/app/leave/apply',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  approve = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, LeaveApplicationDto>({
      method: 'POST',
      url: `/api/app/leave/${id}/approve`,
    },
    { apiName: this.apiName,...config });
  

  cancel = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, LeaveApplicationDto>({
      method: 'POST',
      url: `/api/app/leave/${id}/cancel`,
    },
    { apiName: this.apiName,...config });
  

  createLeaveType = (input: CreateLeaveTypeDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, LeaveTypeDto>({
      method: 'POST',
      url: '/api/app/leave/leave-type',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, LeaveApplicationDto>({
      method: 'GET',
      url: `/api/app/leave/${id}`,
    },
    { apiName: this.apiName,...config });
  

  getLeaveTypes = (config?: Partial<Rest.Config>) =>
    this.restService.request<any, LeaveTypeDto[]>({
      method: 'GET',
      url: '/api/app/leave/leave-types',
    },
    { apiName: this.apiName,...config });
  

  getList = (input: GetLeaveListDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<LeaveApplicationDto>>({
      method: 'GET',
      url: '/api/app/leave',
      params: { employeeId: input.employeeId, status: input.status, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  reject = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, LeaveApplicationDto>({
      method: 'POST',
      url: `/api/app/leave/${id}/reject`,
    },
    { apiName: this.apiName,...config });
}