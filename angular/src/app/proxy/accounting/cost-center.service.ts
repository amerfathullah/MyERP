import type { CostCenterDto, CostCenterTreeNodeDto, CreateCostCenterDto, GetCostCenterListDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class CostCenterService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  create = (input: CreateCostCenterDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, CostCenterDto>({
      method: 'POST',
      url: '/api/app/cost-center',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  getList = (input: GetCostCenterListDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<CostCenterDto>>({
      method: 'GET',
      url: '/api/app/cost-center',
      params: { companyId: input.companyId, filter: input.filter, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  getTree = (companyId: string, includeDisabled?: boolean, config?: Partial<Rest.Config>) =>
    this.restService.request<any, CostCenterTreeNodeDto[]>({
      method: 'GET',
      url: `/api/app/cost-center/tree/${companyId}`,
      params: { includeDisabled },
    },
    { apiName: this.apiName,...config });
  

  update = (id: string, input: CreateCostCenterDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, CostCenterDto>({
      method: 'PUT',
      url: `/api/app/cost-center/${id}`,
      body: input,
    },
    { apiName: this.apiName,...config });
}