import type { CreateSupplierQuotationDto, SupplierQuotationDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedAndSortedResultRequestDto, PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class SupplierQuotationService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  cancel = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, SupplierQuotationDto>({
      method: 'POST',
      url: `/api/app/supplier-quotation/${id}/cancel`,
    },
    { apiName: this.apiName,...config });
  

  create = (input: CreateSupplierQuotationDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, SupplierQuotationDto>({
      method: 'POST',
      url: '/api/app/supplier-quotation',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, SupplierQuotationDto>({
      method: 'GET',
      url: `/api/app/supplier-quotation/${id}`,
    },
    { apiName: this.apiName,...config });
  

  getList = (input: PagedAndSortedResultRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<SupplierQuotationDto>>({
      method: 'GET',
      url: '/api/app/supplier-quotation',
      params: { sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  submit = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, SupplierQuotationDto>({
      method: 'POST',
      url: `/api/app/supplier-quotation/${id}/submit`,
    },
    { apiName: this.apiName,...config });
}