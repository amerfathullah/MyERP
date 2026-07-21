import type { CreateInvoiceDiscountingDto, InvoiceDiscountingDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class InvoiceDiscountingService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  getList = (input: any, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<InvoiceDiscountingDto>>({
      method: 'GET',
      url: '/api/app/invoice-discounting',
      params: { ...input },
    },
    { apiName: this.apiName,...config });
  

  calculate = (input: CreateInvoiceDiscountingDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, InvoiceDiscountingDto>({
      method: 'POST',
      url: '/api/app/invoice-discounting/calculate',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  disburse = (input: CreateInvoiceDiscountingDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, InvoiceDiscountingDto>({
      method: 'POST',
      url: '/api/app/invoice-discounting/disburse',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  disburseById = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, InvoiceDiscountingDto>({
      method: 'POST',
      url: `/api/app/invoice-discounting/${id}/disburse`,
    },
    { apiName: this.apiName,...config });
  

  settleById = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, InvoiceDiscountingDto>({
      method: 'POST',
      url: `/api/app/invoice-discounting/${id}/settle`,
    },
    { apiName: this.apiName,...config });
  

  settle = (companyId: string, amount: number, shortTermLoanAccountId: string, receivableAccountId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, InvoiceDiscountingDto>({
      method: 'POST',
      url: '/api/app/invoice-discounting/settle',
      params: { companyId, amount, shortTermLoanAccountId, receivableAccountId },
    },
    { apiName: this.apiName,...config });
}