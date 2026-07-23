import type { CostCenterAllocationDto, CreateCostCenterAllocationDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';
import type { CompanyFilteredPagedRequestDto } from '../shared/models';

@Injectable({
  providedIn: 'root',
})
export class CostCenterAllocationService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  create = (input: CreateCostCenterAllocationDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, CostCenterAllocationDto>({
      method: 'POST',
      url: '/api/app/cost-center-allocation',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/cost-center-allocation/${id}`,
    },
    { apiName: this.apiName,...config });
  

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, CostCenterAllocationDto>({
      method: 'GET',
      url: `/api/app/cost-center-allocation/${id}`,
    },
    { apiName: this.apiName,...config });
  

  getList = (input: CompanyFilteredPagedRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<CostCenterAllocationDto>>({
      method: 'GET',
      url: '/api/app/cost-center-allocation',
      params: { companyId: input.companyId, filter: input.filter, status: input.status, fromDate: input.fromDate, toDate: input.toDate, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  toggleActive = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'POST',
      url: `/api/app/cost-center-allocation/${id}/toggle-active`,
    },
    { apiName: this.apiName,...config });
}