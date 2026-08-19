import type { CreateShiftTypeDto, ShiftTypeDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedAndSortedResultRequestDto, PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class ShiftTypeService {
  private restService = inject(RestService);
  apiName = 'Default';


  create = (input: CreateShiftTypeDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ShiftTypeDto>({
      method: 'POST',
      url: '/api/app/shift-type',
      body: input,
    },
    { apiName: this.apiName,...config });


  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/shift-type/${id}`,
    },
    { apiName: this.apiName,...config });


  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ShiftTypeDto>({
      method: 'GET',
      url: `/api/app/shift-type/${id}`,
    },
    { apiName: this.apiName,...config });


  getList = (input: PagedAndSortedResultRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<ShiftTypeDto>>({
      method: 'GET',
      url: '/api/app/shift-type',
      params: { sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });


  update = (id: string, input: CreateShiftTypeDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ShiftTypeDto>({
      method: 'PUT',
      url: `/api/app/shift-type/${id}`,
      body: input,
    },
    { apiName: this.apiName,...config });
}
