import type { CreateWarehouseAccountDto, WarehouseAccountDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { ListResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class WarehouseAccountService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/warehouse-account/${id}`,
    },
    { apiName: this.apiName,...config });
  

  getList = (companyId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ListResultDto<WarehouseAccountDto>>({
      method: 'GET',
      url: '/api/app/warehouse-account',
      params: { companyId },
    },
    { apiName: this.apiName,...config });
  

  save = (input: CreateWarehouseAccountDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, WarehouseAccountDto>({
      method: 'POST',
      url: '/api/app/warehouse-account/save',
      body: input,
    },
    { apiName: this.apiName,...config });
}