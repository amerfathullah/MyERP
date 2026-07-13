import { Injectable } from '@angular/core';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';

export interface LeaveTypeDto {
  id?: string;
  name?: string;
  maxDaysAllowed?: number;
  isPaidLeave?: boolean;
  requiresApproval?: boolean;
}

export interface LeaveApplicationDto {
  id?: string;
  employeeId?: string;
  employeeName?: string;
  leaveTypeId?: string;
  leaveTypeName?: string;
  fromDate?: string;
  toDate?: string;
  totalLeaveDays?: number;
  halfDay?: boolean;
  reason?: string;
  status?: number;
  creationTime?: string;
}

@Injectable({ providedIn: 'root' })
export class LeaveService {
  apiName = 'Default';
  constructor(private restService: RestService) {}

  getLeaveTypes = (config?: Partial<Rest.Config>) =>
    this.restService.request<void, LeaveTypeDto[]>({ method: 'GET', url: '/api/app/leave/leave-types' }, { apiName: this.apiName, ...config });

  getList = (input: any, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<LeaveApplicationDto>>({ method: 'GET', url: '/api/app/leave', params: { ...input } }, { apiName: this.apiName, ...config });

  apply = (input: any, config?: Partial<Rest.Config>) =>
    this.restService.request<any, LeaveApplicationDto>({ method: 'POST', url: '/api/app/leave/apply', body: input }, { apiName: this.apiName, ...config });

  approve = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<void, LeaveApplicationDto>({ method: 'POST', url: `/api/app/leave/${id}/approve` }, { apiName: this.apiName, ...config });

  reject = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<void, LeaveApplicationDto>({ method: 'POST', url: `/api/app/leave/${id}/reject` }, { apiName: this.apiName, ...config });
}
