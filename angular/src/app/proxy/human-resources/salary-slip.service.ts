import type { CreateSalarySlipDto, SalarySlipDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';
import type { CompanyFilteredPagedRequestDto } from '../shared/models';

@Injectable({
  providedIn: 'root',
})
export class SalarySlipService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  cancel = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, SalarySlipDto>({
      method: 'POST',
      url: `/api/app/salary-slip/${id}/cancel`,
    },
    { apiName: this.apiName,...config });
  

  create = (input: CreateSalarySlipDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, SalarySlipDto>({
      method: 'POST',
      url: '/api/app/salary-slip',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/salary-slip/${id}`,
    },
    { apiName: this.apiName,...config });
  

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, SalarySlipDto>({
      method: 'GET',
      url: `/api/app/salary-slip/${id}`,
    },
    { apiName: this.apiName,...config });
  

  getList = (input: CompanyFilteredPagedRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<SalarySlipDto>>({
      method: 'GET',
      url: '/api/app/salary-slip',
      params: { companyId: input.companyId, filter: input.filter, status: input.status, fromDate: input.fromDate, toDate: input.toDate, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  submit = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, SalarySlipDto>({
      method: 'POST',
      url: `/api/app/salary-slip/${id}/submit`,
    },
    { apiName: this.apiName,...config });
  

  update = (id: string, input: CreateSalarySlipDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, SalarySlipDto>({
      method: 'PUT',
      url: `/api/app/salary-slip/${id}`,
      body: input,
    },
    { apiName: this.apiName,...config });
}