import { RestService, Rest } from '@abp/ng.core';
import type { PagedAndSortedResultRequestDto, PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';
import type { BomDto, CreateBomDto, CreateWorkOrderDto, GetWorkOrderListDto, WorkOrderDto } from '../manufacturing/models';

@Injectable({
  providedIn: 'root',
})
export class ManufacturingService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  cancelWorkOrder = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, WorkOrderDto>({
      method: 'POST',
      url: `/api/app/manufacturing/work-order/${id}/cancel`,
    },
    { apiName: this.apiName,...config });
  

  createBom = (input: CreateBomDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, BomDto>({
      method: 'POST',
      url: '/api/app/manufacturing/bom',
      params: { itemId: input.itemId, quantity: input.quantity, uom: input.uom, companyId: input.companyId, isDefault: input.isDefault, sourceWarehouseId: input.sourceWarehouseId, targetWarehouseId: input.targetWarehouseId, items: input.items },
    },
    { apiName: this.apiName,...config });
  

  createWorkOrder = (input: CreateWorkOrderDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, WorkOrderDto>({
      method: 'POST',
      url: '/api/app/manufacturing/work-order',
      params: { itemId: input.itemId, bomId: input.bomId, quantity: input.quantity, companyId: input.companyId, salesOrderId: input.salesOrderId, sourceWarehouseId: input.sourceWarehouseId, wipWarehouseId: input.wipWarehouseId, fgWarehouseId: input.fgWarehouseId, plannedStartDate: input.plannedStartDate, plannedEndDate: input.plannedEndDate, notes: input.notes },
    },
    { apiName: this.apiName,...config });
  

  deleteBom = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/manufacturing/bom/${id}`,
    },
    { apiName: this.apiName,...config });
  

  deleteWorkOrder = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/manufacturing/work-order/${id}`,
    },
    { apiName: this.apiName,...config });
  

  getBom = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, BomDto>({
      method: 'GET',
      url: `/api/app/manufacturing/bom/${id}`,
    },
    { apiName: this.apiName,...config });
  

  getBomList = (input: PagedAndSortedResultRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<BomDto>>({
      method: 'GET',
      url: '/api/app/manufacturing/bom',
      params: { sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  getWorkOrder = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, WorkOrderDto>({
      method: 'GET',
      url: `/api/app/manufacturing/work-order/${id}`,
    },
    { apiName: this.apiName,...config });
  

  getWorkOrderList = (input: GetWorkOrderListDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<WorkOrderDto>>({
      method: 'GET',
      url: '/api/app/manufacturing/work-order',
      params: { status: input.status, filter: input.filter, companyId: input.companyId, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  recordProduction = (id: string, quantity: number, config?: Partial<Rest.Config>) =>
    this.restService.request<any, WorkOrderDto>({
      method: 'POST',
      url: `/api/app/manufacturing/work-order/${id}/record-production`,
      params: { quantity },
    },
    { apiName: this.apiName,...config });
  

  startWorkOrder = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, WorkOrderDto>({
      method: 'POST',
      url: `/api/app/manufacturing/work-order/${id}/start`,
    },
    { apiName: this.apiName,...config });
  

  stopWorkOrder = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, WorkOrderDto>({
      method: 'POST',
      url: `/api/app/manufacturing/work-order/${id}/stop`,
    },
    { apiName: this.apiName,...config });
  

  submitWorkOrder = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, WorkOrderDto>({
      method: 'POST',
      url: `/api/app/manufacturing/work-order/${id}/submit`,
    },
    { apiName: this.apiName,...config });
}