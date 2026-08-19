import type { CreateMasterProductionScheduleDto, FetchMaterialRequestsDto, FetchSalesOrdersDto, MasterProductionScheduleDto, UpdateMasterProductionScheduleDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';
import type { CompanyFilteredPagedRequestDto } from '../shared/models';

@Injectable({
  providedIn: 'root',
})
export class MasterProductionScheduleService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  cancel = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, MasterProductionScheduleDto>({
      method: 'POST',
      url: `/api/app/master-production-schedule/${id}/cancel`,
    },
    { apiName: this.apiName,...config });
  

  computeActualDemand = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, MasterProductionScheduleDto>({
      method: 'POST',
      url: `/api/app/master-production-schedule/${id}/compute-actual-demand`,
    },
    { apiName: this.apiName,...config });
  

  create = (input: CreateMasterProductionScheduleDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, MasterProductionScheduleDto>({
      method: 'POST',
      url: '/api/app/master-production-schedule',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/master-production-schedule/${id}`,
    },
    { apiName: this.apiName,...config });
  

  fetchMaterialRequests = (id: string, input: FetchMaterialRequestsDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, MasterProductionScheduleDto>({
      method: 'POST',
      url: `/api/app/master-production-schedule/${id}/fetch-material-requests`,
      body: input,
    },
    { apiName: this.apiName,...config });
  

  fetchSalesOrders = (id: string, input: FetchSalesOrdersDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, MasterProductionScheduleDto>({
      method: 'POST',
      url: `/api/app/master-production-schedule/${id}/fetch-sales-orders`,
      body: input,
    },
    { apiName: this.apiName,...config });
  

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, MasterProductionScheduleDto>({
      method: 'GET',
      url: `/api/app/master-production-schedule/${id}`,
    },
    { apiName: this.apiName,...config });
  

  getList = (input: CompanyFilteredPagedRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<MasterProductionScheduleDto>>({
      method: 'GET',
      url: '/api/app/master-production-schedule',
      params: { companyId: input.companyId, filter: input.filter, status: input.status, fromDate: input.fromDate, toDate: input.toDate, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  submit = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, MasterProductionScheduleDto>({
      method: 'POST',
      url: `/api/app/master-production-schedule/${id}/submit`,
    },
    { apiName: this.apiName,...config });
  

  update = (id: string, input: UpdateMasterProductionScheduleDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, MasterProductionScheduleDto>({
      method: 'PUT',
      url: `/api/app/master-production-schedule/${id}`,
      body: input,
    },
    { apiName: this.apiName,...config });
}