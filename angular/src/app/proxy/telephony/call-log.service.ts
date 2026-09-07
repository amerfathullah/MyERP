import type { CallStatus } from './call-status.enum';
import type { CallLogDto, CreateCallLogDto, GetCallLogListDto, UpdateCallLogDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class CallLogService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  completeCall = (id: string, durationSeconds: number, recordingUrl?: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, CallLogDto>({
      method: 'POST',
      url: `/api/app/call-log/${id}/complete-call`,
      params: { durationSeconds, recordingUrl },
    },
    { apiName: this.apiName,...config });
  

  create = (input: CreateCallLogDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, CallLogDto>({
      method: 'POST',
      url: '/api/app/call-log',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/call-log/${id}`,
    },
    { apiName: this.apiName,...config });
  

  failCall = (id: string, failureStatus: CallStatus, config?: Partial<Rest.Config>) =>
    this.restService.request<any, CallLogDto>({
      method: 'POST',
      url: `/api/app/call-log/${id}/fail-call`,
      params: { failureStatus },
    },
    { apiName: this.apiName,...config });
  

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, CallLogDto>({
      method: 'GET',
      url: `/api/app/call-log/${id}`,
    },
    { apiName: this.apiName,...config });
  

  getList = (input: GetCallLogListDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<CallLogDto>>({
      method: 'GET',
      url: '/api/app/call-log',
      params: { callDirection: input.callDirection, status: input.status, telephonyCallTypeId: input.telephonyCallTypeId, customerId: input.customerId, filter: input.filter, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  startCall = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, CallLogDto>({
      method: 'POST',
      url: `/api/app/call-log/${id}/start-call`,
    },
    { apiName: this.apiName,...config });
  

  update = (id: string, input: UpdateCallLogDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, CallLogDto>({
      method: 'PUT',
      url: `/api/app/call-log/${id}`,
      body: input,
    },
    { apiName: this.apiName,...config });
}