import type { CreateRfqDto, RfqDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';
import type { CompanyFilteredPagedRequestDto } from '../shared/models';

@Injectable({
  providedIn: 'root',
})
export class RequestForQuotationService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  cancel = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, RfqDto>({
      method: 'POST',
      url: `/api/app/request-for-quotation/${id}/cancel`,
    },
    { apiName: this.apiName,...config });
  

  create = (input: CreateRfqDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, RfqDto>({
      method: 'POST',
      url: '/api/app/request-for-quotation',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, RfqDto>({
      method: 'GET',
      url: `/api/app/request-for-quotation/${id}`,
    },
    { apiName: this.apiName,...config });
  

  getList = (input: CompanyFilteredPagedRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<RfqDto>>({
      method: 'GET',
      url: '/api/app/request-for-quotation',
      params: { companyId: input.companyId, filter: input.filter, status: input.status, fromDate: input.fromDate, toDate: input.toDate, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  submit = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, RfqDto>({
      method: 'POST',
      url: `/api/app/request-for-quotation/${id}/submit`,
    },
    { apiName: this.apiName,...config });
}