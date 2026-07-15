import { Injectable, inject } from '@angular/core';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import type { BomDto, CreateBomDto, WorkOrderDto, CreateWorkOrderDto } from './models';

@Injectable({ providedIn: 'root' })
export class ManufacturingService {
  apiName = 'Default';

  private restService = inject(RestService);

  // BOM
  getBom = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<void, BomDto>({ method: 'GET', url: `/api/app/manufacturing/bom/${id}` }, { apiName: this.apiName, ...config });

  getBomList = (input: any, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<BomDto>>({ method: 'GET', url: '/api/app/manufacturing/bom', params: { ...input } }, { apiName: this.apiName, ...config });

  createBom = (input: CreateBomDto, config?: Partial<Rest.Config>) =>
    this.restService.request<CreateBomDto, BomDto>({ method: 'POST', url: '/api/app/manufacturing/bom', body: input }, { apiName: this.apiName, ...config });

  deleteBom = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<void, void>({ method: 'DELETE', url: `/api/app/manufacturing/bom/${id}` }, { apiName: this.apiName, ...config });

  // Work Order
  getWorkOrder = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<void, WorkOrderDto>({ method: 'GET', url: `/api/app/manufacturing/work-order/${id}` }, { apiName: this.apiName, ...config });

  getWorkOrderList = (input: any, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<WorkOrderDto>>({ method: 'GET', url: '/api/app/manufacturing/work-order', params: { ...input } }, { apiName: this.apiName, ...config });

  createWorkOrder = (input: CreateWorkOrderDto, config?: Partial<Rest.Config>) =>
    this.restService.request<CreateWorkOrderDto, WorkOrderDto>({ method: 'POST', url: '/api/app/manufacturing/work-order', body: input }, { apiName: this.apiName, ...config });

  submitWorkOrder = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<void, WorkOrderDto>({ method: 'POST', url: `/api/app/manufacturing/work-order/${id}/submit` }, { apiName: this.apiName, ...config });

  startWorkOrder = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<void, WorkOrderDto>({ method: 'POST', url: `/api/app/manufacturing/work-order/${id}/start` }, { apiName: this.apiName, ...config });

  recordProduction = (id: string, quantity: number, config?: Partial<Rest.Config>) =>
    this.restService.request<any, WorkOrderDto>({ method: 'POST', url: `/api/app/manufacturing/work-order/${id}/record-production`, params: { quantity } }, { apiName: this.apiName, ...config });

  stopWorkOrder = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<void, WorkOrderDto>({ method: 'POST', url: `/api/app/manufacturing/work-order/${id}/stop` }, { apiName: this.apiName, ...config });
}


