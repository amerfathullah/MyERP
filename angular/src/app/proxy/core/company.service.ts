import type { CompanyDto, CreateUpdateCompanyDto, UpdateCompanySettingsDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedAndSortedResultRequestDto, PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class CompanyService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  create = (input: CreateUpdateCompanyDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, CompanyDto>({
      method: 'POST',
      url: '/api/app/company',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/company/${id}`,
    },
    { apiName: this.apiName,...config });
  

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, CompanyDto>({
      method: 'GET',
      url: `/api/app/company/${id}`,
    },
    { apiName: this.apiName,...config });
  

  getList = (input: PagedAndSortedResultRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<CompanyDto>>({
      method: 'GET',
      url: '/api/app/company',
      params: { sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  setupNewCompany = (companyId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'POST',
      url: `/api/app/company/setup-new-company/${companyId}`,
    },
    { apiName: this.apiName,...config });
  

  update = (id: string, input: CreateUpdateCompanyDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, CompanyDto>({
      method: 'PUT',
      url: `/api/app/company/${id}`,
      body: input,
    },
    { apiName: this.apiName,...config });
  

  updateSettings = (id: string, input: UpdateCompanySettingsDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'PUT',
      url: `/api/app/company/${id}/settings`,
      body: input,
    },
    { apiName: this.apiName,...config });
}