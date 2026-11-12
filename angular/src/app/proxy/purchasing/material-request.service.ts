import type { CreateMaterialRequestDto, GetMaterialRequestListDto, MaterialRequestDto, MrFulfillmentStatusDto } from './dtos/models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';
import type { BatchCreateWorkOrdersResultDto } from '../manufacturing/models';

@Injectable({
  providedIn: 'root',
})
export class MaterialRequestService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  
  cancel = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, MaterialRequestDto>({
      method: 'POST',
      headers: { Accept: 'application/json' },
      url: `/api/app/material-request/${id}/cancel`,
    },
      { apiName: this.apiName,...config });
  

  
  create = (input: CreateMaterialRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, MaterialRequestDto>({
      method: 'POST',
      headers: { Accept: 'application/json' },
      url: '/api/app/material-request',
      body: input,
    },
      { apiName: this.apiName,...config });
  

  
  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/material-request/${id}`,
    },
      { apiName: this.apiName,...config });
  

  
  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, MaterialRequestDto>({
      method: 'GET',
      headers: { Accept: 'application/json' },
      url: `/api/app/material-request/${id}`,
    },
      { apiName: this.apiName,...config });
  

  
  getFulfillmentStatus = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, MrFulfillmentStatusDto>({
      method: 'GET',
      headers: { Accept: 'application/json' },
      url: `/api/app/material-request/${id}/fulfillment-status`,
    },
      { apiName: this.apiName,...config });
  

  
  getList = (input: GetMaterialRequestListDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<MaterialRequestDto>>({
      method: 'GET',
      headers: { Accept: 'application/json' },
      url: '/api/app/material-request',
      params: { requestType: input.requestType, companyId: input.companyId, filter: input.filter, status: input.status, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
      { apiName: this.apiName,...config });
  

  
  raiseWorkOrders = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, BatchCreateWorkOrdersResultDto>({
      method: 'POST',
      headers: { Accept: 'application/json' },
      url: `/api/app/material-request/${id}/raise-work-orders`,
    },
      { apiName: this.apiName,...config });
  

  
  reopen = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, MaterialRequestDto>({
      method: 'POST',
      headers: { Accept: 'application/json' },
      url: `/api/app/material-request/${id}/reopen`,
    },
      { apiName: this.apiName,...config });
  

  
  stop = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, MaterialRequestDto>({
      method: 'POST',
      headers: { Accept: 'application/json' },
      url: `/api/app/material-request/${id}/stop`,
    },
      { apiName: this.apiName,...config });
  

  
  submit = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, MaterialRequestDto>({
      method: 'POST',
      headers: { Accept: 'application/json' },
      url: `/api/app/material-request/${id}/submit`,
    },
      { apiName: this.apiName,...config });
  

  
  update = (id: string, input: CreateMaterialRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, MaterialRequestDto>({
      method: 'PUT',
      headers: { Accept: 'application/json' },
      url: `/api/app/material-request/${id}`,
      body: input,
    },
      { apiName: this.apiName,...config });
}
