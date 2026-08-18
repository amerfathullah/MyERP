import type { CreateDunningTypeDto, DunningTypeDto, UpdateDunningTypeDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

export interface GetDunningTypeListDto {
  companyId?: string;
  sorting?: string;
  skipCount?: number;
  maxResultCount?: number;
}

@Injectable({
  providedIn: 'root',
})
export class DunningTypeService {
  private restService = inject(RestService);
  apiName = 'Default';


  create = (input: CreateDunningTypeDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, DunningTypeDto>({
      method: 'POST',
      url: '/api/app/dunning-type',
      body: input,
    },
    { apiName: this.apiName,...config });


  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/dunning-type/${id}`,
    },
    { apiName: this.apiName,...config });


  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, DunningTypeDto>({
      method: 'GET',
      url: `/api/app/dunning-type/${id}`,
    },
    { apiName: this.apiName,...config });


  getList = (input: GetDunningTypeListDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<DunningTypeDto>>({
      method: 'GET',
      url: '/api/app/dunning-type',
      params: { companyId: input.companyId, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });


  update = (id: string, input: UpdateDunningTypeDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, DunningTypeDto>({
      method: 'PUT',
      url: `/api/app/dunning-type/${id}`,
      body: input,
    },
    { apiName: this.apiName,...config });
}
