import { Injectable } from '@angular/core';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import type { ProductionPlanDto, CreateProductionPlanDto } from './models';

@Injectable({ providedIn: 'root' })
export class ProductionPlanService {
  apiName = 'Default';

  constructor(private restService: RestService) {}

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<void, ProductionPlanDto>({ method: 'GET', url: `/api/app/production-plan/${id}` }, { apiName: this.apiName, ...config });

  getList = (input: any, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<ProductionPlanDto>>({ method: 'GET', url: '/api/app/production-plan', params: { ...input } }, { apiName: this.apiName, ...config });

  create = (input: CreateProductionPlanDto, config?: Partial<Rest.Config>) =>
    this.restService.request<CreateProductionPlanDto, ProductionPlanDto>({ method: 'POST', url: '/api/app/production-plan', body: input }, { apiName: this.apiName, ...config });

  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<void, void>({ method: 'DELETE', url: `/api/app/production-plan/${id}` }, { apiName: this.apiName, ...config });

  submit = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<void, ProductionPlanDto>({ method: 'POST', url: `/api/app/production-plan/${id}/submit` }, { apiName: this.apiName, ...config });

  cancel = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<void, ProductionPlanDto>({ method: 'POST', url: `/api/app/production-plan/${id}/cancel` }, { apiName: this.apiName, ...config });

  calculateMaterials = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<void, ProductionPlanDto>({ method: 'POST', url: `/api/app/production-plan/${id}/calculate-material-requirements` }, { apiName: this.apiName, ...config });

  generateWorkOrders = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<void, ProductionPlanDto>({ method: 'POST', url: `/api/app/production-plan/${id}/generate-work-orders` }, { apiName: this.apiName, ...config });

  generateMaterialRequests = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<void, ProductionPlanDto>({ method: 'POST', url: `/api/app/production-plan/${id}/generate-material-requests` }, { apiName: this.apiName, ...config });
}
