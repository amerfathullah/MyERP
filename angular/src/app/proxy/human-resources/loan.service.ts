import type { CreateLoanDto, DisburseLoanDto, LoanDto, RecordRepaymentDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';
import type { CompanyFilteredPagedRequestDto } from '../shared/models';

@Injectable({
  providedIn: 'root',
})
export class LoanService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  cancel = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, LoanDto>({
      method: 'POST',
      url: `/api/app/loan/${id}/cancel`,
    },
    { apiName: this.apiName,...config });
  

  create = (input: CreateLoanDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, LoanDto>({
      method: 'POST',
      url: '/api/app/loan',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  disburse = (id: string, input: DisburseLoanDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, LoanDto>({
      method: 'POST',
      url: `/api/app/loan/${id}/disburse`,
      body: input,
    },
    { apiName: this.apiName,...config });
  

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, LoanDto>({
      method: 'GET',
      url: `/api/app/loan/${id}`,
    },
    { apiName: this.apiName,...config });
  

  getList = (input: CompanyFilteredPagedRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<LoanDto>>({
      method: 'GET',
      url: '/api/app/loan',
      params: { companyId: input.companyId, filter: input.filter, status: input.status, fromDate: input.fromDate, toDate: input.toDate, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  recordRepayment = (id: string, input: RecordRepaymentDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, LoanDto>({
      method: 'POST',
      url: `/api/app/loan/${id}/record-repayment`,
      body: input,
    },
    { apiName: this.apiName,...config });
  

  sanction = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, LoanDto>({
      method: 'POST',
      url: `/api/app/loan/${id}/sanction`,
    },
    { apiName: this.apiName,...config });
}