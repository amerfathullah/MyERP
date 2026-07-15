import type { AccountingDimensionDto, AccountingDimensionFilterDto, CreateAccountingDimensionDto, CreateDimensionFilterDto, UpdateAccountingDimensionDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedAndSortedResultRequestDto, PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class AccountingDimensionService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  create = (input: CreateAccountingDimensionDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, AccountingDimensionDto>({
      method: 'POST',
      url: '/api/app/accounting-dimension',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  createFilter = (input: CreateDimensionFilterDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, AccountingDimensionFilterDto>({
      method: 'POST',
      url: '/api/app/accounting-dimension/filter',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/accounting-dimension/${id}`,
    },
    { apiName: this.apiName,...config });
  

  deleteFilter = (filterId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/accounting-dimension/filter/${filterId}`,
    },
    { apiName: this.apiName,...config });
  

  disable = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'POST',
      url: `/api/app/accounting-dimension/${id}/disable`,
    },
    { apiName: this.apiName,...config });
  

  enable = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'POST',
      url: `/api/app/accounting-dimension/${id}/enable`,
    },
    { apiName: this.apiName,...config });
  

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, AccountingDimensionDto>({
      method: 'GET',
      url: `/api/app/accounting-dimension/${id}`,
    },
    { apiName: this.apiName,...config });
  

  getEnabledDimensions = (companyId?: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, AccountingDimensionDto[]>({
      method: 'GET',
      url: '/api/app/accounting-dimension/enabled-dimensions',
      params: { companyId },
    },
    { apiName: this.apiName,...config });
  

  getFilters = (dimensionId: string, companyId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, AccountingDimensionFilterDto[]>({
      method: 'GET',
      url: '/api/app/accounting-dimension/filters',
      params: { dimensionId, companyId },
    },
    { apiName: this.apiName,...config });
  

  getList = (input: PagedAndSortedResultRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<AccountingDimensionDto>>({
      method: 'GET',
      url: '/api/app/accounting-dimension',
      params: { sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  update = (id: string, input: UpdateAccountingDimensionDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, AccountingDimensionDto>({
      method: 'PUT',
      url: `/api/app/accounting-dimension/${id}`,
      body: input,
    },
    { apiName: this.apiName,...config });
}