import type { CreateTimesheetDto, CreateTimesheetInvoiceDto, DailyTimesheetSummaryReportDto, DailyTimesheetSummaryRequestDto, GetTimesheetListDto, TimesheetBillingResultDto, TimesheetBillingSummaryReportDto, TimesheetBillingSummaryRequestDto, TimesheetDto, UnbilledTimesheetSummaryDto } from './models';
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
      headers: { Accept: 'application/json' },
      url: `/api/app/timesheet/${id}/cancel`,
    },
      { apiName: this.apiName,...config });
  

  
  create = (input: CreateTimesheetDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, TimesheetDto>({
      method: 'POST',
      headers: { Accept: 'application/json' },
      url: '/api/app/timesheet',
      body: input,
    },
      { apiName: this.apiName,...config });
  

  
  createInvoiceFromTimesheets = (input: CreateTimesheetInvoiceDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, TimesheetBillingResultDto>({
      method: 'POST',
      headers: { Accept: 'application/json' },
      url: '/api/app/timesheet/invoice-from-timesheets',
      body: input,
    },
      { apiName: this.apiName,...config });
  

  
  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, TimesheetDto>({
      method: 'GET',
      headers: { Accept: 'application/json' },
      url: `/api/app/timesheet/${id}`,
    },
      { apiName: this.apiName,...config });
  

  
  getList = (input: GetTimesheetListDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<TimesheetDto>>({
      method: 'GET',
      headers: { Accept: 'application/json' },
      url: '/api/app/timesheet',
      params: { companyId: input.companyId, employeeId: input.employeeId, status: input.status, fromDate: input.fromDate, toDate: input.toDate, filter: input.filter, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
      { apiName: this.apiName,...config });
  

  
  getUnbilledSummary = (companyId: string, projectId: string, fromDate?: string, toDate?: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, UnbilledTimesheetSummaryDto[]>({
      method: 'GET',
      headers: { Accept: 'application/json' },
      url: '/api/app/timesheet/unbilled-summary',
      params: { companyId, projectId, fromDate, toDate },
    },
      { apiName: this.apiName,...config });
  

  
  submit = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, TimesheetDto>({
      method: 'POST',
      headers: { Accept: 'application/json' },
      url: `/api/app/timesheet/${id}/submit`,
    },
      { apiName: this.apiName,...config });
  

  
  update = (id: string, input: CreateTimesheetDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, TimesheetDto>({
      method: 'PUT',
      headers: { Accept: 'application/json' },
      url: `/api/app/timesheet/${id}`,
      body: input,
    },
      { apiName: this.apiName,...config });

  
  getDailyTimesheetSummary = (input: DailyTimesheetSummaryRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, DailyTimesheetSummaryReportDto>({
      method: 'GET',
      headers: { Accept: 'application/json' },
      url: '/api/app/timesheet/daily-timesheet-summary',
      params: { companyId: input.companyId, fromDate: input.fromDate, toDate: input.toDate, employeeId: input.employeeId, projectId: input.projectId },
    },
      { apiName: this.apiName,...config });

  
  getTimesheetBillingSummary = (input: TimesheetBillingSummaryRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, TimesheetBillingSummaryReportDto>({
      method: 'GET',
      headers: { Accept: 'application/json' },
      url: '/api/app/timesheet/timesheet-billing-summary',
      params: { companyId: input.companyId, employeeId: input.employeeId, projectId: input.projectId, fromDate: input.fromDate, toDate: input.toDate, includeDraftTimesheets: input.includeDraftTimesheets },
    },
      { apiName: this.apiName,...config });
}
