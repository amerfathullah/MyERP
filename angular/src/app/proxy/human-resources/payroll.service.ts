import type { CreatePayrollBankEntryDto, CreatePayrollEntryDto, GetPayrollListDto, PayrollBankEntryResultDto, PayrollEntryDto, PayrollPreviewDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class PayrollService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  cancel = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PayrollEntryDto>({
      method: 'POST',
      url: `/api/app/payroll/${id}/cancel`,
    },
    { apiName: this.apiName,...config });
  

  create = (input: CreatePayrollEntryDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PayrollEntryDto>({
      method: 'POST',
      url: '/api/app/payroll',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  createBankEntry = (input: CreatePayrollBankEntryDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PayrollBankEntryResultDto>({
      method: 'POST',
      url: '/api/app/payroll/bank-entry',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PayrollEntryDto>({
      method: 'GET',
      url: `/api/app/payroll/${id}`,
    },
    { apiName: this.apiName,...config });
  

  getEmployeePreview = (input: CreatePayrollEntryDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PayrollPreviewDto>({
      method: 'GET',
      url: '/api/app/payroll/employee-preview',
      params: { companyId: input.companyId, year: input.year, month: input.month },
    },
    { apiName: this.apiName,...config });
  

  getList = (input: GetPayrollListDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<PayrollEntryDto>>({
      method: 'GET',
      url: '/api/app/payroll',
      params: { companyId: input.companyId, filter: input.filter, status: input.status, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  submit = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PayrollEntryDto>({
      method: 'POST',
      url: `/api/app/payroll/${id}/submit`,
    },
    { apiName: this.apiName,...config });
}