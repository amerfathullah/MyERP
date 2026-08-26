import type { TaxWithholdingGroupDto, CreateUpdateTaxWithholdingGroupDto, GetTaxWithholdingGroupListDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class TaxWithholdingGroupService {
  private restService = inject(RestService);
  apiName = 'Default';

  create = (input: CreateUpdateTaxWithholdingGroupDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, TaxWithholdingGroupDto>({
      method: 'POST',
      url: '/api/app/tax-withholding-group',
      body: input,
    },
    { apiName: this.apiName, ...config });

  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/tax-withholding-group/${id}`,
    },
    { apiName: this.apiName, ...config });

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, TaxWithholdingGroupDto>({
      method: 'GET',
      url: `/api/app/tax-withholding-group/${id}`,
    },
    { apiName: this.apiName, ...config });

  getList = (input: GetTaxWithholdingGroupListDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<TaxWithholdingGroupDto>>({
      method: 'GET',
      url: '/api/app/tax-withholding-group',
      params: { filter: input.filter, isActive: input.isActive, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName, ...config });

  update = (id: string, input: CreateUpdateTaxWithholdingGroupDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, TaxWithholdingGroupDto>({
      method: 'PUT',
      url: `/api/app/tax-withholding-group/${id}`,
      body: input,
    },
    { apiName: this.apiName, ...config });
}
