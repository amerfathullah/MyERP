import type { CreateUpdateIndustryTypeDto, GetIndustryTypeListDto, IndustryTypeDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class IndustryTypeService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  create = (input: CreateUpdateIndustryTypeDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, IndustryTypeDto>({
      method: 'POST',
      url: '/api/app/industry-type',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/industry-type/${id}`,
    },
    { apiName: this.apiName,...config });
  

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, IndustryTypeDto>({
      method: 'GET',
      url: `/api/app/industry-type/${id}`,
    },
    { apiName: this.apiName,...config });
  

  getList = (input: GetIndustryTypeListDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<IndustryTypeDto>>({
      method: 'GET',
      url: '/api/app/industry-type',
      params: { filter: input.filter, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  update = (id: string, input: CreateUpdateIndustryTypeDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, IndustryTypeDto>({
      method: 'PUT',
      url: `/api/app/industry-type/${id}`,
      body: input,
    },
    { apiName: this.apiName,...config });
}