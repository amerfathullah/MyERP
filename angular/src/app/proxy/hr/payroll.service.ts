import type { PayrollEntryDto, PayrollEntryLineDto, CreatePayrollEntryDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedAndSortedResultRequestDto, PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class PayrollService {
  private restService = inject(RestService);
  apiName = 'Default';

  create = (input: CreatePayrollEntryDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PayrollEntryDto>({
      method: 'POST',
      url: '/api/app/payroll',
      body: input,
    },
    { apiName: this.apiName, ...config });

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PayrollEntryDto>({
      method: 'GET',
      url: `/api/app/payroll/${id}`,
    },
    { apiName: this.apiName, ...config });

  getList = (input: PagedAndSortedResultRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<PayrollEntryDto>>({
      method: 'GET',
      url: '/api/app/payroll',
      params: { sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName, ...config });

  submit = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PayrollEntryDto>({
      method: 'POST',
      url: `/api/app/payroll/${id}/submit`,
    },
    { apiName: this.apiName, ...config });

  cancel = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PayrollEntryDto>({
      method: 'POST',
      url: `/api/app/payroll/${id}/cancel`,
    },
    { apiName: this.apiName, ...config });
}
