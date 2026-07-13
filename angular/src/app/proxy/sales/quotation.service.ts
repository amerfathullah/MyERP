import type { CreateQuotationDto, QuotationDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedAndSortedResultRequestDto, PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class QuotationService {
  private restService = inject(RestService);
  apiName = 'Default';


  cancel = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, QuotationDto>({
      method: 'POST',
      url: `/api/app/quotation/${id}/cancel`,
    },
    { apiName: this.apiName,...config });


  create = (input: CreateQuotationDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, QuotationDto>({
      method: 'POST',
      url: '/api/app/quotation',
      body: input,
    },
    { apiName: this.apiName,...config });


  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, QuotationDto>({
      method: 'GET',
      url: `/api/app/quotation/${id}`,
    },
    { apiName: this.apiName,...config });


  getList = (input: PagedAndSortedResultRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<QuotationDto>>({
      method: 'GET',
      url: '/api/app/quotation',
      params: { sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });


  submit = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, QuotationDto>({
      method: 'POST',
      url: `/api/app/quotation/${id}/submit`,
    },
    { apiName: this.apiName,...config });

  markLost = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, QuotationDto>({
      method: 'POST',
      url: `/api/app/quotation/${id}/mark-lost`,
    },
    { apiName: this.apiName,...config });
}
