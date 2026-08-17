import type { AppointmentDto, CreateAppointmentDto, GetAppointmentListDto, VerifyAppointmentDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class AppointmentService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  close = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, AppointmentDto>({
      method: 'POST',
      url: `/api/app/appointment/${id}/close`,
    },
    { apiName: this.apiName,...config });
  

  create = (input: CreateAppointmentDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, AppointmentDto>({
      method: 'POST',
      url: '/api/app/appointment',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/appointment/${id}`,
    },
    { apiName: this.apiName,...config });
  

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, AppointmentDto>({
      method: 'GET',
      url: `/api/app/appointment/${id}`,
    },
    { apiName: this.apiName,...config });
  

  getList = (input: GetAppointmentListDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<AppointmentDto>>({
      method: 'GET',
      url: '/api/app/appointment',
      params: { companyId: input.companyId, status: input.status, fromDate: input.fromDate, toDate: input.toDate, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  verify = (id: string, input: VerifyAppointmentDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, AppointmentDto>({
      method: 'POST',
      url: `/api/app/appointment/${id}/verify`,
      body: input,
    },
    { apiName: this.apiName,...config });
}