import type { CreateShipmentDto, ShipmentDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';
import type { CompanyFilteredPagedRequestDto } from '../shared/models';

@Injectable({
  providedIn: 'root',
})
export class ShipmentService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  cancel = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ShipmentDto>({
      method: 'POST',
      url: `/api/app/shipment/${id}/cancel`,
    },
    { apiName: this.apiName,...config });
  

  create = (input: CreateShipmentDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ShipmentDto>({
      method: 'POST',
      url: '/api/app/shipment',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ShipmentDto>({
      method: 'GET',
      url: `/api/app/shipment/${id}`,
    },
    { apiName: this.apiName,...config });
  

  getList = (input: CompanyFilteredPagedRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<ShipmentDto>>({
      method: 'GET',
      url: '/api/app/shipment',
      params: { companyId: input.companyId, filter: input.filter, status: input.status, fromDate: input.fromDate, toDate: input.toDate, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  markDelivered = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ShipmentDto>({
      method: 'POST',
      url: `/api/app/shipment/${id}/mark-delivered`,
    },
    { apiName: this.apiName,...config });
  

  markInTransit = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ShipmentDto>({
      method: 'POST',
      url: `/api/app/shipment/${id}/mark-in-transit`,
    },
    { apiName: this.apiName,...config });
  

  submit = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ShipmentDto>({
      method: 'POST',
      url: `/api/app/shipment/${id}/submit`,
    },
    { apiName: this.apiName,...config });
}