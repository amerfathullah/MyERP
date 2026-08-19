import type { CreateUpdateVehicleDto, VehicleDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';
import type { CompanyFilteredPagedRequestDto } from '../shared/models';

@Injectable({
  providedIn: 'root',
})
export class VehicleService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  create = (input: CreateUpdateVehicleDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, VehicleDto>({
      method: 'POST',
      url: '/api/app/vehicle',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/vehicle/${id}`,
    },
    { apiName: this.apiName,...config });
  

  disable = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, VehicleDto>({
      method: 'POST',
      url: `/api/app/vehicle/${id}/disable`,
    },
    { apiName: this.apiName,...config });
  

  enable = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, VehicleDto>({
      method: 'POST',
      url: `/api/app/vehicle/${id}/enable`,
    },
    { apiName: this.apiName,...config });
  

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, VehicleDto>({
      method: 'GET',
      url: `/api/app/vehicle/${id}`,
    },
    { apiName: this.apiName,...config });
  

  getList = (input: CompanyFilteredPagedRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<VehicleDto>>({
      method: 'GET',
      url: '/api/app/vehicle',
      params: { companyId: input.companyId, filter: input.filter, status: input.status, fromDate: input.fromDate, toDate: input.toDate, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  update = (id: string, input: CreateUpdateVehicleDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, VehicleDto>({
      method: 'PUT',
      url: `/api/app/vehicle/${id}`,
      body: input,
    },
    { apiName: this.apiName,...config });
}