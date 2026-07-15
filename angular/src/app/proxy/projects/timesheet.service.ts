import type { CreateTimesheetDto, CreateTimesheetInvoiceDto, GetTimesheetListDto, TimesheetBillingResultDto, TimesheetDto, UnbilledTimesheetSummaryDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class TimesheetService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  cancel = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, TimesheetDto>({
      method: 'POST',
      url: `/api/app/timesheet/${id}/cancel`,
    },
    { apiName: this.apiName,...config });
  

  create = (input: CreateTimesheetDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, TimesheetDto>({
      method: 'POST',
      url: '/api/app/timesheet',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  createInvoiceFromTimesheets = (input: CreateTimesheetInvoiceDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, TimesheetBillingResultDto>({
      method: 'POST',
      url: '/api/app/timesheet/invoice-from-timesheets',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, TimesheetDto>({
      method: 'GET',
      url: `/api/app/timesheet/${id}`,
    },
    { apiName: this.apiName,...config });
  

  getList = (input: GetTimesheetListDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<TimesheetDto>>({
      method: 'GET',
      url: '/api/app/timesheet',
      params: { companyId: input.companyId, employeeId: input.employeeId, status: input.status, filter: input.filter, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  getUnbilledSummary = (companyId: string, projectId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, UnbilledTimesheetSummaryDto[]>({
      method: 'GET',
      url: '/api/app/timesheet/unbilled-summary',
      params: { companyId, projectId },
    },
    { apiName: this.apiName,...config });
  

  submit = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, TimesheetDto>({
      method: 'POST',
      url: `/api/app/timesheet/${id}/submit`,
    },
    { apiName: this.apiName,...config });
}