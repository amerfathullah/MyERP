import type { CreateProductionPlanDto, GetProductionPlanListDto, ProductionPlanDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class ProductionPlanService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  calculateMaterialRequirements = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ProductionPlanDto>({
      method: 'POST',
      url: `/api/app/production-plan/${id}/calculate-material-requirements`,
    },
    { apiName: this.apiName,...config });
  

  cancel = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ProductionPlanDto>({
      method: 'POST',
      url: `/api/app/production-plan/${id}/cancel`,
    },
    { apiName: this.apiName,...config });
  

  create = (input: CreateProductionPlanDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ProductionPlanDto>({
      method: 'POST',
      url: '/api/app/production-plan',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/production-plan/${id}`,
    },
    { apiName: this.apiName,...config });
  

  generateMaterialRequests = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ProductionPlanDto>({
      method: 'POST',
      url: `/api/app/production-plan/${id}/generate-material-requests`,
    },
    { apiName: this.apiName,...config });
  

  generateWorkOrders = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ProductionPlanDto>({
      method: 'POST',
      url: `/api/app/production-plan/${id}/generate-work-orders`,
    },
    { apiName: this.apiName,...config });
  

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ProductionPlanDto>({
      method: 'GET',
      url: `/api/app/production-plan/${id}`,
    },
    { apiName: this.apiName,...config });
  

  getList = (input: GetProductionPlanListDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<ProductionPlanDto>>({
      method: 'GET',
      url: '/api/app/production-plan',
      params: { status: input.status, companyId: input.companyId, filter: input.filter, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  submit = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ProductionPlanDto>({
      method: 'POST',
      url: `/api/app/production-plan/${id}/submit`,
    },
    { apiName: this.apiName,...config });
}