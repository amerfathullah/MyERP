import type { CreateUpdateQuotationLostReasonDto, GetQuotationLostReasonListDto, QuotationLostReasonDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class QuotationLostReasonService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  create = (input: CreateUpdateQuotationLostReasonDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, QuotationLostReasonDto>({
      method: 'POST',
      url: '/api/app/quotation-lost-reason',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/quotation-lost-reason/${id}`,
    },
    { apiName: this.apiName,...config });
  

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, QuotationLostReasonDto>({
      method: 'GET',
      url: `/api/app/quotation-lost-reason/${id}`,
    },
    { apiName: this.apiName,...config });
  

  getAllList = (config?: Partial<Rest.Config>) =>
    this.restService.request<any, QuotationLostReasonDto[]>({
      method: 'GET',
      url: '/api/app/quotation-lost-reason/list',
    },
    { apiName: this.apiName,...config });
  

  getList = (input: GetQuotationLostReasonListDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<QuotationLostReasonDto>>({
      method: 'GET',
      url: '/api/app/quotation-lost-reason',
      params: { filter: input.filter, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  update = (id: string, input: CreateUpdateQuotationLostReasonDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, QuotationLostReasonDto>({
      method: 'PUT',
      url: `/api/app/quotation-lost-reason/${id}`,
      body: input,
    },
    { apiName: this.apiName,...config });
}