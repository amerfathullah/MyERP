import type { BulkLeaveAllocationDto, CreateLeaveAllocationDto, GetLeaveAllocationListDto, LeaveAllocationDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class LeaveAllocationService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  bulkAllocate = (input: BulkLeaveAllocationDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, number>({
      method: 'POST',
      url: '/api/app/leave-allocation/bulk-allocate',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  create = (input: CreateLeaveAllocationDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, LeaveAllocationDto>({
      method: 'POST',
      url: '/api/app/leave-allocation',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/leave-allocation/${id}`,
    },
    { apiName: this.apiName,...config });
  

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, LeaveAllocationDto>({
      method: 'GET',
      url: `/api/app/leave-allocation/${id}`,
    },
    { apiName: this.apiName,...config });
  

  getBalance = (employeeId: string, leaveTypeId: string, asOfDate: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, number>({
      method: 'GET',
      url: '/api/app/leave-allocation/balance',
      params: { employeeId, leaveTypeId, asOfDate },
    },
    { apiName: this.apiName,...config });
  

  getList = (input: GetLeaveAllocationListDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<LeaveAllocationDto>>({
      method: 'GET',
      url: '/api/app/leave-allocation',
      params: { employeeId: input.employeeId, companyId: input.companyId, leaveTypeId: input.leaveTypeId, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
}