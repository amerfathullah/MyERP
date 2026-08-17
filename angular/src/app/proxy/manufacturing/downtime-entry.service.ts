import type { CreateUpdateDowntimeEntryDto, DowntimeEntryDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

export interface GetDowntimeEntryListDto {
  companyId?: string;
  fromDate?: string;
  toDate?: string;
  sorting?: string;
  skipCount?: number;
  maxResultCount?: number;
}

@Injectable({
  providedIn: 'root',
})
export class DowntimeEntryService {
  private restService = inject(RestService);
  apiName = 'Default';


  create = (input: CreateUpdateDowntimeEntryDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, DowntimeEntryDto>({
      method: 'POST',
      url: '/api/app/downtime-entry',
      body: input,
    },
    { apiName: this.apiName,...config });


  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/downtime-entry/${id}`,
    },
    { apiName: this.apiName,...config });


  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, DowntimeEntryDto>({
      method: 'GET',
      url: `/api/app/downtime-entry/${id}`,
    },
    { apiName: this.apiName,...config });


  getList = (input: GetDowntimeEntryListDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<DowntimeEntryDto>>({
      method: 'GET',
      url: '/api/app/downtime-entry',
      params: { companyId: input.companyId, fromDate: input.fromDate, toDate: input.toDate, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });


  update = (id: string, input: CreateUpdateDowntimeEntryDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, DowntimeEntryDto>({
      method: 'PUT',
      url: `/api/app/downtime-entry/${id}`,
      body: input,
    },
    { apiName: this.apiName,...config });
}
