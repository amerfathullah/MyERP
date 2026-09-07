import type { CreateUpdateShipmentParcelTemplateDto, GetShipmentParcelTemplateListDto, ShipmentParcelTemplateDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class ShipmentParcelTemplateService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  create = (input: CreateUpdateShipmentParcelTemplateDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ShipmentParcelTemplateDto>({
      method: 'POST',
      url: '/api/app/shipment-parcel-template',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/shipment-parcel-template/${id}`,
    },
    { apiName: this.apiName,...config });
  

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ShipmentParcelTemplateDto>({
      method: 'GET',
      url: `/api/app/shipment-parcel-template/${id}`,
    },
    { apiName: this.apiName,...config });
  

  getAllList = (config?: Partial<Rest.Config>) =>
    this.restService.request<any, ShipmentParcelTemplateDto[]>({
      method: 'GET',
      url: '/api/app/shipment-parcel-template/list',
    },
    { apiName: this.apiName,...config });
  

  getList = (input: GetShipmentParcelTemplateListDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<ShipmentParcelTemplateDto>>({
      method: 'GET',
      url: '/api/app/shipment-parcel-template',
      params: { filter: input.filter, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  update = (id: string, input: CreateUpdateShipmentParcelTemplateDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ShipmentParcelTemplateDto>({
      method: 'PUT',
      url: `/api/app/shipment-parcel-template/${id}`,
      body: input,
    },
    { apiName: this.apiName,...config });
}