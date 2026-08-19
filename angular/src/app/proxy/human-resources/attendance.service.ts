import type { AttendanceDto, CreateAttendanceDto, GetAttendanceListDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class AttendanceService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  create = (input: CreateAttendanceDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, AttendanceDto>({
      method: 'POST',
      url: '/api/app/attendance',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/attendance/${id}`,
    },
    { apiName: this.apiName,...config });
  

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, AttendanceDto>({
      method: 'GET',
      url: `/api/app/attendance/${id}`,
    },
    { apiName: this.apiName,...config });
  

  getList = (input: GetAttendanceListDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<AttendanceDto>>({
      method: 'GET',
      url: '/api/app/attendance',
      params: { companyId: input.companyId, employeeId: input.employeeId, fromDate: input.fromDate, toDate: input.toDate, status: input.status, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  update = (id: string, input: CreateAttendanceDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, AttendanceDto>({
      method: 'PUT',
      url: `/api/app/attendance/${id}`,
      body: input,
    },
    { apiName: this.apiName,...config });
}