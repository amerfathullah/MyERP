import type { CreateProductionPlanDto, GetProductionPlanListDto, OpenSalesOrderItemDto, PooledBinDetailsDto, ProductionPlanDto, ProductionPlanMaterialDownloadRowDto, ProductionPlanSummaryDto, ProductionPlanVisualizerDto } from './models';
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
      headers: { Accept: 'application/json' },
      url: `/api/app/production-plan/${id}/calculate-material-requirements`,
    },
      { apiName: this.apiName,...config });
  

  
  cancel = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ProductionPlanDto>({
      method: 'POST',
      headers: { Accept: 'application/json' },
      url: `/api/app/production-plan/${id}/cancel`,
    },
      { apiName: this.apiName,...config });
  

  
  close = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ProductionPlanDto>({
      method: 'POST',
      headers: { Accept: 'application/json' },
      url: `/api/app/production-plan/${id}/close`,
    },
      { apiName: this.apiName,...config });
  

  
  create = (input: CreateProductionPlanDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ProductionPlanDto>({
      method: 'POST',
      headers: { Accept: 'application/json' },
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
  

  
  downloadRawMaterials = (id: string, warehouseIds?: readonly string[], config?: Partial<Rest.Config>) =>
    this.restService.request<any, ProductionPlanMaterialDownloadRowDto[]>({
      method: 'POST',
      headers: { Accept: 'application/json' },
      url: `/api/app/production-plan/${id}/download-raw-materials`,
      body: warehouseIds,
    },
      { apiName: this.apiName,...config });
  

  
  generateMaterialRequests = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ProductionPlanDto>({
      method: 'POST',
      headers: { Accept: 'application/json' },
      url: `/api/app/production-plan/${id}/generate-material-requests`,
    },
      { apiName: this.apiName,...config });
  

  
  generateWorkOrders = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ProductionPlanDto>({
      method: 'POST',
      headers: { Accept: 'application/json' },
      url: `/api/app/production-plan/${id}/generate-work-orders`,
    },
      { apiName: this.apiName,...config });
  

  
  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ProductionPlanDto>({
      method: 'GET',
      headers: { Accept: 'application/json' },
      url: `/api/app/production-plan/${id}`,
    },
      { apiName: this.apiName,...config });
  

  
  getList = (input: GetProductionPlanListDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<ProductionPlanDto>>({
      method: 'GET',
      headers: { Accept: 'application/json' },
      url: '/api/app/production-plan',
      params: { status: input.status, companyId: input.companyId, filter: input.filter, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
      { apiName: this.apiName,...config });
  

  
  getOpenSalesOrderItems = (companyId: string, salesOrderIds?: readonly string[], config?: Partial<Rest.Config>) =>
    this.restService.request<any, OpenSalesOrderItemDto[]>({
      method: 'GET',
      headers: { Accept: 'application/json' },
      url: `/api/app/production-plan/open-sales-order-items/${companyId}`,
      params: { salesOrderIds },
    },
      { apiName: this.apiName,...config });
  

  
  getPooledBinDetails = (companyId: string, itemId: string, warehouseId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PooledBinDetailsDto>({
      method: 'GET',
      headers: { Accept: 'application/json' },
      url: '/api/app/production-plan/pooled-bin-details',
      params: { companyId, itemId, warehouseId },
    },
      { apiName: this.apiName,...config });
  

  
  getSummaryReport = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ProductionPlanSummaryDto>({
      method: 'GET',
      headers: { Accept: 'application/json' },
      url: `/api/app/production-plan/${id}/summary-report`,
    },
      { apiName: this.apiName,...config });
  

  
  getVisualizerData = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ProductionPlanVisualizerDto>({
      method: 'GET',
      headers: { Accept: 'application/json' },
      url: `/api/app/production-plan/${id}/visualizer-data`,
    },
      { apiName: this.apiName,...config });
  

  
  reopen = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ProductionPlanDto>({
      method: 'POST',
      headers: { Accept: 'application/json' },
      url: `/api/app/production-plan/${id}/reopen`,
    },
      { apiName: this.apiName,...config });
  

  
  submit = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ProductionPlanDto>({
      method: 'POST',
      headers: { Accept: 'application/json' },
      url: `/api/app/production-plan/${id}/submit`,
    },
      { apiName: this.apiName,...config });
  

  
  update = (id: string, input: CreateProductionPlanDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ProductionPlanDto>({
      method: 'PUT',
      headers: { Accept: 'application/json' },
      url: `/api/app/production-plan/${id}`,
      body: input,
    },
      { apiName: this.apiName,...config });
}
