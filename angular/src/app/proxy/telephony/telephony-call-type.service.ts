import type { TelephonyCallTypeDto, CreateUpdateTelephonyCallTypeDto, GetTelephonyCallTypeListDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class TelephonyCallTypeService {
  private restService = inject(RestService);
  apiName = 'Default';

  create = (input: CreateUpdateTelephonyCallTypeDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, TelephonyCallTypeDto>({
      method: 'POST',
      url: '/api/app/telephony-call-type',
      body: input,
    },
    { apiName: this.apiName, ...config });

  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/telephony-call-type/${id}`,
    },
    { apiName: this.apiName, ...config });

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, TelephonyCallTypeDto>({
      method: 'GET',
      url: `/api/app/telephony-call-type/${id}`,
    },
    { apiName: this.apiName, ...config });

  getList = (input: GetTelephonyCallTypeListDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<TelephonyCallTypeDto>>({
      method: 'GET',
      url: '/api/app/telephony-call-type',
      params: { filter: input.filter, isActive: input.isActive, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName, ...config });

  update = (id: string, input: CreateUpdateTelephonyCallTypeDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, TelephonyCallTypeDto>({
      method: 'PUT',
      url: `/api/app/telephony-call-type/${id}`,
      body: input,
    },
    { apiName: this.apiName, ...config });
}
