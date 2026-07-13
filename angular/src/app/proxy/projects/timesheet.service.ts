import { Injectable } from '@angular/core';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';

export interface TimesheetDto {
  id?: string;
  employeeId?: string;
  employeeName?: string;
  status?: number;
  startDate?: string;
  endDate?: string;
  totalHours?: number;
  totalBillableHours?: number;
  totalBillingAmount?: number;
  totalCostingAmount?: number;
  note?: string;
  creationTime?: string;
  details?: TimesheetDetailDto[];
}

export interface TimesheetDetailDto {
  id?: string;
  activityType?: string;
  hours?: number;
  isBillable?: boolean;
  billingRate?: number;
  billingAmount?: number;
  description?: string;
}

@Injectable({ providedIn: 'root' })
export class TimesheetService {
  apiName = 'Default';
  constructor(private restService: RestService) {}

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<void, TimesheetDto>({ method: 'GET', url: `/api/app/timesheet/${id}` }, { apiName: this.apiName, ...config });

  getList = (input: any, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<TimesheetDto>>({ method: 'GET', url: '/api/app/timesheet', params: { ...input } }, { apiName: this.apiName, ...config });

  create = (input: any, config?: Partial<Rest.Config>) =>
    this.restService.request<any, TimesheetDto>({ method: 'POST', url: '/api/app/timesheet', body: input }, { apiName: this.apiName, ...config });

  submit = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<void, TimesheetDto>({ method: 'POST', url: `/api/app/timesheet/${id}/submit` }, { apiName: this.apiName, ...config });
}
