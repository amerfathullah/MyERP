import type { CreateUpdateSupplierScorecardVariableDto, GetSupplierScorecardVariableListDto, SupplierScorecardVariableDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class SupplierScorecardVariableService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  create = (input: CreateUpdateSupplierScorecardVariableDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, SupplierScorecardVariableDto>({
      method: 'POST',
      url: '/api/app/supplier-scorecard-variable',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/supplier-scorecard-variable/${id}`,
    },
    { apiName: this.apiName,...config });
  

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, SupplierScorecardVariableDto>({
      method: 'GET',
      url: `/api/app/supplier-scorecard-variable/${id}`,
    },
    { apiName: this.apiName,...config });
  

  getAllList = (config?: Partial<Rest.Config>) =>
    this.restService.request<any, SupplierScorecardVariableDto[]>({
      method: 'GET',
      url: '/api/app/supplier-scorecard-variable/list',
    },
    { apiName: this.apiName,...config });
  

  getList = (input: GetSupplierScorecardVariableListDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<SupplierScorecardVariableDto>>({
      method: 'GET',
      url: '/api/app/supplier-scorecard-variable',
      params: { filter: input.filter, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  update = (id: string, input: CreateUpdateSupplierScorecardVariableDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, SupplierScorecardVariableDto>({
      method: 'PUT',
      url: `/api/app/supplier-scorecard-variable/${id}`,
      body: input,
    },
    { apiName: this.apiName,...config });
}