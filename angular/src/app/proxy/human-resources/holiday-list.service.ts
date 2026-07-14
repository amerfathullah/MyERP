import type { CreateHolidayListDto, HolidayListDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedAndSortedResultRequestDto, PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class HolidayListService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  create = (input: CreateHolidayListDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, HolidayListDto>({
      method: 'POST',
      url: '/api/app/holiday-list',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/holiday-list/${id}`,
    },
    { apiName: this.apiName,...config });
  

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, HolidayListDto>({
      method: 'GET',
      url: `/api/app/holiday-list/${id}`,
    },
    { apiName: this.apiName,...config });
  

  getList = (input: PagedAndSortedResultRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<HolidayListDto>>({
      method: 'GET',
      url: '/api/app/holiday-list',
      params: { sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
}