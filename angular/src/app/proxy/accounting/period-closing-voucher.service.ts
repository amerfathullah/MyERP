import type { CreatePeriodClosingVoucherDto, PeriodClosingVoucherDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';
import type { CompanyFilteredPagedRequestDto } from '../shared/models';

@Injectable({
  providedIn: 'root',
})
export class PeriodClosingVoucherService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  cancel = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PeriodClosingVoucherDto>({
      method: 'POST',
      url: `/api/app/period-closing-voucher/${id}/cancel`,
    },
    { apiName: this.apiName,...config });
  

  create = (input: CreatePeriodClosingVoucherDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PeriodClosingVoucherDto>({
      method: 'POST',
      url: '/api/app/period-closing-voucher',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PeriodClosingVoucherDto>({
      method: 'GET',
      url: `/api/app/period-closing-voucher/${id}`,
    },
    { apiName: this.apiName,...config });
  

  getList = (input: CompanyFilteredPagedRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<PeriodClosingVoucherDto>>({
      method: 'GET',
      url: '/api/app/period-closing-voucher',
      params: { companyId: input.companyId, filter: input.filter, status: input.status, fromDate: input.fromDate, toDate: input.toDate, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  submit = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PeriodClosingVoucherDto>({
      method: 'POST',
      url: `/api/app/period-closing-voucher/${id}/submit`,
    },
    { apiName: this.apiName,...config });
}