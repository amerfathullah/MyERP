import type { CreateServiceLevelAgreementDto, GetServiceLevelAgreementListDto, ServiceLevelAgreementDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class ServiceLevelAgreementService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  create = (input: CreateServiceLevelAgreementDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ServiceLevelAgreementDto>({
      method: 'POST',
      url: '/api/app/service-level-agreement',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/service-level-agreement/${id}`,
    },
    { apiName: this.apiName,...config });
  

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ServiceLevelAgreementDto>({
      method: 'GET',
      url: `/api/app/service-level-agreement/${id}`,
    },
    { apiName: this.apiName,...config });
  

  getList = (input: GetServiceLevelAgreementListDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<ServiceLevelAgreementDto>>({
      method: 'GET',
      url: '/api/app/service-level-agreement',
      params: { companyId: input.companyId, filter: input.filter, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  update = (id: string, input: CreateServiceLevelAgreementDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ServiceLevelAgreementDto>({
      method: 'PUT',
      url: `/api/app/service-level-agreement/${id}`,
      body: input,
    },
    { apiName: this.apiName,...config });
}