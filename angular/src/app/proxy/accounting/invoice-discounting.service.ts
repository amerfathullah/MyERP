import type { CreateInvoiceDiscountingDto, InvoiceDiscountingDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedAndSortedResultRequestDto, PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class InvoiceDiscountingService {
  private restService = inject(RestService);
  apiName = 'Default';
  

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
      url: `/api/app/invoice-discounting/${id}/disburse-by-id`,
    },
    { apiName: this.apiName,...config });
  

  getList = (input: PagedAndSortedResultRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<InvoiceDiscountingDto>>({
      method: 'GET',
      url: '/api/app/invoice-discounting',
      params: { sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  settle = (companyId: string, amount: number, shortTermLoanAccountId: string, receivableAccountId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, InvoiceDiscountingDto>({
      method: 'POST',
      url: '/api/app/invoice-discounting/settle',
      params: { companyId, amount, shortTermLoanAccountId, receivableAccountId },
    },
    { apiName: this.apiName,...config });
  

  settleById = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, InvoiceDiscountingDto>({
      method: 'POST',
      url: `/api/app/invoice-discounting/${id}/settle-by-id`,
    },
    { apiName: this.apiName,...config });
}