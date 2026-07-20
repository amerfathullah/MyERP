import type { CreateUpdatePaymentTermsTemplateDto, PaymentTermsTemplateDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedAndSortedResultRequestDto, PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class PaymentTermsTemplateService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  create = (input: CreateUpdatePaymentTermsTemplateDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PaymentTermsTemplateDto>({
      method: 'POST',
      url: '/api/app/payment-terms-template',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/payment-terms-template/${id}`,
    },
    { apiName: this.apiName,...config });
  

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PaymentTermsTemplateDto>({
      method: 'GET',
      url: `/api/app/payment-terms-template/${id}`,
    },
    { apiName: this.apiName,...config });
  

  getList = (input: PagedAndSortedResultRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<PaymentTermsTemplateDto>>({
      method: 'GET',
      url: '/api/app/payment-terms-template',
      params: { sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  update = (id: string, input: CreateUpdatePaymentTermsTemplateDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PaymentTermsTemplateDto>({
      method: 'PUT',
      url: `/api/app/payment-terms-template/${id}`,
      body: input,
    },
    { apiName: this.apiName,...config });
}